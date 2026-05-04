using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

[DisallowMultipleComponent]
public class StockfishService : MonoBehaviour
{
    public enum EngineState
    {
        NotStarted,
        Starting,
        WaitingForUci,
        WaitingForReady,
        Retrying,
        Ready,
        Failed,
        Unavailable
    }

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
            return Instance;
        }

        GameObject host = new("StockfishService");
        Instance = host.AddComponent<StockfishService>();
        return Instance;
    }

    const string StockfishFolder = "stockfish";
    const string LoadingOverlayRootName = "StockfishLoadingOverlayRoot";

    [SerializeField] int defaultDepth = 10;
    [SerializeField] string executablePathOverride;
    [SerializeField, Min(1)] int maxInitializationRetries = 5;
    [SerializeField, Min(0.1f)] float retryDelaySeconds = 1.5f;
    [SerializeField, Min(1f)] float uciTimeoutSeconds = 6f;
    [SerializeField, Min(1f)] float readyTimeoutSeconds = 8f;

    readonly ConcurrentQueue<string> outputLines = new();
    readonly Queue<string> recentOutput = new();
    readonly SemaphoreSlim initializeSemaphore = new(1, 1);

    Process stockfishProcess;
    StreamWriter processInput;
    TaskCompletionSource<bool> uciReadyTask;
    TaskCompletionSource<bool> isReadyTask;
    TaskCompletionSource<string> pendingBestMoveTask;

    bool isQuitting;
    string resolvedExecutablePath;

    public event Action<string> EngineLineReceived;

    public bool IsReady => CurrentState == EngineState.Ready && stockfishProcess is { HasExited: false };
    public bool IsInitializing => CurrentState is EngineState.Starting or EngineState.WaitingForUci or EngineState.WaitingForReady or EngineState.Retrying;
    public bool IsUnavailable => CurrentState == EngineState.Unavailable;
    public EngineState CurrentState { get; private set; } = EngineState.NotStarted;
    public string LastError { get; private set; }
    public int CurrentRetryAttempt { get; private set; }
    public int MaxRetryAttempts => Mathf.Max(1, maxInitializationRetries);
    public int DefaultDepth => defaultDepth;

    CanvasGroup overlayCanvasGroup;
    TMP_Text overlayText;

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
        EnsureLoadingOverlay();
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

        UpdateLoadingOverlay();
    }

    void OnDestroy()
    {
        isQuitting = true;
        CleanupProcess();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public async Task<bool> InitializeAsync()
    {
        if (IsReady)
        {
            return true;
        }

        await initializeSemaphore.WaitAsync();
        try
        {
            if (IsReady)
            {
                return true;
            }

            if (IsUnavailable)
            {
                SetState(EngineState.NotStarted);
            }

            int attempts = MaxRetryAttempts;
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                CurrentRetryAttempt = attempt;
                if (attempt > 1)
                {
                    SetState(EngineState.Retrying);
                    Debug.LogWarning($"[StockfishService] Retrying startup attempt {attempt}/{attempts} after failure: {LastError}");
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
                }

                if (await TryInitializeAttemptAsync(attempt, attempts))
                {
                    return true;
                }

                CleanupProcess();
            }

            SetUnavailable($"Initialization failed after {attempts} attempts. Last error: {LastError}");
            return false;
        }
        finally
        {
            initializeSemaphore.Release();
        }
    }

    public async Task<bool> WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (IsReady)
            {
                return true;
            }

            if (IsUnavailable)
            {
                return false;
            }

            _ = InitializeAsync();
            await Task.Delay(100, cancellationToken);
        }

        return false;
    }

    async Task<bool> TryInitializeAttemptAsync(int attempt, int maxAttempts)
    {
        SetState(EngineState.Starting);
        if (!StartProcess(attempt, maxAttempts))
        {
            return false;
        }

        uciReadyTask = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        isReadyTask = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        SetState(EngineState.WaitingForUci);
        SendCommand("uci");
        if (!await WaitForSignalWithTimeout(uciReadyTask.Task, "uciok", TimeSpan.FromSeconds(uciTimeoutSeconds)))
        {
            LastError ??= "Timed out waiting for uciok.";
            SetState(EngineState.Failed);
            return false;
        }

        Debug.Log("[StockfishService] uciok received.");

        SetState(EngineState.WaitingForReady);
        SendCommand("isready");
        if (!await WaitForSignalWithTimeout(isReadyTask.Task, "readyok", TimeSpan.FromSeconds(readyTimeoutSeconds)))
        {
            LastError ??= "Timed out waiting for readyok.";
            SetState(EngineState.Failed);
            return false;
        }

        Debug.Log("[StockfishService] readyok received.");
        LastError = null;
        SetState(EngineState.Ready);
        Debug.Log("[StockfishService] Engine ready.");
        return true;
    }

    public async Task<string> RequestBestMoveAsync(string fen, CancellationToken cancellationToken, int? depthOverride = null)
    {
        if (string.IsNullOrWhiteSpace(fen))
        {
            Debug.LogError("[StockfishService] Cannot request best move with empty FEN.");
            return null;
        }

        bool ready = await WaitUntilReadyAsync(cancellationToken);
        if (!ready)
        {
            Debug.LogError($"[StockfishService] Cannot request best move because engine is unavailable. Error: {LastError}");
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
            bestMove = await WaitForResultWithTimeout(pendingBestMoveTask.Task, "bestmove", TimeSpan.FromSeconds(12), cancellationToken);
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

    public Task<string> RequestBestMoveAsync(string fen, int? depthOverride = null) => RequestBestMoveAsync(fen, CancellationToken.None, depthOverride);

    public async Task<bool> TrySyncPositionAsync(string fen, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fen)) return false;
        if (!await WaitUntilReadyAsync(cancellationToken)) return false;

        CancelThinking();
        isReadyTask = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SendCommand($"position fen {fen}");
        SendCommand("isready");
        return await WaitForSignalWithTimeout(isReadyTask.Task, "readyok", TimeSpan.FromSeconds(readyTimeoutSeconds));
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

        if (string.IsNullOrWhiteSpace(bestMoveRaw)) return false;
        string[] tokens = bestMoveRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2 || !tokens[0].Equals("bestmove", StringComparison.OrdinalIgnoreCase)) return false;

        string move = tokens[1].Trim();
        if (move.Equals("(none)", StringComparison.OrdinalIgnoreCase) || move.Length < 4) return false;

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

    bool StartProcess(int attempt, int maxAttempts)
    {
        if (stockfishProcess is { HasExited: false }) return true;

        resolvedExecutablePath = ResolveExecutablePath();
        Debug.Log($"[StockfishService] Resolved Stockfish path: {resolvedExecutablePath}");
        if (string.IsNullOrWhiteSpace(resolvedExecutablePath))
        {
            LastError = "Failed to resolve Stockfish executable path.";
            return false;
        }

        try
        {
            Debug.Log($"[StockfishService] Process start attempt {attempt}/{maxAttempts}.");
            stockfishProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = resolvedExecutablePath,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            stockfishProcess.OutputDataReceived += OnProcessOutput;
            stockfishProcess.Exited += OnProcessExited;
            stockfishProcess.Start();
            stockfishProcess.BeginOutputReadLine();
            processInput = stockfishProcess.StandardInput;
            return true;
        }
        catch (Exception exception)
        {
            LastError = $"Failed to start process: {exception.Message}";
            Debug.LogError($"[StockfishService] {LastError}");
            CleanupProcess();
            return false;
        }
    }

    void CleanupProcess()
    {
        uciReadyTask?.TrySetResult(false);
        isReadyTask?.TrySetResult(false);
        pendingBestMoveTask?.TrySetResult(null);
        uciReadyTask = null;
        isReadyTask = null;
        pendingBestMoveTask = null;

        while (outputLines.TryDequeue(out _)) { }
        recentOutput.Clear();

        try
        {
            if (!stockfishProcess.HasExited)
            {
                try
                {
                    processInput?.WriteLine("quit");
                    processInput?.Flush();
                }
                catch (Exception exception)
                {
                    if (!isQuitting)
                    {
                        Debug.LogWarning($"[StockfishService] Failed to send quit command during cleanup: {exception.Message}");
                    }
                }

                if (!stockfishProcess.WaitForExit(700))
                {
                    stockfishProcess.Kill();
                }
            }
        }
        catch (Exception exception)
        {
            if (!isQuitting)
            {
                Debug.LogWarning($"[StockfishService] Cleanup warning: {exception.Message}");
            }
        }
        finally
        {
            processInput?.Dispose();
            processInput = null;
            stockfishProcess?.Dispose();
            stockfishProcess = null;
            if (CurrentState != EngineState.Unavailable)
            {
                SetState(EngineState.NotStarted);
            }
        }
    }

    void SendCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command) || processInput == null) return;
        try
        {
            Debug.Log($"[StockfishService] UCI command sent: {command}");
            processInput.WriteLine(command);
            processInput.Flush();
        }
        catch (Exception exception)
        {
            LastError = $"Failed to send command '{command}': {exception.Message}";
            Debug.LogError($"[StockfishService] {LastError}");
            SetState(EngineState.Failed);
        }
    }

    void OnProcessOutput(object sender, DataReceivedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Data)) return;
        outputLines.Enqueue(args.Data.Trim());
    }

    void OnProcessExited(object sender, EventArgs args) => outputLines.Enqueue("__PROCESS_EXITED__");

    void HandleEngineLine(string line)
    {
        EngineLineReceived?.Invoke(line);

        if (recentOutput.Count > 15) recentOutput.Dequeue();
        recentOutput.Enqueue(line);

        if (line == "__PROCESS_EXITED__")
        {
            LastError = "Stockfish process exited while waiting for a response.";
            uciReadyTask?.TrySetResult(false);
            isReadyTask?.TrySetResult(false);
            pendingBestMoveTask?.TrySetResult(null);
            SetState(EngineState.Failed);
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

    async Task<bool> WaitForSignalWithTimeout(Task<bool> task, string label, TimeSpan timeout)
    {
        Debug.Log($"[StockfishService] Waiting for {label} with timeout {timeout.TotalSeconds:0.0}s.");
        Task completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
        {
            LastError = $"Timeout waiting for {label}. Recent output: {string.Join(" | ", recentOutput)}";
            Debug.LogError($"[StockfishService] {LastError}");
            return false;
        }

        bool signal = await task;
        if (!signal)
        {
            LastError = $"Engine returned failure while waiting for {label}.";
            Debug.LogError($"[StockfishService] {LastError}");
        }

        return signal;
    }

    static async Task<string> WaitForResultWithTimeout(Task<string> task, string label, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(timeout, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        if (completed != task)
        {
            Debug.LogError($"[StockfishService] Timeout waiting for {label}.");
            return null;
        }

        return await task;
    }

    void SetState(EngineState state) => CurrentState = state;

    void SetUnavailable(string reason)
    {
        LastError = reason;
        SetState(EngineState.Unavailable);
        Debug.LogError($"[StockfishService] Initialization failed. Engine unavailable. Reason: {reason}");
    }

    void EnsureLoadingOverlay()
    {
        Canvas canvas = ChessMasterCanvas.GetOrCreateCanvas();
        Transform existing = canvas.transform.Find(LoadingOverlayRootName);
        GameObject root = existing != null ? existing.gameObject : new GameObject(LoadingOverlayRootName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one; rootRect.offsetMin = Vector2.zero; rootRect.offsetMax = Vector2.zero;
        Image bg = root.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        bg.raycastTarget = true;

        overlayCanvasGroup = root.GetComponent<CanvasGroup>();
        overlayCanvasGroup.interactable = true;
        overlayCanvasGroup.blocksRaycasts = true;

        Transform label = root.transform.Find("Message");
        if (label == null)
        {
            GameObject message = new("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            message.transform.SetParent(root.transform, false);
            RectTransform textRect = message.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(900f, 220f);
            textRect.anchoredPosition = Vector2.zero;
        }

        overlayText = root.GetComponentInChildren<TextMeshProUGUI>(true);
        ChessUITheme.ApplyTextStyle(overlayText, 42f, Color.white, TextAlignmentOptions.Center, true);
        root.SetActive(false);
    }

    void UpdateLoadingOverlay()
    {
        if (overlayCanvasGroup == null || overlayText == null) return;

        bool showLoading = CurrentState is EngineState.Starting or EngineState.WaitingForUci or EngineState.WaitingForReady or EngineState.Retrying;
        bool showError = CurrentState == EngineState.Unavailable;

        if (!showLoading && !showError)
        {
            if (overlayCanvasGroup.gameObject.activeSelf)
            {
                overlayCanvasGroup.gameObject.SetActive(false);
            }
            return;
        }

        if (!overlayCanvasGroup.gameObject.activeSelf)
        {
            overlayCanvasGroup.gameObject.SetActive(true);
        }

        overlayText.text = CurrentState switch
        {
            EngineState.WaitingForReady => "Waiting for Stockfish readyok...",
            EngineState.WaitingForUci => "Starting AI engine...",
            EngineState.Retrying => $"Retrying Stockfish startup... Attempt {CurrentRetryAttempt}/{MaxRetryAttempts}",
            EngineState.Unavailable => "AI engine failed to start. Check Stockfish executable path.",
            _ => "Starting AI engine..."
        };
    }

    string ResolveExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(executablePathOverride))
        {
            string overridePath = Path.IsPathRooted(executablePathOverride) ? executablePathOverride : Path.Combine(Application.dataPath, executablePathOverride);
            if (File.Exists(overridePath)) return overridePath;
        }

        string folderPath = Path.Combine(Application.dataPath, StockfishFolder);
        if (!Directory.Exists(folderPath)) return null;

        bool isWindows = Application.platform is RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer;
        bool isMacOrLinux = Application.platform is RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer or RuntimePlatform.LinuxEditor or RuntimePlatform.LinuxPlayer;

        string[] candidateNames = isWindows ? new[] { "stockfish-windows-x86-64-avx2.exe", "stockfish.exe" } : isMacOrLinux ? new[] { "stockfish-macos", "stockfish-linux", "stockfish" } : new[] { "stockfish" };
        foreach (string candidateName in candidateNames)
        {
            string candidatePath = Path.Combine(folderPath, candidateName);
            if (File.Exists(candidatePath)) return candidatePath;
        }

        foreach (string file in Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly))
        {
            string filename = Path.GetFileName(file);
            bool isWindowsExe = filename.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
            bool isNonWindowsStockfish = filename.Contains("stockfish", StringComparison.OrdinalIgnoreCase) && !isWindowsExe;
            if ((isWindows && isWindowsExe) || (isMacOrLinux && isNonWindowsStockfish)) return file;
        }

        return null;
    }
}
