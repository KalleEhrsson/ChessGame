using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChessPauseMenuUI : MonoBehaviour
{
    #region Variables
    [SerializeField] bool pauseDebugMarkerEnabled = true;

    GameObject pauseMenuRoot;
    GameObject dimBackground;
    CanvasGroup rootCanvasGroup;
    CanvasGroup dimCanvasGroup;
    RectTransform panelRect;
    CanvasGroup panelCanvasGroup;
    TMP_Text debugMarkerText;

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

    public static ChessPauseMenuUI GetOrCreate()
    {
        ChessPauseMenuUI existing = FindFirstObjectByType<ChessPauseMenuUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            return existing;
        }

        GameObject host = new("ChessPauseMenuUI");
        return host.AddComponent<ChessPauseMenuUI>();
    }

    void Awake()
    {
        pauseManager = ChessPauseManager.GetOrCreate();
        aiConsole = ChessAiRoundConsole.GetOrCreate();
        sandbox = ChessDevSandboxController.Instance;
        resignUi = ChessResignUiController.GetOrCreate();
        EnsureUi();
    }

    void OnEnable()
    {
        pauseManager ??= ChessPauseManager.GetOrCreate();
        pauseManager.PauseStateChanged -= OnPauseStateChanged;
        pauseManager.PauseStateChanged += OnPauseStateChanged;
        SetPauseVisualState(pauseManager.IsPaused);
    }

    void OnDisable()
    {
        if (pauseManager != null)
        {
            pauseManager.PauseStateChanged -= OnPauseStateChanged;
        }
    }

    void Update() => Refresh();

    #region UI
    void EnsureUi()
    {
        Canvas canvas = ChessMasterCanvas.GetOrCreateCanvas();
        Transform overlayRoot = FindPauseMenuRootFromCanvas(canvas.transform);
        if (overlayRoot == null)
        {
            overlayRoot = ChessMasterCanvas.GetOrCreateOverlayRoot("PauseMenuRoot");
            Debug.Log("[ChessPauseMenuUI] PauseMenuRoot auto-created under ChessMasterCanvas", this);
        }
        else
        {
            Debug.Log("[ChessPauseMenuUI] PauseMenuRoot auto-found under ChessMasterCanvas", this);
        }
        pauseMenuRoot = overlayRoot.gameObject;
        rootCanvasGroup = pauseMenuRoot.GetComponent<CanvasGroup>() ?? pauseMenuRoot.AddComponent<CanvasGroup>();

        ClearChildren(overlayRoot);
        ConfigureFullscreenRect((RectTransform)overlayRoot);

        dimBackground = new GameObject("DimBackground", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        dimBackground.transform.SetParent(overlayRoot, false);
        RectTransform dimRect = dimBackground.GetComponent<RectTransform>();
        ConfigureFullscreenRect(dimRect);
        dimBackground.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.07f, 0.78f);
        dimCanvasGroup = dimBackground.GetComponent<CanvasGroup>();

        panelRect = ChessUIFactory.CreatePanel("PausePanel", overlayRoot, new Vector2(560f, 620f), new Vector2(0.5f, 0.5f));
        ConfigurePanelRect(panelRect);
        panelCanvasGroup = panelRect.gameObject.GetComponent<CanvasGroup>() ?? panelRect.gameObject.AddComponent<CanvasGroup>();

        CreateDebugMarker(canvas.transform);

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

        resumeButton = CreateThemedButton("ResumeButton", buttonStack.transform, "Resume", () => pauseManager.RequestResume());
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

        SetDebugMarkerVisible(false);
        pauseMenuRoot.SetActive(false);
    }


    bool EnsureRuntimeReferences()
    {
        if (pauseMenuRoot == null || dimBackground == null || panelRect == null)
        {
            EnsureUi();
        }

        if (pauseMenuRoot == null || dimBackground == null || panelRect == null)
        {
            return false;
        }

        rootCanvasGroup = pauseMenuRoot.GetComponent<CanvasGroup>() ?? pauseMenuRoot.AddComponent<CanvasGroup>();
        dimCanvasGroup = dimBackground.GetComponent<CanvasGroup>() ?? dimBackground.AddComponent<CanvasGroup>();

        GameObject panel = panelRect.gameObject;
        panelCanvasGroup = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();

        return rootCanvasGroup != null && dimCanvasGroup != null && panelCanvasGroup != null;
    }

    void Refresh()
    {
        if (pauseMenuRoot == null || pauseManager == null || !EnsureRuntimeReferences())
        {
            return;
        }

        bool show = pauseManager.IsPaused;
        if (show && !pauseMenuRoot.activeSelf)
        {
            Show();
            hasLoggedThisVisibleCycle = false;
        }
        else if (!show && pauseMenuRoot.activeSelf)
        {
            Hide();
            hasLoggedThisVisibleCycle = false;
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
            LogShowDiagnostics();
            LogLayoutMetrics();
            hasLoggedThisVisibleCycle = true;
        }
    }
    #endregion

    void OnPauseStateChanged(bool isPauseRequested, bool isPaused)
    {
        Debug.Log($"[ChessPauseMenuUI] Pause state changed requested={isPauseRequested} paused={isPaused}", this);
        SetPauseVisualState(isPaused);
    }

    #region ShowHide
    void SetPauseVisualState(bool visible)
    {
        if (!EnsureRuntimeReferences())
        {
            return;
        }

        if (visible)
        {
            if (!pauseMenuRoot.activeSelf)
            {
                Show();
            }
        }
        else if (pauseMenuRoot.activeSelf)
        {
            Hide();
        }
    }

    void Show()
    {
        if (!EnsureRuntimeReferences())
        {
            return;
        }

        Debug.Log("[ChessPauseMenuUI] Show called", this);
        ForceVisibleSafeState();
        SetDebugMarkerVisible(pauseDebugMarkerEnabled);
        PlayShowAnimation();
    }

    void Hide()
    {
        Debug.Log("[ChessPauseMenuUI] Hide called", this);
        SetDebugMarkerVisible(false);
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }
        pauseMenuRoot.SetActive(false);
    }

    void ForceVisibleSafeState()
    {
        Canvas masterCanvas = ChessMasterCanvas.GetOrCreateCanvas();
        masterCanvas.gameObject.SetActive(true);
        masterCanvas.enabled = true;
        masterCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        masterCanvas.sortingOrder = Mathf.Max(masterCanvas.sortingOrder, 5000);

        ConfigureFullscreenRect((RectTransform)pauseMenuRoot.transform);
        pauseMenuRoot.SetActive(true);
        pauseMenuRoot.transform.localScale = Vector3.one;
        rootCanvasGroup.alpha = 1f;
        rootCanvasGroup.interactable = true;
        rootCanvasGroup.blocksRaycasts = true;

        dimBackground.SetActive(true);
        dimCanvasGroup.alpha = 1f;
        dimCanvasGroup.interactable = true;
        dimCanvasGroup.blocksRaycasts = true;

        panelRect.gameObject.SetActive(true);
        ConfigurePanelRect(panelRect);
        panelRect.localScale = Vector3.one;
        panelCanvasGroup.alpha = 1f;
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;
    }

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
        dimCanvasGroup.alpha = 0.85f;
        panelCanvasGroup.alpha = 0.5f;
        panelRect.localScale = new Vector3(0.98f, 0.98f, 1f);

        float duration = Mathf.Max(0.01f, ChessUITheme.FadeDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            panelCanvasGroup.alpha = Mathf.Lerp(0.5f, 1f, k);
            panelRect.localScale = Vector3.Lerp(new Vector3(0.98f, 0.98f, 1f), Vector3.one, k);
            yield return null;
        }

        // final visibility clamp
        rootCanvasGroup.alpha = 1f;
        panelCanvasGroup.alpha = 1f;
        panelRect.localScale = Vector3.one;
        pauseMenuRoot.SetActive(true);
        showRoutine = null;
    }
    #endregion

    #region Helpers
    void CreateDebugMarker(Transform canvasTransform)
    {
        GameObject marker = new("PauseDebugOverlayRoot", typeof(RectTransform), typeof(Image));
        marker.transform.SetParent(canvasTransform, false);
        RectTransform rect = marker.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20f, -20f);
        rect.sizeDelta = new Vector2(460f, 60f);
        marker.GetComponent<Image>().color = new Color(1f, 0f, 0.25f, 0.92f);

        debugMarkerText = ChessUIFactory.CreateText("PauseDebugText", marker.transform, "PAUSE UI DEBUG VISIBLE", 30f, TextAlignmentOptions.Center);
        RectTransform textRect = (RectTransform)debugMarkerText.transform;
        ConfigureFullscreenRect(textRect);
        debugMarkerText.color = Color.white;
        marker.SetActive(false);
    }

    void SetDebugMarkerVisible(bool visible)
    {
        if (debugMarkerText != null)
        {
            debugMarkerText.transform.parent.gameObject.SetActive(visible);
        }
    }

    void LogShowDiagnostics()
    {
        if (!EnsureRuntimeReferences())
        {
            return;
        }

        Canvas canvas = ChessMasterCanvas.GetOrCreateCanvas();
        RectTransform rootRect = pauseMenuRoot != null ? pauseMenuRoot.GetComponent<RectTransform>() : null;
        RectTransform panel = panelRect;

        Debug.Log($"[ChessPauseMenuUI] Root activeSelf={pauseMenuRoot.activeSelf}", this);
        Debug.Log($"[ChessPauseMenuUI] Root activeInHierarchy={pauseMenuRoot.activeInHierarchy}", this);
        Debug.Log($"[ChessPauseMenuUI] Root canvas group alpha={rootCanvasGroup.alpha:0.###}", this);
        Debug.Log($"[ChessPauseMenuUI] Root canvas group interactable={rootCanvasGroup.interactable}", this);
        Debug.Log($"[ChessPauseMenuUI] Root canvas group blocksRaycasts={rootCanvasGroup.blocksRaycasts}", this);
        Debug.Log($"[ChessPauseMenuUI] Root localScale={pauseMenuRoot.transform.localScale}", this);
        Debug.Log($"[ChessPauseMenuUI] Root anchoredPosition={rootRect.anchoredPosition}", this);
        Debug.Log($"[ChessPauseMenuUI] Root sizeDelta={rootRect.sizeDelta}", this);
        Debug.Log($"[ChessPauseMenuUI] Panel activeInHierarchy={panel.gameObject.activeInHierarchy}", this);
        Debug.Log($"[ChessPauseMenuUI] Panel localScale={panel.localScale}", this);
        Debug.Log($"[ChessPauseMenuUI] Panel anchoredPosition={panel.anchoredPosition}", this);
        Debug.Log($"[ChessPauseMenuUI] Panel sizeDelta={panel.sizeDelta}", this);
        Debug.Log($"[ChessPauseMenuUI] Master canvas activeInHierarchy={canvas.gameObject.activeInHierarchy}", this);
        Debug.Log($"[ChessPauseMenuUI] Master canvas renderMode={canvas.renderMode}", this);
        Debug.Log($"[ChessPauseMenuUI] Master canvas sortingOrder={canvas.sortingOrder}", this);
        Debug.Log($"[ChessPauseMenuUI] Master canvas enabled={canvas.enabled}", this);
    }

    string ResolvePendingHint()
    {
        if (pauseManager.CanPauseImmediately)
        {
            return "Finishing current move...";
        }

        return "Waiting for AI...";
    }

    static void ConfigurePanelRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        if (rect.sizeDelta.x < 1f || rect.sizeDelta.y < 1f)
        {
            rect.sizeDelta = new Vector2(560f, 620f);
        }
    }

    static void ConfigureFullscreenRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    static Transform FindPauseMenuRootFromCanvas(Transform canvasTransform)
    {
        if (canvasTransform == null)
        {
            return null;
        }

        Transform direct = canvasTransform.Find("PauseMenuRoot");
        if (direct != null)
        {
            return direct;
        }

        Transform overlayRoot = canvasTransform.Find("OverlayRoot");
        return overlayRoot != null ? overlayRoot.Find("PauseMenuRoot") : null;
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
            Color color = text.color;
            color.a = Mathf.Max(color.a, 1f);
            text.color = color;
        }

        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            Color color = buttonImage.color;
            color.a = Mathf.Max(0.9f, color.a);
            buttonImage.color = color;
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
