using UnityEngine;

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

    #endregion

    #region Properties

    public bool IsPauseRequested => isPauseRequested;
    public bool IsPaused => isPaused;
    public bool IsPausePending => isPauseRequested && !isPaused;
    public bool CanPauseImmediately => activeRoundActions <= 0;

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

    #region API

    public void RequestPause()
    {
        isPauseRequested = true;
        TryEnterPausedState();
    }

    public void Resume()
    {
        isPauseRequested = false;
        isPaused = false;
    }

    public void TogglePauseRequest()
    {
        if (isPauseRequested)
        {
            Resume();
            return;
        }

        RequestPause();
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
}
