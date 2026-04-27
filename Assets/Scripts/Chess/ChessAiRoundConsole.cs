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

        ChessAiRoundConsole existing = FindFirstObjectByType<ChessAiRoundConsole>();
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureUi();
            return Instance;
        }

        GameObject host = new("ChessAiRoundConsole");
        Instance = host.AddComponent<ChessAiRoundConsole>();
        return Instance;
    }

    #endregion

    #region Variables

    const float MaxPanelWidth = 520f;
    const float ScreenPadding = 16f;

    string playerMove;
    string fen;
    bool isThinking;
    string stockfishMove;
    string aiMove;

    int? currentDepth;
    string currentEval;
    string currentPv;

    bool isVisible = true;

    Canvas rootCanvas;
    RectTransform panelRect;
    RectTransform viewportRect;
    RectTransform contentRect;
    ScrollRect scrollRect;
    TMP_Text displayText;

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
        DontDestroyOnLoad(gameObject);
        EnsureUi();
        RefreshDisplayIfChanged();
    }

    void Update()
    {
        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            isVisible = !isVisible;
            ApplyVisibility();
        }

        if (Keyboard.current.f4Key.wasPressedThisFrame)
        {
            ClearCurrentRound();
        }

        UpdateScrollState();
    }

    #endregion

    #region API

    public void StartNewRound(string nextPlayerMove)
    {
        playerMove = nextPlayerMove;
        fen = null;
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
        if (string.Equals(fen, nextFen, StringComparison.Ordinal))
        {
            return;
        }

        fen = nextFen;
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

    public void ClearCurrentRound()
    {
        playerMove = null;
        fen = null;
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
        if (rootCanvas != null && displayText != null)
        {
            return;
        }

        GameObject canvasObject = new("ChessAiRoundConsoleCanvas");
        canvasObject.transform.SetParent(transform, false);
        rootCanvas = canvasObject.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = short.MaxValue;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new("Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-ScreenPadding, -ScreenPadding);
        panelRect.sizeDelta = new Vector2(MaxPanelWidth, 200f);

        VerticalLayoutGroup panelLayout = panelObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(14, 14, 12, 12);
        panelLayout.childControlHeight = true;
        panelLayout.childControlWidth = true;
        panelLayout.childForceExpandHeight = false;
        panelLayout.childForceExpandWidth = true;

        ContentSizeFitter panelFitter = panelObject.AddComponent<ContentSizeFitter>();
        panelFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject viewportObject = new("Viewport");
        viewportObject.transform.SetParent(panelObject.transform, false);
        viewportRect = viewportObject.GetComponent<RectTransform>();
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        Mask viewportMask = viewportObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject contentObject = new("Content");
        contentObject.transform.SetParent(viewportObject.transform, false);
        contentRect = contentObject.GetComponent<RectTransform>();
        VerticalLayoutGroup contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(0, 0, 0, 0);
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;

        ContentSizeFitter contentFitter = contentObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textObject = new("Text");
        textObject.transform.SetParent(contentObject.transform, false);
        displayText = textObject.AddComponent<TextMeshProUGUI>();
        displayText.fontSize = 22f;
        displayText.color = new Color(0.88f, 0.94f, 1f, 1f);
        displayText.alignment = TextAlignmentOptions.TopLeft;
        displayText.textWrappingMode = TextWrappingModes.PreserveWhitespace;
        displayText.overflowMode = TextOverflowModes.Overflow;

        LayoutElement textLayoutElement = textObject.AddComponent<LayoutElement>();
        textLayoutElement.preferredWidth = MaxPanelWidth - 28f;

        scrollRect = panelObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;

        ApplyVisibility();
    }

    void ApplyVisibility()
    {
        if (rootCanvas == null)
        {
            return;
        }

        rootCanvas.enabled = isVisible;
    }

    void UpdateScrollState()
    {
        if (panelRect == null || viewportRect == null || contentRect == null || scrollRect == null || !isVisible)
        {
            return;
        }

        float maxHeight = Mathf.Max(140f, Screen.height - (ScreenPadding * 2f));
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        float contentHeight = contentRect.rect.height;

        float viewportHeight = Mathf.Min(contentHeight, maxHeight - 24f);
        if (viewportHeight < 64f)
        {
            viewportHeight = 64f;
        }

        viewportRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, MaxPanelWidth - 28f);
        viewportRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight);

        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, MaxPanelWidth);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight + 24f);

        bool shouldScroll = contentHeight > viewportHeight + 0.5f;
        if (scrollRect.vertical != shouldScroll)
        {
            scrollRect.vertical = shouldScroll;
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    void RefreshDisplayIfChanged()
    {
        if (displayText == null)
        {
            EnsureUi();
        }

        string next = BuildDisplayText();
        if (string.Equals(lastRenderedText, next, StringComparison.Ordinal))
        {
            return;
        }

        lastRenderedText = next;
        displayText.text = next;
        UpdateScrollState();
    }

    string BuildDisplayText()
    {
        StringBuilder builder = new();
        builder.AppendLine("=== AI ROUND ===");
        builder.AppendLine();
        builder.AppendLine("Player move:");
        builder.AppendLine(string.IsNullOrWhiteSpace(playerMove) ? "-" : playerMove);
        builder.AppendLine();
        builder.AppendLine("FEN:");
        builder.AppendLine(string.IsNullOrWhiteSpace(fen) ? "-" : fen);
        builder.AppendLine();
        builder.AppendLine("Status:");
        builder.AppendLine(isThinking ? "AI thinking..." : "Idle");
        builder.AppendLine();
        builder.AppendLine("Stockfish thinking:");
        builder.AppendLine($"Depth: {(currentDepth.HasValue ? currentDepth.Value.ToString() : "-")}");
        builder.AppendLine($"Eval: {(string.IsNullOrWhiteSpace(currentEval) ? "-" : currentEval)}");
        builder.AppendLine($"Line: {(string.IsNullOrWhiteSpace(currentPv) ? "-" : currentPv)}");
        builder.AppendLine();
        builder.AppendLine("Stockfish result:");
        builder.AppendLine(string.IsNullOrWhiteSpace(stockfishMove) ? "-" : stockfishMove);
        builder.AppendLine();
        builder.AppendLine("AI move:");
        builder.AppendLine(string.IsNullOrWhiteSpace(aiMove) ? "-" : aiMove);
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
