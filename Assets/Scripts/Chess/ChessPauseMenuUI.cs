using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChessPauseMenuUI : MonoBehaviour
{
    static ChessPauseMenuUI activeInstance;

    [SerializeField] bool enablePauseDebugLogs;
    [SerializeField] GameObject pauseMenuRoot;

    CanvasGroup rootCanvasGroup;
    ChessPauseManager pauseManager;
    ChessAiRoundConsole aiConsole;
    ChessDevSandboxController sandbox;
    ChessResignUiController resignUi;

    Button resumeButton;
    Button devButton;
    Button debugButton;
    Button boardButton;
    Button aiConsoleButton;
    Button restartButton;
    Button mainMenuButton;
    Button quitButton;
    Button resignButton;
    TMP_Text statusText;
    TMP_Text footerHintText;

    enum RootResolution { Assigned, AutoFound, FallbackCreated }

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
        if (activeInstance != null && activeInstance != this)
        {
            Debug.LogWarning($"[ChessPauseMenuUI] Duplicate UI disabled: {name} ({GetInstanceID()}) existing={activeInstance.name} ({activeInstance.GetInstanceID()})", this);
            enabled = false;
            return;
        }

        activeInstance = this;
        pauseManager = ChessPauseManager.GetOrCreate();
        aiConsole = ChessAiRoundConsole.GetOrCreate();
        sandbox = ChessDevSandboxController.Instance;
        resignUi = ChessResignUiController.GetOrCreate();

        EnsureUiReferences();
        CacheOptionalControls();
    }

    void OnEnable()
    {
        pauseManager ??= ChessPauseManager.GetOrCreate();
        pauseManager.PauseStateChanged -= OnPauseStateChanged;
        pauseManager.PauseStateChanged += OnPauseStateChanged;

        if (enablePauseDebugLogs)
        {
            Debug.Log($"[ChessPauseMenuUI] {name} ({GetInstanceID()}) subscribed to {pauseManager.name} ({pauseManager.GetInstanceID()})", this);
        }

        SyncVisualState();
    }

    void OnDisable()
    {
        if (pauseManager != null)
        {
            pauseManager.PauseStateChanged -= OnPauseStateChanged;
        }
    }

    void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    void Start()
    {
        SyncVisualState();
    }

    void OnPauseStateChanged(bool _, bool __)
    {
        SyncVisualState();
    }

    void SyncVisualState()
    {
        if (pauseManager == null || !EnsureUiReferences())
        {
            return;
        }

        if (pauseManager.IsPaused)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    bool EnsureUiReferences()
    {
        RootResolution resolution = ResolvePauseMenuRoot();
        if (pauseMenuRoot == null)
        {
            return false;
        }

        rootCanvasGroup = pauseMenuRoot.GetComponent<CanvasGroup>() ?? pauseMenuRoot.AddComponent<CanvasGroup>();
        PopulatePauseMenuIfMissing();

        if (enablePauseDebugLogs)
        {
            Debug.Log($"[ChessPauseMenuUI] {name} ({GetInstanceID()}) controlling root {pauseMenuRoot.name} ({pauseMenuRoot.GetInstanceID()}) path={GetHierarchyPath(pauseMenuRoot.transform)} source={resolution}", this);
        }

        return true;
    }

    RootResolution ResolvePauseMenuRoot()
    {
        if (pauseMenuRoot != null)
        {
            return RootResolution.Assigned;
        }

        Canvas canvas = ChessMasterCanvas.GetOrCreateCanvas();
        Transform canvasTransform = canvas.transform;

        Transform found = canvasTransform.Find("PauseMenuRoot");
        if (found == null)
        {
            Transform overlayRoot = canvasTransform.Find("OverlayRoot");
            if (overlayRoot != null)
            {
                found = overlayRoot.Find("PauseMenuRoot");
            }
        }

        if (found != null)
        {
            pauseMenuRoot = found.gameObject;
            return RootResolution.AutoFound;
        }

        GameObject fallback = new("PauseMenuRoot", typeof(RectTransform), typeof(CanvasGroup));
        fallback.transform.SetParent(canvasTransform, false);
        ConfigureFullScreenRect(fallback.GetComponent<RectTransform>());
        pauseMenuRoot = fallback;
        // Debug.LogWarning($"[ChessPauseMenuUI] PauseMenuRoot missing under ChessMasterCanvas. Created fallback root once: {pauseMenuRoot.name} ({pauseMenuRoot.GetInstanceID()})", this);
        return RootResolution.FallbackCreated;
    }

    void Show()
    {
        Canvas masterCanvas = ChessMasterCanvas.GetOrCreateCanvas();
        if (!masterCanvas.gameObject.activeSelf)
        {
            masterCanvas.gameObject.SetActive(true);
        }

        pauseMenuRoot.SetActive(true);
        rootCanvasGroup.alpha = 1f;
        rootCanvasGroup.interactable = true;
        rootCanvasGroup.blocksRaycasts = true;

        CacheOptionalControls();
        RefreshOptionalControls();
    }

    public void Hide()
    {
        if (pauseMenuRoot == null)
        {
            return;
        }

        rootCanvasGroup.alpha = 0f;
        rootCanvasGroup.interactable = false;
        rootCanvasGroup.blocksRaycasts = false;
        pauseMenuRoot.SetActive(false);
    }

    void CacheOptionalControls()
    {
        if (pauseMenuRoot == null)
        {
            return;
        }

        resumeButton ??= FindButton("ResumeButton");
        devButton ??= FindButton("DevMenuButton");
        debugButton ??= FindButton("DebugMenuButton");
        boardButton ??= FindButton("BoardPresetsButton");
        aiConsoleButton ??= FindButton("StockfishConsoleButton");
        restartButton ??= FindButton("RestartButton");
        mainMenuButton ??= FindButton("MainMenuButton");
        quitButton ??= FindButton("QuitButton");
        resignButton ??= FindButton("ResignButton");
        statusText ??= FindText("StatusText");
        footerHintText ??= FindText("FooterHintText");
    }

    void RefreshOptionalControls()
    {
        bool fullyPaused = pauseManager != null && pauseManager.IsPaused;
        if (statusText != null)
        {
            statusText.text = fullyPaused ? "Game paused" : "Waiting for safe pause...";
        }

        if (footerHintText != null)
        {
            footerHintText.text = fullyPaused ? "Gameplay paused. Open tools or resume." : "Waiting for AI...";
        }

        SetButtonVisibility(devButton, sandbox != null);
        SetButtonVisibility(debugButton, sandbox != null);
        SetButtonVisibility(boardButton, sandbox != null);
        SetButtonVisibility(aiConsoleButton, aiConsole != null);
        SetButtonVisibility(restartButton, ChessBoard.Instance != null);
        SetButtonVisibility(mainMenuButton, HasMainMenuScene());
        SetButtonVisibility(quitButton, true);
        SetButtonVisibility(resignButton, resignUi != null);
    }

    void PopulatePauseMenuIfMissing()
    {
        if (pauseMenuRoot == null)
        {
            return;
        }

        RectTransform rootRect = pauseMenuRoot.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            ConfigureFullScreenRect(rootRect);
        }

        Transform existingPanel = FindChildByName(pauseMenuRoot.transform, "PauseMenuPanel");
        if (existingPanel == null)
        {
            CreateMenuVisualTree();
        }

        EnsureMenuButtons(existingPanel != null ? existingPanel : FindChildByName(pauseMenuRoot.transform, "PauseMenuPanel"));
        WireButtons();

        if (enablePauseDebugLogs)
        {
            Debug.Log("[ChessPauseMenuUI] Pause menu rebuilt and DevMenuButton ensured.", this);
        }
    }


    void EnsureMenuButtons(Transform panel)
    {
        if (panel == null)
        {
            return;
        }

        EnsureButtonExists(panel, "ResumeButton", "Resume");
        EnsureButtonExists(panel, "DevMenuButton", "Dev Menu");
        EnsureButtonExists(panel, "ResignButton", "Resign");
        EnsureButtonExists(panel, "RestartButton", "Restart");
        EnsureButtonExists(panel, "MainMenuButton", "Main Menu");
        EnsureButtonExists(panel, "QuitButton", "Quit");

        SetSiblingIfFound(panel, "ResumeButton", 1);
        SetSiblingIfFound(panel, "DevMenuButton", 2);
        SetSiblingIfFound(panel, "ResignButton", 3);
        SetSiblingIfFound(panel, "RestartButton", 4);
        SetSiblingIfFound(panel, "MainMenuButton", 5);
        SetSiblingIfFound(panel, "QuitButton", 6);
    }

    void EnsureButtonExists(Transform panel, string objectName, string text)
    {
        if (FindChildByName(panel, objectName) != null)
        {
            return;
        }

        CreateButton(panel, objectName, text);
    }

    static void SetSiblingIfFound(Transform parent, string name, int index)
    {
        Transform child = FindChildByName(parent, name);
        if (child != null)
        {
            child.SetSiblingIndex(Mathf.Min(index, parent.childCount - 1));
        }
    }

    void CreateMenuVisualTree()
    {
        GameObject panelObject = new("PauseMenuPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelObject.transform.SetParent(pauseMenuRoot.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(560f, 640f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.88f);

        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 16f;
        layout.padding = new RectOffset(36, 36, 36, 36);
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateLabel(panelObject.transform, "PauseMenuTitle", "Paused", 60f, 48, FontStyles.Bold);
        CreateButton(panelObject.transform, "ResumeButton", "Resume");
        CreateButton(panelObject.transform, "DevMenuButton", "Dev Menu");
        CreateButton(panelObject.transform, "ResignButton", "Resign");
        CreateButton(panelObject.transform, "RestartButton", "Restart");
        CreateButton(panelObject.transform, "MainMenuButton", "Main Menu");
        CreateButton(panelObject.transform, "QuitButton", "Quit");
    }

    TMP_Text CreateLabel(Transform parent, string objectName, string text, float height, float fontSize, FontStyles style)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.preferredHeight = height;

        TMP_Text label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        return label;
    }

    Button CreateButton(Transform parent, string objectName, string text)
    {
        GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 64f;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

        GameObject labelObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        ConfigureFullScreenRect(labelObject.GetComponent<RectTransform>());
        TMP_Text label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 34f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        return buttonObject.GetComponent<Button>();
    }

    void WireButtons()
    {
        CacheOptionalControls();

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(OnResumeClicked);
            resumeButton.onClick.AddListener(OnResumeClicked);
        }

        if (devButton != null)
        {
            devButton.onClick.RemoveListener(OnDevMenuClicked);
            devButton.onClick.AddListener(OnDevMenuClicked);
        }

        if (resignButton != null)
        {
            resignButton.onClick.RemoveListener(OnResignClicked);
            resignButton.onClick.AddListener(OnResignClicked);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(OnRestartClicked);
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitClicked);
            quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    public void ShowPauseMenuFromDevMenu()
    {
        Show();
    }

    public void CloseDevMenuToPause()
    {
        if (sandbox == null)
        {
            return;
        }

        sandbox.ReturnToPauseMenuFromDevMenu();
        Show();

        if (enablePauseDebugLogs)
        {
            Debug.Log("[ChessPauseMenuUI] Dev menu closed back to pause menu.", this);
        }
    }

    void OnResumeClicked() => pauseManager?.RequestResume();

    void OnDevMenuClicked()
    {
        if (enablePauseDebugLogs)
        {
            Debug.Log("[ChessPauseMenuUI] Dev Menu button clicked.", this);
        }

        pauseManager ??= ChessPauseManager.GetOrCreate();
        sandbox ??= ChessDevSandboxController.Instance;

        if (sandbox == null || pauseManager == null || !pauseManager.IsPaused)
        {
            Debug.LogWarning("[ChessPauseMenuUI] Dev menu unavailable while paused.", this);
            return;
        }

        if (!sandbox.EnsureDevPanelReady())
        {
            Debug.LogWarning("[ChessPauseMenuUI] Dev Menu button clicked but ChessDevPanel could not be resolved.", this);
            return;
        }

        Hide();
        sandbox.OpenDevMenuFromPauseMenu();
    }

    public void RequestResignFromPauseMenu()
    {
        Hide();
        resignUi?.OpenConfirmFromPauseMenu();
    }

    void OnResignClicked()
    {
        RequestResignFromPauseMenu();
    }

    void OnRestartClicked()
    {
        ChessBoard board = ChessBoard.Instance;
        if (board != null)
        {
            board.RestartMatch();
            pauseManager?.RequestResume();
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnMainMenuClicked()
    {
        if (HasMainMenuScene())
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    void OnQuitClicked() => Application.Quit();

    static bool HasMainMenuScene()
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (sceneName == "MainMenu")
            {
                return true;
            }
        }

        return false;
    }

    static void ConfigureFullScreenRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    Button FindButton(string objectName)
    {
        Transform node = FindChildByName(pauseMenuRoot.transform, objectName);
        return node != null ? node.GetComponent<Button>() : null;
    }

    TMP_Text FindText(string objectName)
    {
        Transform node = FindChildByName(pauseMenuRoot.transform, objectName);
        return node != null ? node.GetComponent<TMP_Text>() : null;
    }

    static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName)
            {
                return child;
            }

            Transform found = FindChildByName(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static void SetButtonVisibility(Button button, bool visible)
    {
        if (button != null)
        {
            button.gameObject.SetActive(visible);
        }
    }

    static string GetHierarchyPath(Transform current)
    {
        if (current == null)
        {
            return "<null>";
        }

        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = $"{current.name}/{path}";
        }

        return path;
    }
}
