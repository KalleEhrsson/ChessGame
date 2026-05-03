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

        ChessPauseManager existing = FindFirstObjectByType<ChessPauseManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject host = new("ChessPauseManager");
        Instance = host.AddComponent<ChessPauseManager>();
        return Instance;
    }

    #endregion

    #region Variables

    int activeRoundActions;
    bool isPauseRequested;
    bool isPaused;
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
        DontDestroyOnLoad(gameObject);
    }

    #endregion

    void Update()
    {
        HandleEscapeInput();
        RefreshCursorState();
    }

    #region API

    public void RequestPause()
    {
        if (!isPauseRequested)
        {
            Debug.Log("[ChessPauseManager] Pause requested", this);
        }

        isPauseRequested = true;
        TryEnterPausedState();
        Debug.Log("[ChessPauseManager] Pause menu shown", this);
    }

    public void Resume()
    {
        isPauseRequested = false;
        isPaused = false;
        Debug.Log("[ChessPauseManager] Resumed", this);
    }

    public void TogglePauseRequest()
    {
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
    }

    #endregion

    void HandleEscapeInput()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        Debug.Log("[ChessPauseManager] Escape pressed", this);
        TogglePauseRequest();
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

    void ResolveUiDependencies()
    {
        pauseMenuUi ??= FindFirstObjectByType<ChessPauseMenuUI>();
        sandbox ??= ChessDevSandboxController.Instance;
        resignUi ??= ChessResignUiController.GetOrCreate();
        winScreen ??= ChessWinScreenUI.GetOrCreate();
    }
}

