using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChessPauseMenuUI : MonoBehaviour
{
    #region Variables
    GameObject overlay;
    RectTransform panelRect;
    RectTransform headerRect;
    RectTransform buttonsRect;
    VerticalLayoutGroup panelLayout;
    VerticalLayoutGroup buttonsLayout;
    TMP_Text statusText;
    TMP_Text hintText;
    TMP_Text titleText;
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
    bool hasLoggedLayoutMetrics;
    #endregion

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
        panelRect = ChessUIFactory.CreatePanel("PausePanel", overlay.transform, new Vector2(640f, 700f), new Vector2(0.5f, 0.5f));
        panelLayout = panelRect.gameObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(44, 44, 44, 44);
        panelLayout.spacing = 18f;
        panelLayout.childAlignment = TextAnchor.MiddleCenter;
        panelLayout.childControlHeight = false;
        panelLayout.childControlWidth = false;
        panelLayout.childForceExpandHeight = false;
        panelLayout.childForceExpandWidth = false;

        GameObject header = new("HeaderContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        header.transform.SetParent(panelRect, false);
        headerRect = header.GetComponent<RectTransform>();
        var headerLayout = header.GetComponent<VerticalLayoutGroup>();
        headerLayout.spacing = 6f;
        headerLayout.padding = new RectOffset(0, 0, 0, 0);
        headerLayout.childAlignment = TextAnchor.MiddleCenter;
        headerLayout.childControlHeight = false;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandHeight = false;
        headerLayout.childForceExpandWidth = false;
        var headerElement = header.GetComponent<LayoutElement>();
        headerElement.flexibleHeight = 0f;
        headerElement.preferredHeight = 120f;

        titleText = ChessUIFactory.CreateText("TitleText", headerRect, "PAUSED", ChessUITheme.TitleSize, TextAlignmentOptions.Center);
        titleText.color = ChessUITheme.GoldAccent;
        var titleElement = titleText.gameObject.AddComponent<LayoutElement>();
        titleElement.preferredHeight = 62f;
        titleElement.flexibleHeight = 0f;

        statusText = ChessUIFactory.CreateText("SubtitleText", headerRect, "Game paused", ChessUITheme.BodySize, TextAlignmentOptions.Center);
        var statusElement = statusText.gameObject.AddComponent<LayoutElement>();
        statusElement.preferredHeight = 30f;
        statusElement.flexibleHeight = 0f;

        hintText = ChessUIFactory.CreateText("HintText", headerRect, "", 20f, TextAlignmentOptions.Center);
        hintText.color = ChessUITheme.MutedText;
        var hintElement = hintText.gameObject.AddComponent<LayoutElement>();
        hintElement.preferredHeight = 24f;
        hintElement.flexibleHeight = 0f;

        GameObject buttons = new("ButtonsContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        buttons.transform.SetParent(panelRect, false);
        buttonsRect = buttons.GetComponent<RectTransform>();
        buttonsLayout = buttons.GetComponent<VerticalLayoutGroup>();
        buttonsLayout.spacing = 10f;
        buttonsLayout.padding = new RectOffset(0, 0, 0, 0);
        buttonsLayout.childAlignment = TextAnchor.UpperCenter;
        buttonsLayout.childControlHeight = false;
        buttonsLayout.childControlWidth = true;
        buttonsLayout.childForceExpandHeight = false;
        buttonsLayout.childForceExpandWidth = false;
        var buttonsElement = buttons.GetComponent<LayoutElement>();
        buttonsElement.preferredWidth = 420f;
        buttonsElement.flexibleHeight = 0f;

        resumeButton = ChessUIFactory.CreateButton("ResumeButton", buttons.transform, "Resume", () => pauseManager.Resume());
        devButton = ChessUIFactory.CreateButton("DevButton", buttons.transform, "Dev Menu", () => sandbox?.SetOpenFromPauseMenu(true));
        debugButton = ChessUIFactory.CreateButton("DebugButton", buttons.transform, "Debug Menu", () => sandbox?.SetOpenFromPauseMenu(true));
        boardButton = ChessUIFactory.CreateButton("BoardButton", buttons.transform, "Board Presets", () => sandbox?.SetOpenFromPauseMenu(true));
        aiConsoleButton = ChessUIFactory.CreateButton("AiButton", buttons.transform, "AI / Stockfish Console", () => aiConsole.SetVisible(true));
        restartButton = ChessUIFactory.CreateButton("RestartButton", buttons.transform, "Restart / New Game", () => { ChessBoard.Instance?.RestartMatch(); pauseManager.ResetPauseState(); });
        resignButton = ChessUIFactory.CreateButton("ResignButton", buttons.transform, "Resign", () => resignUi.OpenConfirmFromPauseMenu());

        overlay.SetActive(false);
        hasLoggedLayoutMetrics = false;
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

        if (!hasLoggedLayoutMetrics)
        {
            Canvas.ForceUpdateCanvases();
            float panelHeight = panelRect != null ? panelRect.rect.height : 0f;
            float headerHeight = headerRect != null ? headerRect.rect.height : 0f;
            float buttonHeight = buttonsRect != null ? buttonsRect.rect.height : 0f;
            float titleHeight = titleText != null ? ((RectTransform)titleText.transform).rect.height : 0f;
            float subtitleHeight = statusText != null ? ((RectTransform)statusText.transform).rect.height : 0f;
            Debug.Log($"[ChessPauseMenuUI] Panel={panelHeight:0.#}h Header={headerHeight:0.#}h Buttons={buttonHeight:0.#}h Title={titleHeight:0.#}h Subtitle={subtitleHeight:0.#}h");
            Debug.Log($"[ChessPauseMenuUI] Layout spacing: panelSpacing={panelLayout.spacing:0.#} buttonSpacing={buttonsLayout.spacing:0.#} paddingTop={panelLayout.padding.top}");
            hasLoggedLayoutMetrics = true;
        }
    }
    #endregion
}
