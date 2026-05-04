using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChessAiRoundConsole : MonoBehaviour
{
    #region Singleton

    public static ChessAiRoundConsole Instance { get; private set; }

    public static ChessAiRoundConsole GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessAiRoundConsole[] found = FindObjectsByType<ChessAiRoundConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        ChessAiRoundConsole existing = found.Length > 0 ? found[0] : null;
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureUi();
            return Instance;
        }

        GameObject host = new("ChessAiRoundConsole");
        Instance = host.AddComponent<ChessAiRoundConsole>();
        // Debug.Log("[ChessRuntimeBootstrap] Created fallback instance: ChessAiRoundConsole");
        return Instance;
    }

    #endregion

    #region Variables

    const float DefaultPanelWidth = 420f;
    const float MinPanelHeight = 200f;
    const float MaxPanelHeight = 460f;
    const float ScreenPadding = 20f;
    const float PanelInnerPadding = 12f;
    const float MinViewportHeight = 120f;
    const float PanelChromeHeight = 24f;

    string playerMove;
    bool isThinking;
    string stockfishMove;
    string aiMove;
    string syncedFen;
    bool stockfishSynced;
    string debugValidationError;
    string debugCancellationReason;

    int? currentDepth;
    string currentEval;
    string currentPv;

    bool isVisible;

    Canvas rootCanvas;
    RectTransform viewportRect;

    [Header("UI References")]
    [SerializeField] RectTransform panelRoot;
    [SerializeField] TextMeshProUGUI logText;
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] RectTransform contentRoot;

    string lastRenderedText;

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
        EnsureUi();
        RefreshDisplayIfChanged();
    }

    void Update()
    {
        UpdateScrollState();
    }

    #endregion

    #region API

    public void StartNewRound(string nextPlayerMove)
    {
        playerMove = nextPlayerMove;
        isThinking = false;
        stockfishMove = null;
        aiMove = null;
        currentDepth = null;
        currentEval = null;
        currentPv = null;
        RefreshDisplayIfChanged();
    }

    public void SetFen(string nextFen)
    {
        syncedFen = nextFen;
        RefreshDisplayIfChanged();
    }

    public void SetDebugSyncStatus(string fen, bool isValid, string validationError, bool thinkingCancelled, string cancellationReason)
    {
        syncedFen = fen;
        stockfishSynced = isValid;
        debugValidationError = validationError;
        debugCancellationReason = thinkingCancelled ? cancellationReason : null;
        RefreshDisplayIfChanged();
    }

    public void SetThinking(bool thinking)
    {
        if (isThinking == thinking)
        {
            return;
        }

        isThinking = thinking;
        RefreshDisplayIfChanged();
    }

    public void SetStockfishInfo(string rawInfoLine)
    {
        if (!TryParseStockfishInfo(rawInfoLine, out int? depth, out string eval, out string pv))
        {
            return;
        }

        bool changed = false;

        if (depth.HasValue && currentDepth != depth)
        {
            currentDepth = depth;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(eval) && !string.Equals(currentEval, eval, StringComparison.Ordinal))
        {
            currentEval = eval;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(pv) && !string.Equals(currentPv, pv, StringComparison.Ordinal))
        {
            currentPv = pv;
            changed = true;
        }

        if (changed)
        {
            RefreshDisplayIfChanged();
        }
    }

    public void SetStockfishMove(string bestMove)
    {
        if (string.Equals(stockfishMove, bestMove, StringComparison.Ordinal))
        {
            return;
        }

        stockfishMove = bestMove;
        RefreshDisplayIfChanged();
    }

    public void SetAiMove(string readableMove)
    {
        if (string.Equals(aiMove, readableMove, StringComparison.Ordinal))
        {
            return;
        }

        aiMove = readableMove;
        RefreshDisplayIfChanged();
    }


    public void SetVisible(bool visible)
    {
        isVisible = visible;
        ApplyVisibility();
    }

    public void ClearCurrentRound()
    {
        playerMove = null;
        isThinking = false;
        stockfishMove = null;
        aiMove = null;
        currentDepth = null;
        currentEval = null;
        currentPv = null;
        RefreshDisplayIfChanged();
    }

    #endregion

    #region UI

    void EnsureUi()
    {
        if (panelRoot != null || scrollRect != null || contentRoot != null || logText != null)
        {
            TryResolveMissingReferences();
        }

        if (panelRoot != null && logText != null && scrollRect != null && contentRoot != null)
        {
            ResolveCanvasFromHierarchy();
            EnsureCanvasSettings();
            EnsurePanelSettings();
            EnsureViewportReference();
            EnsureContentSettings();
            EnsureTextSettings();
            return;
        }

        BuildRuntimeUi();
        ApplyVisibility();
    }

    void BuildRuntimeUi()
    {
        rootCanvas = ChessMasterCanvas.GetOrCreateCanvas();
        Transform consoleRoot = ChessMasterCanvas.GetOrCreateOverlayRoot("StockfishConsoleRoot");
        EnsureCanvasSettings();

        panelRoot = CreateRect("Panel", consoleRoot);
        Image panelImage = panelRoot.gameObject.AddComponent<Image>();
        panelImage.color = ChessUITheme.MainBackground;
        EnsurePanelSettings();

        RectTransform scrollRectTransform = CreateRect("ScrollView", panelRoot);
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(PanelInnerPadding, PanelInnerPadding);
        scrollRectTransform.offsetMax = new Vector2(-PanelInnerPadding, -PanelInnerPadding);

        scrollRect = scrollRectTransform.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;
        scrollRect.inertia = true;

        viewportRect = CreateRect("Viewport", scrollRectTransform);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        Image viewportImage = viewportRect.gameObject.AddComponent<Image>();
        viewportImage.color = ChessUITheme.PanelSecondary;
        Mask viewportMask = viewportRect.gameObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        contentRoot = CreateRect("Content", viewportRect);
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.anchoredPosition = Vector2.zero;
        contentRoot.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = false;
        contentLayout.spacing = 4f;

        ContentSizeFitter contentFitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform textRect = CreateRect("RoundLog", contentRoot);
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.sizeDelta = new Vector2(0f, 0f);

        TextMeshProUGUI tmp = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 20f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = 24f;
        tmp.color = new Color(0.93f, 0.96f, 1f, 1f);
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        logText = tmp;

        LayoutElement textLayout = textRect.gameObject.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;
        textLayout.minHeight = 120f;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRoot;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    void ResolveCanvasFromHierarchy()
    {
        if (panelRoot == null)
        {
            return;
        }

        rootCanvas = panelRoot.GetComponentInParent<Canvas>();
    }

    void EnsureCanvasSettings()
    {
        if (rootCanvas == null)
        {
            return;
        }

        CanvasScaler scaler = rootCanvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = rootCanvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (rootCanvas.GetComponent<GraphicRaycaster>() == null)
        {
            rootCanvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    void EnsurePanelSettings()
    {
        if (panelRoot == null)
        {
            return;
        }

        panelRoot.anchorMin = new Vector2(0f, 1f);
        panelRoot.anchorMax = new Vector2(0f, 1f);
        panelRoot.pivot = new Vector2(0f, 1f);
        panelRoot.anchoredPosition = new Vector2(ScreenPadding, -ScreenPadding);

        Vector2 size = panelRoot.sizeDelta;
        if (size.x <= 0f || size.y <= 0f)
        {
            size = new Vector2(DefaultPanelWidth, MinPanelHeight);
        }

        panelRoot.sizeDelta = new Vector2(Mathf.Max(DefaultPanelWidth, size.x), Mathf.Max(MinPanelHeight, size.y));
    }

    void EnsureViewportReference()
    {
        if (scrollRect == null)
        {
            return;
        }

        viewportRect = scrollRect.viewport;
        if (viewportRect == null)
        {
            Transform viewport = scrollRect.transform.Find("Viewport");
            if (viewport != null)
            {
                viewportRect = viewport as RectTransform;
                scrollRect.viewport = viewportRect;
            }
        }
    }

    void TryResolveMissingReferences()
    {
        if (panelRoot != null)
        {
            if (scrollRect == null)
            {
                scrollRect = panelRoot.GetComponentInChildren<ScrollRect>(true);
            }

            if (contentRoot == null && scrollRect != null)
            {
                contentRoot = scrollRect.content;
            }

            if (logText == null)
            {
                logText = panelRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        if (scrollRect != null && contentRoot == null)
        {
            contentRoot = scrollRect.content;
        }

        if (scrollRect != null && panelRoot == null)
        {
            panelRoot = scrollRect.GetComponentInParent<RectTransform>();
        }
    }

    void EnsureContentSettings()
    {
        if (contentRoot == null)
        {
            return;
        }

        VerticalLayoutGroup layout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.spacing = 4f;

        ContentSizeFitter fitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void EnsureTextSettings()
    {
        if (logText == null)
        {
            return;
        }

        RectTransform textRect = logText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(0f, 0f);

        logText.fontSize = 20f;
        logText.enableAutoSizing = true;
        logText.fontSizeMin = 18f;
        logText.fontSizeMax = 24f;
        logText.color = new Color(0.93f, 0.96f, 1f, 1f);
        logText.alignment = TextAlignmentOptions.TopLeft;
        logText.textWrappingMode = TextWrappingModes.Normal;
        logText.overflowMode = TextOverflowModes.Overflow;

        LayoutElement textLayout = logText.GetComponent<LayoutElement>();
        if (textLayout == null)
        {
            textLayout = logText.gameObject.AddComponent<LayoutElement>();
        }

        textLayout.flexibleWidth = 1f;
        textLayout.minHeight = 120f;
    }

    void ApplyVisibility()
    {
        if (rootCanvas == null)
        {
            return;
        }

        if (panelRoot != null)
        {
            panelRoot.gameObject.SetActive(isVisible);
        }
    }

    void UpdateScrollState()
    {
        if (panelRoot == null || viewportRect == null || contentRoot == null || scrollRect == null || !isVisible)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        float preferredContentHeight = LayoutUtility.GetPreferredHeight(contentRoot);
        float maxPanelHeight = Mathf.Min(MaxPanelHeight, Screen.height - (ScreenPadding * 2f));
        maxPanelHeight = Mathf.Max(MinPanelHeight, maxPanelHeight);

        float maxViewportHeight = Mathf.Max(MinViewportHeight, maxPanelHeight - PanelChromeHeight);
        float targetViewportHeight = Mathf.Clamp(preferredContentHeight + 4f, MinViewportHeight, maxViewportHeight);
        float targetPanelHeight = Mathf.Clamp(targetViewportHeight + PanelChromeHeight, MinPanelHeight, maxPanelHeight);

        panelRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, DefaultPanelWidth);
        panelRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetPanelHeight);

        bool shouldScroll = preferredContentHeight > targetViewportHeight + 0.5f;
        if (scrollRect.vertical != shouldScroll)
        {
            scrollRect.vertical = shouldScroll;
        }

        scrollRect.verticalNormalizedPosition = 1f;
    }

    void RefreshDisplayIfChanged()
    {
        if (logText == null)
        {
            EnsureUi();
        }

        string next = BuildDisplayText();
        if (string.Equals(lastRenderedText, next, StringComparison.Ordinal))
        {
            return;
        }

        lastRenderedText = next;
        logText.text = next;
        UpdateScrollState();
    }

    string BuildDisplayText()
    {
        StringBuilder builder = new();
        builder.AppendLine("=== AI ROUND ===");
        builder.AppendLine();
        builder.AppendLine("Player move:");
        builder.AppendLine(string.IsNullOrWhiteSpace(playerMove) ? "-" : playerMove);
        builder.AppendLine($"Status: {(isThinking ? "AI thinking..." : "Idle")}");
        builder.AppendLine();
        builder.AppendLine("Stockfish thinking:");
        builder.AppendLine($"Depth: {(currentDepth.HasValue ? currentDepth.Value.ToString() : "-")}");
        builder.AppendLine($"Eval: {(string.IsNullOrWhiteSpace(currentEval) ? "-" : currentEval)}");
        builder.AppendLine($"Best line: {(string.IsNullOrWhiteSpace(currentPv) ? "-" : currentPv)}");
        builder.AppendLine();
        builder.AppendLine($"Stockfish best move: {(string.IsNullOrWhiteSpace(stockfishMove) ? "-" : stockfishMove)}");
        builder.AppendLine("AI move:");
        builder.AppendLine(string.IsNullOrWhiteSpace(aiMove) ? "-" : aiMove);
        builder.AppendLine();
        builder.AppendLine($"Stockfish synced: {(stockfishSynced ? "Yes" : "No")}");
        builder.AppendLine($"Synced FEN: {(string.IsNullOrWhiteSpace(syncedFen) ? "-" : syncedFen)}");
        if (!string.IsNullOrWhiteSpace(debugValidationError))
        {
            builder.AppendLine($"Rejected state: {debugValidationError}");
        }

        if (!string.IsNullOrWhiteSpace(debugCancellationReason))
        {
            builder.AppendLine($"AI cancelled: {debugCancellationReason}");
        }
        return builder.ToString();
    }

    #endregion

    #region Parsing

    static bool TryParseStockfishInfo(string rawInfoLine, out int? depth, out string eval, out string pv)
    {
        depth = null;
        eval = null;
        pv = null;

        if (string.IsNullOrWhiteSpace(rawInfoLine))
        {
            return false;
        }

        string line = rawInfoLine.Trim();
        if (!line.StartsWith("info ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        for (int i = 1; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (token.Equals("depth", StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Length)
            {
                if (int.TryParse(tokens[i + 1], out int parsedDepth))
                {
                    depth = parsedDepth;
                }

                i++;
                continue;
            }

            if (token.Equals("score", StringComparison.OrdinalIgnoreCase) && i + 2 < tokens.Length)
            {
                string scoreType = tokens[i + 1];
                string scoreValue = tokens[i + 2];
                if (scoreType.Equals("cp", StringComparison.OrdinalIgnoreCase) && int.TryParse(scoreValue, out int centipawns))
                {
                    float pawns = centipawns / 100f;
                    eval = pawns >= 0f ? $"+{pawns:0.00}" : $"{pawns:0.00}";
                }
                else if (scoreType.Equals("mate", StringComparison.OrdinalIgnoreCase) && int.TryParse(scoreValue, out int mateIn))
                {
                    eval = $"Mate in {mateIn}";
                }

                i += 2;
                continue;
            }

            if (token.Equals("pv", StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Length)
            {
                pv = string.Join(" ", tokens, i + 1, tokens.Length - (i + 1));
                break;
            }
        }

        return depth.HasValue || !string.IsNullOrWhiteSpace(eval) || !string.IsNullOrWhiteSpace(pv);
    }

    #endregion
}
