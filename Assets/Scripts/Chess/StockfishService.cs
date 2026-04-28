using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

[DisallowMultipleComponent]
public class StockfishService : MonoBehaviour
{
    #region Singleton

    public static StockfishService Instance { get; private set; }

    public static StockfishService GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        StockfishService existing = FindFirstObjectByType<StockfishService>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject host = new("StockfishService");
        Instance = host.AddComponent<StockfishService>();
        return Instance;
    }

    #endregion

    #region Variables

    const string StockfishFolder = "stockfish";
    [SerializeField] int defaultDepth = 10;

    readonly ConcurrentQueue<string> outputLines = new();

    Process stockfishProcess;
    StreamWriter processInput;
    CancellationTokenSource processLifetimeTokenSource;

    TaskCompletionSource<bool> uciReadyTask;
    TaskCompletionSource<bool> isReadyTask;
    TaskCompletionSource<string> pendingBestMoveTask;

    bool engineReady;

    #endregion

    #region Events

    public event Action<string> EngineLineReceived;

    #endregion

    #region Properties

    public bool IsReady => engineReady && stockfishProcess is { HasExited: false };
    public int DefaultDepth => defaultDepth;

    #endregion

    #region Unity

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    async void Start()
    {
        await InitializeAsync();
    }

    void Update()
    {
        while (outputLines.TryDequeue(out string line))
        {
            HandleEngineLine(line);
        }
    }

    void OnDestroy()
    {
        ShutdownProcess();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region API

    public async Task<bool> InitializeAsync()
    {
        if (IsReady)
        {
            return true;
        }

        if (!StartProcess())
        {
            return false;
        }

        processLifetimeTokenSource ??= new CancellationTokenSource();
        uciReadyTask = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        isReadyTask = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        SendCommand("uci");
        if (!await WaitForSignalWithTimeout(uciReadyTask.Task, "uciok", 5000))
        {
            return false;
        }

        SendCommand("isready");
        if (!await WaitForSignalWithTimeout(isReadyTask.Task, "readyok", 5000))
        {
            return false;
        }

        engineReady = true;
        Debug.Log("[StockfishService] Engine ready.");
        return true;
    }

    public async Task<string> RequestBestMoveAsync(string fen, int? depthOverride = null)
    {
        if (string.IsNullOrWhiteSpace(fen))
        {
            Debug.LogError("[StockfishService] Cannot request best move with empty FEN.");
            return null;
        }

        if (!await InitializeAsync())
        {
            return null;
        }

        if (pendingBestMoveTask != null && !pendingBestMoveTask.Task.IsCompleted)
        {
            Debug.LogWarning("[StockfishService] Best move request already in progress.");
            return null;
        }

        pendingBestMoveTask = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        SendCommand($"position fen {fen}");
        int depth = Mathf.Max(1, depthOverride ?? defaultDepth);
        SendCommand($"go depth {depth}");

        string bestMove = await WaitForResultWithTimeout(pendingBestMoveTask.Task, "bestmove", 12000);
        if (string.IsNullOrWhiteSpace(bestMove))
        {
            pendingBestMoveTask = null;
            return null;
        }

        return bestMove;
    }

    public bool TryParseBestMove(string bestMoveRaw, out string from, out string to, out PieceType? promotionPiece)
    {
        from = null;
        to = null;
        promotionPiece = null;

        if (string.IsNullOrWhiteSpace(bestMoveRaw))
        {
            return false;
        }

        string[] tokens = bestMoveRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2 || !tokens[0].Equals("bestmove", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string move = tokens[1].Trim();
        if (move.Equals("(none)", StringComparison.OrdinalIgnoreCase) || move.Length < 4)
        {
            return false;
        }

        from = move.Substring(0, 2).ToUpperInvariant();
        to = move.Substring(2, 2).ToUpperInvariant();
        if (move.Length >= 5)
        {
            promotionPiece = move[4] switch
            {
                'q' or 'Q' => PieceType.Queen,
                'r' or 'R' => PieceType.Rook,
                'b' or 'B' => PieceType.Bishop,
                'n' or 'N' => PieceType.Knight,
                _ => null
            };
        }

        return true;
    }

    #endregion

    #region Process

    bool StartProcess()
{
    if (stockfishProcess is { HasExited: false })
    {
        return true;
    }

    string executablePath = ResolveExecutablePath();

    if (string.IsNullOrWhiteSpace(executablePath))
    {
        Debug.LogError("[StockfishService] Failed to resolve Stockfish executable.");
        return false;
    }

    try
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = executablePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        stockfishProcess = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        stockfishProcess.OutputDataReceived += OnProcessOutput;
        stockfishProcess.Exited += OnProcessExited;

        stockfishProcess.Start();
        stockfishProcess.BeginOutputReadLine();

        processInput = stockfishProcess.StandardInput;
        engineReady = false;

        Debug.Log($"[StockfishService] Started using: {executablePath}");

        return true;
    }
    catch (Exception exception)
    {
        Debug.LogError($"[StockfishService] Failed to start process: {exception.Message}");
        ShutdownProcess();
        return false;
    }
}

    void ShutdownProcess()
    {
        try
        {
            processLifetimeTokenSource?.Cancel();
            processLifetimeTokenSource?.Dispose();
            processLifetimeTokenSource = null;

            if (stockfishProcess == null)
            {
                return;
            }

            stockfishProcess.OutputDataReceived -= OnProcessOutput;
            stockfishProcess.Exited -= OnProcessExited;

            if (!stockfishProcess.HasExited)
            {
                SendCommand("quit");
                if (!stockfishProcess.WaitForExit(1000))
                {
                    stockfishProcess.Kill();
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[StockfishService] Process shutdown warning: {exception.Message}");
        }
        finally
        {
            processInput = null;
            stockfishProcess?.Dispose();
            stockfishProcess = null;
            engineReady = false;
        }
    }

    void SendCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command) || processInput == null)
        {
            return;
        }

        try
        {
            processInput.WriteLine(command);
            processInput.Flush();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[StockfishService] Failed to send command '{command}': {exception.Message}");
            engineReady = false;
        }
    }

    void OnProcessOutput(object sender, DataReceivedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Data))
        {
            return;
        }

        outputLines.Enqueue(args.Data.Trim());
    }

    void OnProcessExited(object sender, EventArgs args)
    {
        outputLines.Enqueue("__PROCESS_EXITED__");
    }

    #endregion

    #region Output Handling

    void HandleEngineLine(string line)
    {
        EngineLineReceived?.Invoke(line);

        if (line == "__PROCESS_EXITED__")
        {
            engineReady = false;
            uciReadyTask?.TrySetResult(false);
            isReadyTask?.TrySetResult(false);
            pendingBestMoveTask?.TrySetResult(null);
            Debug.LogError("[StockfishService] Process exited unexpectedly.");
            return;
        }

        if (line.Equals("uciok", StringComparison.OrdinalIgnoreCase))
        {
            uciReadyTask?.TrySetResult(true);
            return;
        }

        if (line.Equals("readyok", StringComparison.OrdinalIgnoreCase))
        {
            isReadyTask?.TrySetResult(true);
            return;
        }

        if (line.StartsWith("bestmove", StringComparison.OrdinalIgnoreCase))
        {
            pendingBestMoveTask?.TrySetResult(line);
        }
    }

    static async Task<bool> WaitForSignalWithTimeout(Task<bool> task, string label, int timeoutMs)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
        if (completed != task)
        {
            Debug.LogError($"[StockfishService] Timeout waiting for {label}.");
            return false;
        }

        bool signal = await task;
        if (!signal)
        {
            Debug.LogError($"[StockfishService] Engine returned failure while waiting for {label}.");
        }

        return signal;
    }

    static async Task<string> WaitForResultWithTimeout(Task<string> task, string label, int timeoutMs)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
        if (completed != task)
        {
            Debug.LogError($"[StockfishService] Timeout waiting for {label}.");
            return null;
        }

        return await task;
    }

    #endregion
    
    #region Path Resolution
    
    string ResolveExecutablePath()
    {
        string folderPath = Path.Combine(Application.dataPath, StockfishFolder);
    
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"[Stockfish] Folder not found: {folderPath}");
            return null;
        }
    
        string[] executables = Directory.GetFiles(folderPath, "*.exe", SearchOption.TopDirectoryOnly);
    
        if (executables.Length == 0)
        {
            Debug.LogError($"[Stockfish] No .exe found in: {folderPath}");
            return null;
        }
    
        // Prefer AVX2 builds if available
        foreach (string exe in executables)
        {
            if (exe.Contains("avx2", StringComparison.OrdinalIgnoreCase))
            {
                return exe;
            }
        }
    
        if (executables.Length > 1)
        {
            Debug.LogWarning($"[Stockfish] Multiple executables found. Using first: {executables[0]}");
        }
    
        return executables[0];
    }
    
    #endregion
}
