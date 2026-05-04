using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChessTurnOverlayUI : MonoBehaviour
{
    public static ChessTurnOverlayUI Instance { get; private set; }

    [SerializeField] float dotBounceSpeed = 5.2f;
    [SerializeField] float dotBounceAmount = 5f;

    Canvas canvas;
    RectTransform panel;
    TextMeshProUGUI turnLabel;
    TextMeshProUGUI movingLabel;
    RectTransform[] dots;
    Vector2[] dotBaseAnchors;
    ChessTurnManager turnManager;

    public static ChessTurnOverlayUI GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessTurnOverlayUI existing = FindFirstObjectByType<ChessTurnOverlayUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureUi();
            return existing;
        }

        GameObject host = new("ChessTurnOverlayUI");
        Instance = host.AddComponent<ChessTurnOverlayUI>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureUi();
    }

    void OnEnable()
    {
        ChessPieceMotion.AnyMotionStateChanged += OnMotionStateChanged;
        BindTurnManager();
        RefreshTurnLabel();
        RefreshMotionVisuals(ChessPieceMotion.IsAnyAnimating);
    }

    void OnDisable()
    {
        ChessPieceMotion.AnyMotionStateChanged -= OnMotionStateChanged;
        if (turnManager != null)
        {
            turnManager.TurnChanged -= OnTurnChanged;
        }
    }

    void Update()
    {
        if (dots == null || dotBaseAnchors == null || dots.Length != dotBaseAnchors.Length)
        {
            return;
        }

        bool moving = ChessPieceMotion.IsAnyAnimating;
        for (int i = 0; i < dots.Length; i++)
        {
            RectTransform dot = dots[i];
            if (dot == null)
            {
                continue;
            }

            float wave = Mathf.Sin((Time.unscaledTime * dotBounceSpeed) + (i * 0.7f));
            float offsetY = moving ? wave * dotBounceAmount : 0f;
            dot.anchoredPosition = dotBaseAnchors[i] + new Vector2(0f, offsetY);
        }
    }

    void EnsureUi()
    {
        canvas = ChessMasterCanvas.GetOrCreateCanvas();
        Transform overlayRoot = ChessMasterCanvas.GetOrCreateOverlayRoot("TurnOverlayRoot");

        Transform existingPanel = overlayRoot.Find("TurnPanel");
        if (existingPanel == null)
        {
            GameObject panelObject = new("TurnPanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(overlayRoot, false);
            panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(24f, -24f);
            panel.sizeDelta = new Vector2(360f, 110f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.1f, 0.15f, 0.82f);

            turnLabel = CreateLabel(panel, "TurnLabel", new Vector2(16f, -12f), new Vector2(320f, 42f), 34f);
            movingLabel = CreateLabel(panel, "MovingLabel", new Vector2(16f, -58f), new Vector2(180f, 32f), 24f);
            movingLabel.text = "Moving";

            (dots, dotBaseAnchors) = CreateDots(panel);
        }
        else
        {
            panel = existingPanel as RectTransform;
            turnLabel = panel.Find("TurnLabel")?.GetComponent<TextMeshProUGUI>();
            movingLabel = panel.Find("MovingLabel")?.GetComponent<TextMeshProUGUI>();

            dots = new RectTransform[3];
            dotBaseAnchors = new Vector2[3];
            for (int i = 0; i < dots.Length; i++)
            {
                RectTransform dot = panel.Find($"Dot{i + 1}") as RectTransform;
                dots[i] = dot;
                dotBaseAnchors[i] = dot != null ? dot.anchoredPosition : Vector2.zero;
            }
        }

        BindTurnManager();
        RefreshTurnLabel();
        RefreshMotionVisuals(ChessPieceMotion.IsAnyAnimating);
    }

    void BindTurnManager()
    {
        ChessTurnManager resolved = ChessTurnManager.GetOrCreate();
        if (turnManager == resolved)
        {
            return;
        }

        if (turnManager != null)
        {
            turnManager.TurnChanged -= OnTurnChanged;
        }

        turnManager = resolved;
        if (turnManager != null)
        {
            turnManager.TurnChanged += OnTurnChanged;
        }
    }

    void OnTurnChanged(PieceTeam _)
    {
        RefreshTurnLabel();
    }

    void OnMotionStateChanged(bool isAnimating)
    {
        RefreshMotionVisuals(isAnimating);
    }

    void RefreshTurnLabel()
    {
        if (turnLabel == null)
        {
            return;
        }

        PieceTeam currentTurn = turnManager != null ? turnManager.GetCurrentTurn() : PieceTeam.White;
        Color color = currentTurn == PieceTeam.White ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.78f, 0.83f, 0.96f);
        turnLabel.color = color;
        turnLabel.text = currentTurn == PieceTeam.White ? "Turn: White" : "Turn: Black";
    }

    void RefreshMotionVisuals(bool isAnimating)
    {
        if (movingLabel != null)
        {
            movingLabel.gameObject.SetActive(isAnimating);
        }

        if (dots == null)
        {
            return;
        }

        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null)
            {
                continue;
            }

            dots[i].gameObject.SetActive(isAnimating);
            dots[i].anchoredPosition = dotBaseAnchors[i];
        }
    }

    static TextMeshProUGUI CreateLabel(RectTransform parent, string name, Vector2 anchoredTopLeft, Vector2 size, float fontSize)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI label = obj.GetComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Left;
        label.color = Color.white;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredTopLeft;
        rect.sizeDelta = size;
        return label;
    }

    static (RectTransform[], Vector2[]) CreateDots(RectTransform parent)
    {
        RectTransform[] createdDots = new RectTransform[3];
        Vector2[] baseAnchors = new Vector2[3];
        for (int i = 0; i < createdDots.Length; i++)
        {
            GameObject dotObject = new($"Dot{i + 1}", typeof(RectTransform), typeof(TextMeshProUGUI));
            dotObject.transform.SetParent(parent, false);
            RectTransform dotRect = dotObject.GetComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0f, 1f);
            dotRect.anchorMax = new Vector2(0f, 1f);
            dotRect.pivot = new Vector2(0f, 1f);
            dotRect.anchoredPosition = new Vector2(134f + (i * 16f), -58f);
            dotRect.sizeDelta = new Vector2(24f, 32f);

            TextMeshProUGUI dotText = dotObject.GetComponent<TextMeshProUGUI>();
            dotText.fontSize = 34f;
            dotText.alignment = TextAlignmentOptions.Center;
            dotText.text = ".";
            dotText.color = new Color(1f, 0.84f, 0.2f);

            createdDots[i] = dotRect;
            baseAnchors[i] = dotRect.anchoredPosition;
        }

        return (createdDots, baseAnchors);
    }
}
