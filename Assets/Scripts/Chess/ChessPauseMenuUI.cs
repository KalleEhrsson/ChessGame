using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChessPauseMenuUI : MonoBehaviour
{
    #region Variables
    GameObject pauseMenuRoot;
    GameObject dimBackground;
    CanvasGroup dimCanvasGroup;
    RectTransform panelRect;
    CanvasGroup panelCanvasGroup;
    RectTransform contentRootRect;
    RectTransform headerRect;
    RectTransform buttonStackRect;
    VerticalLayoutGroup contentLayout;
    VerticalLayoutGroup buttonStackLayout;
    TMP_Text titleText;
    TMP_Text statusText;
    TMP_Text footerHintText;

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

    Coroutine showRoutine;
    bool hasLoggedThisVisibleCycle;
    #endregion

    public bool IsVisible => pauseMenuRoot != null && pauseMenuRoot.activeSelf;

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
        Transform overlayRoot = ChessMasterCanvas.GetOrCreateOverlayRoot("PauseMenuRoot");
        pauseMenuRoot = overlayRoot.gameObject;

        ClearChildren(overlayRoot);
        ConfigureFullscreenRect((RectTransform)overlayRoot);

        dimBackground = new GameObject("DimBackground", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        dimBackground.transform.SetParent(overlayRoot, false);
        RectTransform dimRect = dimBackground.GetComponent<RectTransform>();
        ConfigureFullscreenRect(dimRect);
        dimBackground.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.07f, 0.78f);
        dimCanvasGroup = dimBackground.GetComponent<CanvasGroup>();

        panelRect = ChessUIFactory.CreatePanel("PausePanel", overlayRoot, new Vector2(580f, 620f), new Vector2(0.5f, 0.5f));
        panelCanvasGroup = panelRect.gameObject.AddComponent<CanvasGroup>();

        Image accent = new GameObject("AccentTopLine", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        accent.transform.SetParent(panelRect, false);
        RectTransform accentRect = (RectTransform)accent.transform;
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.sizeDelta = new Vector2(0f, 3f);
        accentRect.anchoredPosition = Vector2.zero;
        accent.color = ChessUITheme.GoldAccent;

        GameObject contentRoot = new("ContentRoot", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentRoot.transform.SetParent(panelRect, false);
        contentRootRect = contentRoot.GetComponent<RectTransform>();
        contentRootRect.anchorMin = Vector2.zero;
        contentRootRect.anchorMax = Vector2.one;
        contentRootRect.offsetMin = new Vector2(36f, 32f);
        contentRootRect.offsetMax = new Vector2(-36f, -36f);
        contentLayout = contentRoot.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 22f;
        contentLayout.padding = new RectOffset(0, 0, 0, 0);
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = false;

        GameObject headerRoot = new("HeaderRoot", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        headerRoot.transform.SetParent(contentRoot.transform, false);
        headerRect = headerRoot.GetComponent<RectTransform>();
        var headerLayout = headerRoot.GetComponent<VerticalLayoutGroup>();
        headerLayout.spacing = 8f;
        headerLayout.childAlignment = TextAnchor.UpperCenter;
        headerLayout.childControlHeight = false;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandHeight = false;
        headerLayout.childForceExpandWidth = false;
        var headerLe = headerRoot.GetComponent<LayoutElement>();
        headerLe.preferredHeight = 116f;
        headerLe.flexibleHeight = 0f;

        titleText = ChessUIFactory.CreateText("TitleText", headerRoot.transform, "PAUSED", 54f, TextAlignmentOptions.Center);
        titleText.color = ChessUITheme.GoldAccent;
        var titleLe = titleText.gameObject.AddComponent<LayoutElement>();
        titleLe.preferredHeight = 62f;
        titleLe.flexibleHeight = 0f;

        statusText = ChessUIFactory.CreateText("StatusText", headerRoot.transform, "Game paused", 24f, TextAlignmentOptions.Center);
        statusText.color = ChessUITheme.MutedText;
        var statusLe = statusText.gameObject.AddComponent<LayoutElement>();
        statusLe.preferredHeight = 34f;
        statusLe.flexibleHeight = 0f;

        GameObject buttonStack = new("ButtonStack", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        buttonStack.transform.SetParent(contentRoot.transform, false);
        buttonStackRect = buttonStack.GetComponent<RectTransform>();
        buttonStackLayout = buttonStack.GetComponent<VerticalLayoutGroup>();
        buttonStackLayout.spacing = 12f;
        buttonStackLayout.childAlignment = TextAnchor.UpperCenter;
        buttonStackLayout.childControlHeight = false;
        buttonStackLayout.childControlWidth = true;
        buttonStackLayout.childForceExpandHeight = false;
        buttonStackLayout.childForceExpandWidth = false;
        var stackLe = buttonStack.GetComponent<LayoutElement>();
        stackLe.preferredWidth = 432f;
        stackLe.flexibleHeight = 0f;

        resumeButton = CreateThemedButton("ResumeButton", buttonStack.transform, "Resume", () => pauseManager.Resume());
        devButton = CreateThemedButton("DevMenuButton", buttonStack.transform, "Dev Menu", () => sandbox?.SetOpenFromPauseMenu(true));
        debugButton = CreateThemedButton("DebugMenuButton", buttonStack.transform, "Debug Menu", () => sandbox?.SetOpenFromPauseMenu(true));
        boardButton = CreateThemedButton("BoardPresetsButton", buttonStack.transform, "Board Presets", () => sandbox?.SetOpenFromPauseMenu(true));
        aiConsoleButton = CreateThemedButton("StockfishConsoleButton", buttonStack.transform, "Stockfish Console", () => aiConsole.SetVisible(true));
        restartButton = CreateThemedButton("RestartButton", buttonStack.transform, "Restart / New Game", () => { ChessBoard.Instance?.RestartMatch(); pauseManager.ResetPauseState(); });
        resignButton = CreateThemedButton("ResignButton", buttonStack.transform, "Resign", () => resignUi.OpenConfirmFromPauseMenu());

        footerHintText = ChessUIFactory.CreateText("FooterHintText", contentRoot.transform, string.Empty, 18f, TextAlignmentOptions.Center);
        footerHintText.color = ChessUITheme.MutedText;
        var footerLe = footerHintText.gameObject.AddComponent<LayoutElement>();
        footerLe.preferredHeight = 28f;
        footerLe.flexibleHeight = 0f;

        pauseMenuRoot.SetActive(false);
    }

    void Refresh()
    {
        if (pauseMenuRoot == null || pauseManager == null)
        {
            return;
        }

        bool show = pauseManager.IsPauseRequested;
        if (show && !pauseMenuRoot.activeSelf)
        {
            pauseMenuRoot.SetActive(true);
            PlayShowAnimation();
            hasLoggedThisVisibleCycle = false;
        }
        else if (!show && pauseMenuRoot.activeSelf)
        {
            pauseMenuRoot.SetActive(false);
            hasLoggedThisVisibleCycle = false;
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }
        }

        if (!show)
        {
            return;
        }

        bool fullyPaused = pauseManager.IsPaused;
        statusText.text = fullyPaused ? "Game paused" : "Waiting for safe pause...";
        footerHintText.text = fullyPaused ? "Gameplay paused. Open tools or resume." : ResolvePendingHint();
        SetButtonLabel(resumeButton, fullyPaused ? "Resume" : "Cancel Pause");

        bool hasSandbox = sandbox != null;
        SetButtonVisibility(devButton, hasSandbox);
        SetButtonVisibility(debugButton, hasSandbox);
        SetButtonVisibility(boardButton, hasSandbox);
        SetButtonVisibility(aiConsoleButton, aiConsole != null);
        SetButtonVisibility(restartButton, ChessBoard.Instance != null);
        SetButtonVisibility(resignButton, resignUi != null);

        bool unsafeEnabled = fullyPaused;
        devButton.interactable = unsafeEnabled;
        debugButton.interactable = unsafeEnabled;
        boardButton.interactable = unsafeEnabled;
        aiConsoleButton.interactable = unsafeEnabled;
        restartButton.interactable = unsafeEnabled;
        resignButton.interactable = unsafeEnabled;
        resumeButton.interactable = true;

        if (!hasLoggedThisVisibleCycle)
        {
            LogLayoutMetrics();
            hasLoggedThisVisibleCycle = true;
        }
    }
    #endregion

    #region Helpers
    void PlayShowAnimation()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
        }

        showRoutine = StartCoroutine(AnimateShow());
    }

    IEnumerator AnimateShow()
    {
        panelRect.localScale = new Vector3(0.96f, 0.96f, 1f);
        panelCanvasGroup.alpha = 0f;
        dimCanvasGroup.alpha = 0f;

        float duration = ChessUITheme.FadeDuration;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            dimCanvasGroup.alpha = Mathf.Lerp(0f, 1f, k);
            panelCanvasGroup.alpha = k;
            panelRect.localScale = Vector3.Lerp(new Vector3(0.96f, 0.96f, 1f), Vector3.one, k);
            yield return null;
        }

        dimCanvasGroup.alpha = 1f;
        panelCanvasGroup.alpha = 1f;
        panelRect.localScale = Vector3.one;
        showRoutine = null;
    }

    string ResolvePendingHint()
    {
        if (pauseManager.CanPauseImmediately)
        {
            return "Finishing current move...";
        }

        return "Waiting for AI...";
    }

    static void ConfigureFullscreenRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    static Button CreateThemedButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        Button button = ChessUIFactory.CreateButton(name, parent, label, onClick);
        var le = button.GetComponent<LayoutElement>();
        le.preferredHeight = 56f;
        le.flexibleHeight = 0f;

        TMP_Text text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.Center;
        }

        return button;
    }

    static void SetButtonLabel(Button button, string label)
    {
        TMP_Text text = button != null ? button.GetComponentInChildren<TextMeshProUGUI>() : null;
        if (text != null)
        {
            text.text = label;
        }
    }

    static void SetButtonVisibility(Button button, bool visible)
    {
        if (button != null)
        {
            button.gameObject.SetActive(visible);
        }
    }

    void LogLayoutMetrics()
    {
        Canvas.ForceUpdateCanvases();
        List<Button> buttons = new() { resumeButton, devButton, debugButton, boardButton, aiConsoleButton, restartButton, resignButton };
        int visibleButtons = 0;
        foreach (Button button in buttons)
        {
            if (button != null && button.gameObject.activeInHierarchy)
            {
                visibleButtons++;
            }
        }

        float panelW = panelRect.rect.width;
        float panelH = panelRect.rect.height;
        float headerH = headerRect.rect.height;
        float stackH = buttonStackRect.rect.height;
        Debug.Log($"[ChessPauseMenuUI] Rebuilt layout: buttons={visibleButtons} panel={panelW:0.#}x{panelH:0.#} header={headerH:0.#} stack={stackH:0.#} scroll=false");
    }
    #endregion
}
