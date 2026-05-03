using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChessPauseMenuUI : MonoBehaviour
{
    #region Variables
    Canvas canvas;
    GameObject overlay;
    TMP_Text statusText;
    Button resumeButton;
    Button aiConsoleButton;
    Button sandboxButton;
    Button restartButton;
    Button resignButton;

    ChessPauseManager pauseManager;
    ChessAiRoundConsole aiConsole;
    ChessDevSandboxController sandbox;
    ChessResignUiController resignUi;
    Rect debugRect = new(12f, 12f, 460f, 170f);

    [SerializeField] bool enablePauseDebugOverlay = true;

    public bool IsVisible => overlay != null && overlay.activeSelf;
    public bool IsRootActive => overlay != null && overlay.activeSelf;
    public bool IsCanvasActive => canvas != null && canvas.gameObject.activeInHierarchy;

    #endregion

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureRuntimeInstance()
    {
        if (FindFirstObjectByType<ChessPauseMenuUI>() != null)
        {
            return;
        }

        GameObject host = new("ChessPauseMenuUI");
        DontDestroyOnLoad(host);
        host.AddComponent<ChessPauseMenuUI>();
    }

    #region Unity

    void Awake()
    {
        pauseManager = ChessPauseManager.GetOrCreate();
        aiConsole = ChessAiRoundConsole.GetOrCreate();
        sandbox = ChessDevSandboxController.Instance;
        resignUi = ChessResignUiController.GetOrCreate();
        EnsureUi();
    }

    void Update()
    {
        Refresh();
    }

    void OnGUI()
    {
        if (!enablePauseDebugOverlay)
        {
            return;
        }

        GUI.color = new Color(1f, 0.95f, 0.2f, 0.95f);
        bool pPressed = pauseManager != null && pauseManager.ConsumeLastPPressedThisFrame();
        string text = $"P pressed: {(pPressed ? "yes" : "no")}\nPause requested: {pauseManager != null && pauseManager.IsPauseRequested}\nPause pending: {pauseManager != null && pauseManager.IsPausePending}\nPaused: {pauseManager != null && pauseManager.IsPaused}\nPause menu root active: {IsRootActive}\nCanvas active: {IsCanvasActive}";
        GUI.Box(debugRect, text);
    }

    #endregion

    #region UI

    void EnsureUi()
    {
        if (EventSystem.current == null)
        {
            GameObject es = new("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        Canvas existing = FindFirstObjectByType<Canvas>();
        if (existing == null)
        {
            GameObject canvasObject = new("ChessPauseCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            DontDestroyOnLoad(canvasObject);
        }
        else
        {
            canvas = existing;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 5000);
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        Debug.Log($"[ChessPauseMenuUI] Canvas found/created: {canvas.name}", this);
        Debug.Log($"[ChessPauseMenuUI] Canvas render mode: {canvas.renderMode}", this);
        Debug.Log($"[ChessPauseMenuUI] Canvas sorting order: {canvas.sortingOrder}", this);
        Debug.Log($"[ChessPauseMenuUI] Canvas scale factor: {canvas.scaleFactor}", this);

        overlay = new GameObject("PauseOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        RectTransform oRect = overlay.GetComponent<RectTransform>();
        oRect.anchorMin = Vector2.zero;
        oRect.anchorMax = Vector2.one;
        oRect.offsetMin = Vector2.zero;
        oRect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        GameObject panel = new("PausePanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(overlay.transform, false);
        RectTransform pRect = panel.GetComponent<RectTransform>();
        pRect.anchorMin = pRect.anchorMax = new Vector2(0.5f, 0.5f);
        pRect.sizeDelta = new Vector2(420f, 0f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        VerticalLayoutGroup v = panel.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(20, 20, 20, 20);
        v.spacing = 8;
        v.childControlHeight = false;
        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateText(panel.transform, "PAUSED", 54);
        CreateText(panel.transform, "Pause UI is visible", 30);
        statusText = CreateText(panel.transform, string.Empty, 24);
        resumeButton = CreateButton(panel.transform, "Resume", () => pauseManager.Resume());
        aiConsoleButton = CreateButton(panel.transform, "AI / Stockfish Console", () => aiConsole.SetVisible(true));
        sandboxButton = CreateButton(panel.transform, "Sandbox Tools", () => { if (sandbox != null) sandbox.SetOpenFromPauseMenu(true); });
        restartButton = CreateButton(panel.transform, "Restart / New Game", () => { ChessBoard.Instance?.RestartMatch(); pauseManager.ResetPauseState(); });
        resignButton = CreateButton(panel.transform, "Resign", () => resignUi.OpenConfirmFromPauseMenu());

        overlay.SetActive(false);
        Debug.Log($"[ChessPauseMenuUI] Root active: {overlay.activeSelf}", this);
    }

    void Refresh()
    {
        if (overlay == null || pauseManager == null)
        {
            return;
        }

        bool shouldShow = pauseManager.IsPauseRequested;
        if (shouldShow && !overlay.activeSelf)
        {
            Show();
        }
        else if (!shouldShow && overlay.activeSelf)
        {
            Hide();
        }
        if (!pauseManager.IsPauseRequested)
        {
            aiConsole?.SetVisible(false);
            sandbox?.SetOpenFromPauseMenu(false);
            return;
        }

        bool fullyPaused = pauseManager.IsPaused;
        statusText.gameObject.SetActive(!fullyPaused);
        statusText.text = fullyPaused ? string.Empty : "Finishing current move...";
        resumeButton.GetComponentInChildren<TMP_Text>().text = fullyPaused ? "Resume" : "Cancel Pause";

        aiConsoleButton.interactable = fullyPaused;
        sandboxButton.interactable = fullyPaused;
        restartButton.interactable = fullyPaused;
        resignButton.interactable = fullyPaused;
    }

    void Show()
    {
        Debug.Log("[ChessPauseMenuUI] Show called", this);
        overlay.SetActive(true);
        RectTransform rootRect = overlay.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        CanvasGroup group = overlay.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = overlay.AddComponent<CanvasGroup>();
        }

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        Debug.Log($"[ChessPauseMenuUI] Root active: {overlay.activeSelf}", this);
        Debug.Log($"[ChessPauseMenuUI] Root position/anchored position: {rootRect.position}/{rootRect.anchoredPosition}", this);
        Debug.Log($"[ChessPauseMenuUI] Root alpha if CanvasGroup exists: {group.alpha}", this);
        Debug.Log("[ChessPauseMenuUI] Pause menu should now be visible", this);
    }

    void Hide()
    {
        Debug.Log("[ChessPauseMenuUI] Hide called", this);
        overlay.SetActive(false);
        Debug.Log($"[ChessPauseMenuUI] Root active: {overlay.activeSelf}", this);
    }

    #endregion

    static TMP_Text CreateText(Transform parent, string value, float size)
    {
        GameObject go = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 42f);
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        return text;
    }

    Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new(label.Replace(" ", string.Empty), typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 44f);
        go.GetComponent<Image>().color = new Color(0.86f, 0.86f, 0.9f, 1f);
        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);
        TMP_Text labelText = CreateText(go.transform, label, 24f);
        labelText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        return button;
    }
}
