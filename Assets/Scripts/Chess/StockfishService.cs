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

        StockfishService[] existingServices = FindObjectsByType<StockfishService>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        StockfishService existing = existingServices.Length > 0 ? existingServices[0] : null;
        if (existing != null)
        {
            Instance = existing;
            Debug.Log("[ChessRuntimeBootstrap] Reused existing instance: StockfishService");
            return Instance;
        }

        GameObject host = new("StockfishService");
        Instance = host.AddComponent<StockfishService>();
        Debug.Log("[ChessRuntimeBootstrap] Created fallback instance: StockfishService");
        return Instance;
    }

    #endregion

    #region Variables

    const string StockfishFolder = "stockfish";
    [SerializeField] int defaultDepth = 10;
    [SerializeField] string executablePathOverride;

    readonly ConcurrentQueue<string> outputLines = new();

    Process stockfishProcess;
    StreamWriter processInput;
    readonly SemaphoreSlim initializeSemaphore = new(1, 1);

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
            Debug.LogWarning("[ChessRuntimeBootstrap] Persistent instance kept: StockfishService");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    async void Start()
    {
        try
        {
            bool initialized = await InitializeAsync();
            if (!initialized)
            {
                Debug.LogError("[StockfishService] Initialization failed. Engine unavailable.");
            }
        }
        catch (Exception exception)
        {
            engineReady = false;
            Debug.LogError($"[StockfishService] Initialization failure: {exception}");
        }
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
        await initializeSemaphore.WaitAsync();
        try
        {
            if (IsReady)
            {
                return true;
            }

            if (!StartProcess())
            {
                return false;
            }

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
        finally
        {
            initializeSemaphore.Release();
        }
    }

    public Task<string> RequestBestMoveAsync(string fen, int? depthOverride = null)
    {
        return RequestBestMoveAsync(fen, CancellationToken.None, depthOverride);
    }

    public async Task<string> RequestBestMoveAsync(string fen, CancellationToken cancellationToken, int? depthOverride = null)
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
        string bestMove;
        try
        {
            bestMove = await WaitForResultWithTimeout(pendingBestMoveTask.Task, "bestmove", 12000, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            SendCommand("stop");
            pendingBestMoveTask?.TrySetCanceled(cancellationToken);
            pendingBestMoveTask = null;
            throw;
        }

        if (string.IsNullOrWhiteSpace(bestMove))
        {
            pendingBestMoveTask = null;
            return null;
        }

        return bestMove;
    }

    public async Task<bool> TrySyncPositionAsync(string fen, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fen))
        {
            return false;
        }

        if (!await InitializeAsync())
        {
            return false;
        }

        CancelThinking();
        isReadyTask = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SendCommand($"position fen {fen}");
        SendCommand("isready");
        return await WaitForSignalWithTimeout(isReadyTask.Task, "readyok", 5000);
    }

    public void CancelThinking()
    {
        SendCommand("stop");
        pendingBestMoveTask?.TrySetCanceled();
        pendingBestMoveTask = null;
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

    static async Task<string> WaitForResultWithTimeout(Task<string> task, string label, int timeoutMs, CancellationToken cancellationToken)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(timeoutMs, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
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
        if (!string.IsNullOrWhiteSpace(executablePathOverride))
        {
            string overridePath = executablePathOverride;
            if (!Path.IsPathRooted(overridePath))
            {
                overridePath = Path.Combine(Application.dataPath, overridePath);
            }

            if (File.Exists(overridePath))
            {
                Debug.Log($"[StockfishService] Resolved executable from override: {overridePath}");
                return overridePath;
            }
        }

        string folderPath = Path.Combine(Application.dataPath, StockfishFolder);
        string defaultWindowsPath = Path.Combine(folderPath, "stockfish-windows-x86-64-avx2.exe");
        bool isWindows = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
        bool isMacOrLinux = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.LinuxEditor || Application.platform == RuntimePlatform.LinuxPlayer;

        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"[StockfishService] Folder not found: {folderPath}");
            return null;
        }

        string[] allFiles = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly);
        string[] candidateNames = isWindows
            ? new[] { "stockfish-windows-x86-64-avx2.exe", "stockfish.exe" }
            : isMacOrLinux
                ? new[] { "stockfish-macos", "stockfish-linux", "stockfish" }
                : new[] { "stockfish" };

        foreach (string candidateName in candidateNames)
        {
            string candidatePath = Path.Combine(folderPath, candidateName);
            if (File.Exists(candidatePath))
            {
                Debug.Log($"[StockfishService] Resolved executable path: {candidatePath}");
                return candidatePath;
            }
        }

        foreach (string file in allFiles)
        {
            string filename = Path.GetFileName(file);
            bool isWindowsExe = filename.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
            bool isNonWindowsStockfish = filename.Contains("stockfish", StringComparison.OrdinalIgnoreCase) && !filename.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

            if ((isWindows && isWindowsExe) || (isMacOrLinux && isNonWindowsStockfish))
            {
                Debug.Log($"[StockfishService] Resolved executable path: {file}");
                return file;
            }
        }

        Debug.LogError($"[StockfishService] No Stockfish executable found. Searched: {defaultWindowsPath}, {Path.Combine(folderPath, "stockfish.exe")}, {Path.Combine(folderPath, "stockfish-macos")}, {Path.Combine(folderPath, "stockfish-linux")}, {Path.Combine(folderPath, "stockfish")}, override='{executablePathOverride}'.");
        if (isWindows && File.Exists(defaultWindowsPath))
        {
            Debug.Log($"[StockfishService] Resolved executable path: {defaultWindowsPath}");
            return defaultWindowsPath;
        }

        return null;
    }
    
    #endregion
}
