using TMPro;
using UnityEngine;
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
        pauseMenuRoot = fallback;
        Debug.LogWarning($"[ChessPauseMenuUI] PauseMenuRoot missing under ChessMasterCanvas. Created fallback root once: {pauseMenuRoot.name} ({pauseMenuRoot.GetInstanceID()})", this);
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

    void Hide()
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
        SetButtonVisibility(resignButton, resignUi != null);
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
