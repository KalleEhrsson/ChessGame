using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ChessResignUiController : MonoBehaviour
{
    #region Singleton

    public static ChessResignUiController Instance { get; private set; }

    public static ChessResignUiController GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessResignUiController[] found = FindObjectsByType<ChessResignUiController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        ChessResignUiController existing = found.Length > 0 ? found[0] : null;
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureUi();
            return Instance;
        }

        GameObject host = new("ChessResignUiController");
        Instance = host.AddComponent<ChessResignUiController>();
        // Debug.Log("[ChessRuntimeBootstrap] Created fallback instance: ChessResignUiController");
        return Instance;
    }

    #endregion

    #region Variables

    ChessGameStateController gameStateController;
    ChessTurnManager turnManager;
    ChessSelectionController selectionController;
    ChessWinScreenUI winScreen;
    ChessPauseMenuUI pauseMenuUi;
    ChessPauseManager pauseManager;
    ChessDevSandboxController devSandbox;
    PawnPromotionController promotionController;
    StockfishService stockfishService;
    Canvas rootCanvas;
    GameObject confirmPanel;
    bool openedFromPauseMenu;

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
        ResolveSystems();
        EnsureUi();
        RefreshButtonState();
    }

    void OnDestroy()
    {
        if (gameStateController != null)
        {
            gameStateController.GameEnded -= OnGameEnded;
        }
    }

    #endregion

    #region API

    public void EnsureUi()
    {
        ResolveSystems();
        EnsureCanvas();
        EnsureConfirmPanel();
        ResetForNewGame();
    }

    public void ResetForNewGame()
    {
        CloseConfirm();
        winScreen?.Hide();
        RefreshButtonState();
    }

    #endregion

    #region Setup

    void ResolveSystems()
    {
        gameStateController = ChessGameStateController.GetOrCreate();
        turnManager = ChessTurnManager.GetOrCreate();
        selectionController = ChessSelectionController.GetOrCreate();
        winScreen = ChessWinScreenUI.GetOrCreate();
        pauseMenuUi = ChessPauseMenuUI.GetOrCreate();
        pauseManager = ChessPauseManager.GetOrCreate();
        devSandbox = ChessDevSandboxController.Instance;
        promotionController = PawnPromotionController.GetOrCreate();
        stockfishService = StockfishService.GetOrCreate();
        gameStateController.GameEnded -= OnGameEnded;
        gameStateController.GameEnded += OnGameEnded;
    }

    void EnsureCanvas()
    {
        if (rootCanvas != null)
        {
            return;
        }

        rootCanvas = ChessMasterCanvas.GetOrCreateCanvas();
    }

    void EnsureConfirmPanel()
    {
        if (confirmPanel != null)
        {
            return;
        }

        Transform resignRoot = ChessMasterCanvas.GetOrCreateOverlayRoot("ResignPopupRoot");
        confirmPanel = CreatePanelObject("ResignConfirmPanel", resignRoot, new Vector2(460f, 240f), new Vector2(0.5f, 0.5f), Vector2.zero);
        confirmPanel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        confirmPanel.SetActive(false);

        CreateLabel("Title", confirmPanel.transform, "Resign?", 38, new Vector2(0.5f, 0.78f), Vector2.zero);
        CreateLabel("Body", confirmPanel.transform, "Are you sure you want to resign?", 24, new Vector2(0.5f, 0.58f), Vector2.zero);

        Button yesButton = CreateActionButton(confirmPanel.transform, "YesButton", "Yes, resign", new Vector2(-105f, -74f));
        yesButton.onClick.AddListener(ConfirmResign);

        Button cancelButton = CreateActionButton(confirmPanel.transform, "CancelButton", "Cancel", new Vector2(105f, -74f));
        cancelButton.onClick.AddListener(CloseConfirmToOrigin);
    }

    #endregion

    #region UI Actions

    public void OpenConfirmFromPauseMenu()
    {
        openedFromPauseMenu = true;
        pauseMenuUi?.Hide();
        OpenConfirm();
        Debug.Log("[ChessResignUiController] Resign requested from pause.");
    }

    public bool IsConfirmOpen()
    {
        return confirmPanel != null && confirmPanel.activeSelf;
    }

    public void CloseConfirmFromPauseMenu()
    {
        CloseConfirmToOrigin();
    }

    void OpenConfirm()
    {
        if (gameStateController == null || !gameStateController.IsGameplayActive())
        {
            return;
        }

        confirmPanel?.transform.SetAsLastSibling();
        confirmPanel?.SetActive(true);
    }

    void CloseConfirm()
    {
        confirmPanel?.SetActive(false);
    }

    void CloseConfirmToOrigin()
    {
        CloseConfirm();
        if (openedFromPauseMenu && pauseManager != null && pauseManager.IsPauseRequested)
        {
            pauseMenuUi?.ShowPauseMenuFromDevMenu();
            Debug.Log("[ChessResignUiController] Resign cancelled back to pause.");
        }

        openedFromPauseMenu = false;
    }

    void ConfirmResign()
    {
        CloseConfirm();
        openedFromPauseMenu = false;
        pauseMenuUi?.Hide();
        devSandbox?.OpenDevMenuFromGameplay(false);
        promotionController?.ClearPendingState();
        stockfishService?.CancelThinking();
        gameStateController?.ResignCurrentPlayer();
    }

    void OnGameEnded(ChessGameEndResult result)
    {
        CloseConfirm();
        openedFromPauseMenu = false;
        pauseMenuUi?.Hide();
        selectionController?.Deselect();
        promotionController?.ClearPendingState();
        devSandbox?.OpenDevMenuFromGameplay(false);
        RefreshButtonState();

        string title;
        if (result.WinningTeam.HasValue && result.LosingTeam.HasValue && turnManager != null && turnManager.IsHumanTurn(result.LosingTeam.Value))
        {
            title = "You Lose";
        }
        else if (result.WinningTeam.HasValue && turnManager != null && turnManager.IsHumanTurn(result.WinningTeam.Value))
        {
            title = "You Win";
        }
        else if (result.WinningTeam.HasValue)
        {
            title = $"{result.WinningTeam.Value} Wins";
        }
        else
        {
            title = "Draw";
        }

        if (result.WinningTeam.HasValue)
        {
            if (result.FinalState == ChessGameState.Resignation)
            {
                Debug.Log($"[ChessResignUiController] Resign confirmed, winner = {result.WinningTeam.Value}.");
            }

            winScreen?.ShowWin(title, result.Reason, $"{result.WinningTeam.Value} wins");
            return;
        }

        winScreen?.ShowDraw(result.Reason);
    }

    void RefreshButtonState()
    {
        if (gameStateController == null)
        {
            return;
        }

        if (!gameStateController.IsGameplayActive())
        {
            CloseConfirm();
        }
    }

    #endregion

    #region Factories

    static GameObject CreatePanelObject(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 anchoredPos)
    {
        GameObject panel = new(name);
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        return panel;
    }

    static TextMeshProUGUI CreateLabel(string name, Transform parent, string text, float fontSize, Vector2? anchor = null, Vector2? anchoredPos = null)
    {
        GameObject labelObject = new(name);
        labelObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        RectTransform rect = label.rectTransform;
        Vector2 resolvedAnchor = anchor ?? new Vector2(0.5f, 0.5f);
        rect.anchorMin = resolvedAnchor;
        rect.anchorMax = resolvedAnchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos ?? Vector2.zero;
        rect.sizeDelta = new Vector2(400f, 56f);
        return label;
    }

    static Button CreateActionButton(Transform parent, string name, string labelText, Vector2 anchoredPos)
    {
        GameObject buttonObject = CreatePanelObject(name, parent, new Vector2(180f, 48f), new Vector2(0.5f, 0.5f), anchoredPos);
        buttonObject.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        TextMeshProUGUI label = CreateLabel("Label", buttonObject.transform, labelText, 22);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        return button;
    }

    #endregion
}
