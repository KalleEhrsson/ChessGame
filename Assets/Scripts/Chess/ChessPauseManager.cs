using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ChessPauseManager : MonoBehaviour
{
    #region Singleton

    public static ChessPauseManager Instance { get; private set; }

    public static ChessPauseManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessPauseManager existing = FindExisting();
        if (existing != null)
        {
            Instance = existing;
            Debug.Log("[ChessRuntimeBootstrap] Reused existing instance: ChessPauseManager");
            return Instance;
        }

        GameObject host = new("ChessPauseManager");
        Instance = host.AddComponent<ChessPauseManager>();
        // Debug.Log("[ChessRuntimeBootstrap] Created fallback instance: ChessPauseManager");
        return Instance;
    }

    #endregion

    #region Variables

    int activeRoundActions;
    [SerializeField] Key pauseKey = Key.P;
    [SerializeField] bool verbosePauseLogs = true;
    bool isPauseRequested;
    bool isPaused;
    bool lastPPressedThisFrame;
    ChessPauseMenuUI pauseMenuUi;
    ChessDevSandboxController sandbox;
    ChessResignUiController resignUi;
    ChessWinScreenUI winScreen;

    #endregion

    #region Properties

    public bool IsPauseRequested => isPauseRequested;
    public bool IsPaused => isPaused;
    public bool IsPausePending => isPauseRequested && !isPaused;
    public bool CanPauseImmediately => activeRoundActions <= 0;
    public bool ShouldUnlockCursor => IsPauseRequested || IsBlockingOverlayOpen();

    #endregion

    #region Unity

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion

    void Update()
    {
        HandlePauseInput();
        RefreshCursorState();
    }

    #region API

    public void RequestPause()
    {
        Debug.Log("[ChessPauseManager] RequestPause called", this);
        if (!isPauseRequested)
        {
            Debug.Log("[ChessPauseManager] Pause requested", this);
        }

        isPauseRequested = true;
        TryEnterPausedState();
        LogState("Pause pending");
        if (isPaused)
        {
            Debug.Log("[ChessPauseManager] Fully paused", this);
            LogState("Fully paused");
        }
    }

    public void Resume()
    {
        Debug.Log("[ChessPauseManager] Resume called", this);
        isPauseRequested = false;
        isPaused = false;
        Debug.Log("[ChessPauseManager] Resumed", this);
        LogState("Resumed");
    }

    public void TogglePauseRequest()
    {
        Debug.Log("[ChessPauseManager] Toggle called while requested/paused/pending", this);
        LogState("Before toggle");
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

        Resume();
    }

    public void NotifyRoundActionStarted()
    {
        activeRoundActions++;
    }

    public void NotifyRoundActionFinished()
    {
        activeRoundActions = Mathf.Max(0, activeRoundActions - 1);
        TryEnterPausedState();
    }

    public void ResetPauseState()
    {
        activeRoundActions = 0;
        isPauseRequested = false;
        isPaused = false;
    }

    #endregion

    #region Helpers

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

        if (isPaused)
        {
            Debug.Log("[ChessPauseManager] Fully paused", this);
        }
    }

    #endregion

    void HandlePauseInput()
    {
        lastPPressedThisFrame = false;
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[pauseKey].wasPressedThisFrame)
        {
            lastPPressedThisFrame = true;
            if (verbosePauseLogs)
            {
                Debug.Log($"[ChessPauseInput] {pauseKey} pressed", this);
                Debug.Log("[ChessPauseInput] Calling pause toggle", this);
            }
            TogglePauseRequest();
        }

        if (verbosePauseLogs && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Debug.Log("[ChessPauseInput] Escape pressed, ignored for pause debug", this);
        }
    }

    void RefreshCursorState()
    {
        ResolveUiDependencies();

        if (ShouldUnlockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    bool IsBlockingOverlayOpen()
    {
        ResolveUiDependencies();
        return (pauseMenuUi != null && pauseMenuUi.IsVisible)
            || (sandbox != null && sandbox.IsOpen)
            || (resignUi != null && resignUi.IsConfirmOpen())
            || (winScreen != null && winScreen.IsVisible);
    }


    void LogState(string phase)
    {
        Debug.Log($"[ChessPauseManager] {phase} | IsPauseRequested={IsPauseRequested} IsPausePending={IsPausePending} IsPaused={IsPaused} CanPauseImmediately={CanPauseImmediately} ActiveRoundActions={activeRoundActions}", this);
    }

    public bool ConsumeLastPPressedThisFrame()
    {
        bool wasPressed = lastPPressedThisFrame;
        lastPPressedThisFrame = false;
        return wasPressed;
    }

    void ResolveUiDependencies()
    {
        pauseMenuUi ??= ChessPauseMenuUI.GetOrCreate();
        sandbox ??= ChessDevSandboxController.Instance;
        resignUi ??= ChessResignUiController.GetOrCreate();
        winScreen ??= ChessWinScreenUI.GetOrCreate();
    }

    static ChessPauseManager FindExisting()
    {
        ChessPauseManager[] items = FindObjectsByType<ChessPauseManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return items.Length > 0 ? items[0] : null;
    }
}
