using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChessPauseMenuUI : MonoBehaviour
{
    #region Variables
    GameObject overlay;
    TMP_Text statusText;
    TMP_Text hintText;
    Button resumeButton;
    Button devButton;
    Button debugButton;
    Button boardButton;
    Button aiConsoleButton;
    Button restartButton;
    Button resignButton;

    ChessPauseManager pauseManager;
    ChessAiRoundConsole aiConsole;
    ChessDevSandboxController sandbox;
    ChessResignUiController resignUi;
    #endregion

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRuntimeInstance()
    {
        if (FindFirstObjectByType<ChessPauseMenuUI>() != null) return;
        GameObject host = new("ChessPauseMenuUI");
        DontDestroyOnLoad(host);
        host.AddComponent<ChessPauseMenuUI>();
    }

    void Awake()
    {
        pauseManager = ChessPauseManager.GetOrCreate();
        aiConsole = ChessAiRoundConsole.GetOrCreate();
        sandbox = ChessDevSandboxController.Instance;
        resignUi = ChessResignUiController.GetOrCreate();
        EnsureUi();
    }

    void Update() => Refresh();

    #region UI
    void EnsureUi()
    {
        Transform root = ChessMasterCanvas.GetOrCreateOverlayRoot("PauseMenuRoot");
        overlay = ChessUIFactory.CreateFullscreenOverlay("PauseOverlay", root);
        RectTransform panel = ChessUIFactory.CreatePanel("PausePanel", overlay.transform, new Vector2(640f, 700f), new Vector2(0.5f, 0.5f));
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 28, 28);
        layout.spacing = 10f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;

        var title = ChessUIFactory.CreateText("Title", panel, "PAUSED", ChessUITheme.TitleSize, TextAlignmentOptions.Center);
        title.color = ChessUITheme.GoldAccent;
        statusText = ChessUIFactory.CreateText("Status", panel, "Game paused", ChessUITheme.BodySize, TextAlignmentOptions.Center);
        hintText = ChessUIFactory.CreateText("Hint", panel, "", 20f, TextAlignmentOptions.Center);
        hintText.color = ChessUITheme.MutedText;

        GameObject buttons = new("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
        buttons.transform.SetParent(panel, false);
        var bl = buttons.GetComponent<VerticalLayoutGroup>(); bl.spacing = 8f; bl.childControlHeight = false; bl.childControlWidth = true;

        resumeButton = ChessUIFactory.CreateButton("ResumeButton", buttons.transform, "Resume", () => pauseManager.Resume());
        devButton = ChessUIFactory.CreateButton("DevButton", buttons.transform, "Dev Menu", () => sandbox?.SetOpenFromPauseMenu(true));
        debugButton = ChessUIFactory.CreateButton("DebugButton", buttons.transform, "Debug Menu", () => sandbox?.SetOpenFromPauseMenu(true));
        boardButton = ChessUIFactory.CreateButton("BoardButton", buttons.transform, "Board Presets", () => sandbox?.SetOpenFromPauseMenu(true));
        aiConsoleButton = ChessUIFactory.CreateButton("AiButton", buttons.transform, "AI / Stockfish Console", () => aiConsole.SetVisible(true));
        restartButton = ChessUIFactory.CreateButton("RestartButton", buttons.transform, "Restart / New Game", () => { ChessBoard.Instance?.RestartMatch(); pauseManager.ResetPauseState(); });
        resignButton = ChessUIFactory.CreateButton("ResignButton", buttons.transform, "Resign", () => resignUi.OpenConfirmFromPauseMenu());

        overlay.SetActive(false);
    }

    void Refresh()
    {
        if (overlay == null || pauseManager == null) return;
        bool show = pauseManager.IsPauseRequested;
        if (overlay.activeSelf != show) overlay.SetActive(show);
        if (!show) return;

        bool fullyPaused = pauseManager.IsPaused;
        statusText.text = fullyPaused ? "Game paused" : "Finishing current move...";
        hintText.text = fullyPaused ? "Dev tools are available while paused." : "Waiting for AI...";
        (resumeButton.GetComponentInChildren<TextMeshProUGUI>()).text = fullyPaused ? "Resume" : "Cancel Pause";

        devButton.interactable = fullyPaused;
        debugButton.interactable = fullyPaused;
        boardButton.interactable = fullyPaused;
        aiConsoleButton.interactable = fullyPaused;
        restartButton.interactable = fullyPaused;
        resignButton.interactable = fullyPaused;
    }
    #endregion
}
