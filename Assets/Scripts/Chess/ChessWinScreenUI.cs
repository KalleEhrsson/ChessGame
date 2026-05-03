using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChessWinScreenUI : MonoBehaviour
{
    #region Singleton

    public static ChessWinScreenUI Instance { get; private set; }

    public static ChessWinScreenUI GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessWinScreenUI existing = FindFirstObjectByType<ChessWinScreenUI>();
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureUi();
            return Instance;
        }

        GameObject host = new("ChessWinScreenUI");
        Instance = host.AddComponent<ChessWinScreenUI>();
        return Instance;
    }

    #endregion

    #region Variables

    Canvas rootCanvas;
    GameObject overlay;
    TextMeshProUGUI titleLabel;
    TextMeshProUGUI reasonLabel;
    TextMeshProUGUI winnerLabel;

    #endregion

    #region Properties

    public bool IsVisible => overlay != null && overlay.activeSelf;

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
        EnsureUi();
        Hide();
    }

    #endregion

    #region API

    public void ShowWin(string title, string reason, string winningSide)
    {
        EnsureUi();
        SetTexts(title, reason, winningSide);
        overlay.SetActive(true);
    }

    public void ShowDraw(string reason)
    {
        EnsureUi();
        SetTexts("Draw", reason, string.Empty);
        overlay.SetActive(true);
    }

    public void Hide()
    {
        if (overlay == null)
        {
            return;
        }

        overlay.SetActive(false);
    }

    public void EnsureUi()
    {
        EnsureCanvas();
        if (overlay != null)
        {
            return;
        }

        overlay = CreateUiObject("ChessWinScreenOverlay", rootCanvas.transform, true);
        RectTransform overlayRect = overlay.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

        GameObject panel = CreateUiObject("Panel", overlay.transform, true);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(680f, 420f);
        panel.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.96f);

        titleLabel = CreateLabel(panel.transform, "Title", 64f, new Vector2(0.5f, 0.76f), new Vector2(580f, 86f));
        reasonLabel = CreateLabel(panel.transform, "Reason", 34f, new Vector2(0.5f, 0.56f), new Vector2(580f, 74f));
        winnerLabel = CreateLabel(panel.transform, "Winner", 28f, new Vector2(0.5f, 0.44f), new Vector2(580f, 62f));

        Button restartButton = CreateButton(panel.transform, "RestartButton", "Restart / New Game", new Vector2(0.5f, 0.2f), new Vector2(280f, 56f));
        restartButton.onClick.AddListener(RestartMatch);

        Button closeButton = CreateButton(panel.transform, "CloseButton", "Close", new Vector2(0.5f, 0.07f), new Vector2(180f, 48f));
        closeButton.onClick.AddListener(Hide);
    }

    #endregion

    #region Helpers

    void EnsureCanvas()
    {
        if (rootCanvas != null)
        {
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new("ChessRuntimeCanvas");
            rootCanvas = canvasObject.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return;
        }

        rootCanvas = canvas;
        CanvasScaler existingScaler = rootCanvas.GetComponent<CanvasScaler>();
        if (existingScaler == null)
        {
            existingScaler = rootCanvas.gameObject.AddComponent<CanvasScaler>();
        }

        existingScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        existingScaler.referenceResolution = new Vector2(1920f, 1080f);
        existingScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        existingScaler.matchWidthOrHeight = 0.5f;
    }

    void RestartMatch()
    {
        ChessBoard board = ChessBoard.Instance != null ? ChessBoard.Instance : FindFirstObjectByType<ChessBoard>();
        if (board != null)
        {
            board.RestartMatch();
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void SetTexts(string title, string reason, string winningSide)
    {
        if (titleLabel != null)
        {
            titleLabel.text = string.IsNullOrWhiteSpace(title) ? "Game Over" : title;
        }

        if (reasonLabel != null)
        {
            reasonLabel.text = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason;
        }

        if (winnerLabel != null)
        {
            winnerLabel.text = winningSide;
            winnerLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(winningSide));
        }
    }

    static GameObject CreateUiObject(string name, Transform parent, bool stretch)
    {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        if (stretch)
        {
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        return obj;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, float size, Vector2 anchor, Vector2 dimensions)
    {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = dimensions;
        return label;
    }

    static Button CreateButton(Transform parent, string name, string text, Vector2 anchor, Vector2 size)
    {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image image = obj.AddComponent<Image>();
        image.color = new Color(0.23f, 0.23f, 0.23f, 1f);
        Button button = obj.AddComponent<Button>();

        TextMeshProUGUI label = CreateLabel(obj.transform, "Label", 26f, new Vector2(0.5f, 0.5f), size);
        label.text = text;
        return button;
    }

    #endregion
}
