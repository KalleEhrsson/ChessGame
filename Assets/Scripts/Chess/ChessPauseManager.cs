using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ChessPauseManager : MonoBehaviour
{
    public delegate void PauseStateChangedHandler(bool isPauseRequested, bool isPaused);
    public event PauseStateChangedHandler PauseStateChanged;

    public static ChessPauseManager Instance { get; private set; }

    public static ChessPauseManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessPauseManager existing = FindFirstObjectByType<ChessPauseManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = new("ChessPauseManager");
        return host.AddComponent<ChessPauseManager>();
    }

    [SerializeField] bool enablePauseDebugLogs;

    int activeRoundActions;
    bool isPauseRequested;
    bool isPaused;

    ChessPauseMenuUI pauseMenuUi;
    ChessDevSandboxController sandbox;
    ChessResignUiController resignUi;
    ChessWinScreenUI winScreen;

    public bool IsPauseRequested => isPauseRequested;
    public bool IsPaused => isPaused;
    public bool IsPausePending => isPauseRequested && !isPaused;
    public bool CanPauseImmediately => activeRoundActions <= 0;
    public bool ShouldUnlockCursor => IsPauseRequested || IsBlockingOverlayOpen();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[ChessPauseManager] Duplicate manager disabled: {name} ({GetInstanceID()})", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    void OnEnable()
    {
        RefreshCursorState();
    }

    void Update()
    {
        HandlePauseInput();
        RefreshCursorState();
    }

    void HandlePauseInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (!keyboard.pKey.wasPressedThisFrame && !keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        TogglePauseRequest();
    }

    public void TogglePauseRequest()
    {
        ResolveUiDependencies();

        if (winScreen != null && winScreen.IsVisible)
        {
            return;
        }

        if (!isPauseRequested)
        {
            RequestPause();
            return;
        }

        if (resignUi != null && resignUi.IsConfirmOpen())
        {
            resignUi.CloseConfirmFromPauseMenu();
            return;
        }

        if (sandbox != null && sandbox.IsOpen)
        {
            sandbox.SetOpenFromPauseMenu(false);
            return;
        }

        RequestResume();
    }

    public void RequestPause()
    {
        if (isPauseRequested || isPaused)
        {
            return;
        }

        bool previousRequested = isPauseRequested;
        bool previousPaused = isPaused;

        isPauseRequested = true;
        TryEnterPausedState();

        NotifyIfChanged(previousRequested, previousPaused);
    }

    public void RequestResume()
    {
        if (!isPauseRequested && !isPaused)
        {
            return;
        }

        bool previousRequested = isPauseRequested;
        bool previousPaused = isPaused;

        isPauseRequested = false;
        isPaused = false;

        NotifyIfChanged(previousRequested, previousPaused);
    }

    public void NotifyRoundActionStarted()
    {
        activeRoundActions++;
    }

    public void NotifyRoundActionFinished()
    {
        activeRoundActions = Mathf.Max(0, activeRoundActions - 1);

        bool previousRequested = isPauseRequested;
        bool previousPaused = isPaused;

        TryEnterPausedState();
        NotifyIfChanged(previousRequested, previousPaused);
    }

    public void ResetPauseState()
    {
        bool previousRequested = isPauseRequested;
        bool previousPaused = isPaused;

        activeRoundActions = 0;
        isPauseRequested = false;
        isPaused = false;

        NotifyIfChanged(previousRequested, previousPaused);
    }

    void TryEnterPausedState()
    {
        if (!isPauseRequested)
        {
            isPaused = false;
            return;
        }

        if (CanPauseImmediately)
        {
            isPaused = true;
        }
    }

    void NotifyIfChanged(bool previousRequested, bool previousPaused)
    {
        if (previousRequested == isPauseRequested && previousPaused == isPaused)
        {
            return;
        }

        Time.timeScale = isPaused ? 0f : 1f;

        if (enablePauseDebugLogs)
        {
            Debug.Log($"[ChessPauseManager] {name} ({GetInstanceID()}) -> requested={isPauseRequested} paused={isPaused} pending={IsPausePending}", this);
        }

        PauseStateChanged?.Invoke(isPauseRequested, isPaused);
    }

    void RefreshCursorState()
    {
        ResolveUiDependencies();
        ChessCursorStateCoordinator.SetPauseCursorOverride(ShouldUnlockCursor);
    }

    bool IsBlockingOverlayOpen()
    {
        ResolveUiDependencies();
        return (pauseMenuUi != null && pauseMenuUi.IsVisible)
            || (sandbox != null && sandbox.IsOpen)
            || (resignUi != null && resignUi.IsConfirmOpen())
            || (winScreen != null && winScreen.IsVisible);
    }

    void ResolveUiDependencies()
    {
        pauseMenuUi ??= ChessPauseMenuUI.GetOrCreate();
        sandbox ??= ChessDevSandboxController.Instance;
        resignUi ??= ChessResignUiController.GetOrCreate();
        winScreen ??= ChessWinScreenUI.GetOrCreate();
    }
}
