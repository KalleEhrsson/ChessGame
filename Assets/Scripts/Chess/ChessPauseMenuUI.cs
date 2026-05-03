using TMPro;
using UnityEngine;
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
    TMP_Text resumeButtonLabel;

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
        canvas = ChessMasterCanvas.GetOrCreateCanvas();
        Transform pauseRoot = ChessMasterCanvas.GetOrCreateOverlayRoot("PauseMenuRoot");
        Debug.Log($"[ChessPauseMenuUI] Canvas found/created: {canvas.name}", this);
        Debug.Log($"[ChessPauseMenuUI] Canvas render mode: {canvas.renderMode}", this);
        Debug.Log($"[ChessPauseMenuUI] Canvas sorting order: {canvas.sortingOrder}", this);
        Debug.Log($"[ChessPauseMenuUI] Canvas scale factor: {canvas.scaleFactor}", this);

        overlay = new GameObject("PauseOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(pauseRoot, false);
        RectTransform oRect = overlay.GetComponent<RectTransform>();
        oRect.anchorMin = Vector2.zero;
        oRect.anchorMax = Vector2.one;
        oRect.offsetMin = Vector2.zero;
        oRect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        GameObject panel = new("PausePanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(overlay.transform, false);
        RectTransform pRect = panel.GetComponent<RectTransform>();
        pRect.anchorMin = pRect.anchorMax = new Vector2(0.5f, 0.5f);
        pRect.pivot = new Vector2(0.5f, 0.5f);
        pRect.sizeDelta = new Vector2(500f, 460f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        VerticalLayoutGroup v = panel.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(24, 24, 24, 24);
        v.spacing = 10;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = false;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;

        CreateText(panel.transform, "TitleText", "PAUSED", 54f, 64f, new Color(1f, 1f, 1f, 1f));
        CreateText(panel.transform, "HeaderText", "Pause UI is visible", 28f, 38f, new Color(0.85f, 0.9f, 1f, 1f));
        statusText = CreateText(panel.transform, "StatusText", string.Empty, 22f, 32f, new Color(1f, 1f, 1f, 0.95f));

        GameObject buttonsContainer = new("ButtonsContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
        buttonsContainer.transform.SetParent(panel.transform, false);
        VerticalLayoutGroup buttonLayout = buttonsContainer.GetComponent<VerticalLayoutGroup>();
        buttonLayout.spacing = 8;
        buttonLayout.childAlignment = TextAnchor.UpperCenter;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = false;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childForceExpandHeight = false;
        LayoutElement buttonsLayoutElement = buttonsContainer.AddComponent<LayoutElement>();
        buttonsLayoutElement.flexibleWidth = 1f;
        buttonsLayoutElement.preferredHeight = 290f;

        resumeButton = CreateButton(buttonsContainer.transform, "Resume", () => pauseManager.Resume(), out resumeButtonLabel);
        aiConsoleButton = CreateButton(buttonsContainer.transform, "AI / Stockfish Console", () => aiConsole.SetVisible(true), out _);
        sandboxButton = CreateButton(buttonsContainer.transform, "Sandbox Tools", () => { if (sandbox != null) sandbox.SetOpenFromPauseMenu(true); }, out _);
        restartButton = CreateButton(buttonsContainer.transform, "Restart / New Game", () => { ChessBoard.Instance?.RestartMatch(); pauseManager.ResetPauseState(); }, out _);
        resignButton = CreateButton(buttonsContainer.transform, "Resign", () => resignUi.OpenConfirmFromPauseMenu(), out _);

        LogElementRect(statusText.rectTransform, "StatusText");

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
        resumeButtonLabel.text = fullyPaused ? "Resume" : "Cancel Pause";

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

    static TMP_Text CreateText(Transform parent, string objectName, string value, float size, float preferredHeight, Color color)
    {
        GameObject go = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.offsetMin = new Vector2(0f, 0f);
        rect.offsetMax = new Vector2(0f, 0f);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        layout.flexibleWidth = 1f;
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = false;
        return text;
    }

    Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, out TMP_Text labelText)
    {
        GameObject go = new(label.Replace(" ", string.Empty), typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        LayoutElement buttonLayout = go.AddComponent<LayoutElement>();
        buttonLayout.preferredHeight = 52f;
        buttonLayout.flexibleWidth = 1f;
        go.GetComponent<Image>().color = new Color(0.86f, 0.86f, 0.9f, 1f);
        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        GameObject labelObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(go.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(12f, 6f);
        labelRect.offsetMax = new Vector2(-12f, -6f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);

        labelText = labelObject.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 24f;
        labelText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        labelText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        labelText.verticalAlignment = VerticalAlignmentOptions.Middle;
        labelText.enableWordWrapping = false;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        labelText.enableAutoSizing = false;
        labelText.color = new Color(0.12f, 0.12f, 0.12f, 1f);

        LogButtonRect(go.name, rect, labelRect);
        return button;
    }

    static void LogButtonRect(string buttonName, RectTransform buttonRect, RectTransform labelRect)
    {
        Debug.Log($"[ChessPauseMenuUI] {buttonName} rect={buttonRect.rect.width:0}x{buttonRect.rect.height:0} labelRect={labelRect.rect.width:0}x{labelRect.rect.height:0}");
    }

    static void LogElementRect(RectTransform rect, string elementName)
    {
        Debug.Log($"[ChessPauseMenuUI] {elementName} rect={rect.rect.width:0}x{rect.rect.height:0}");
    }
}
