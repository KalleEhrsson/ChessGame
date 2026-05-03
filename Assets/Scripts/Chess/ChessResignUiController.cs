using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

        ChessResignUiController existing = FindFirstObjectByType<ChessResignUiController>();
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureUi();
            return Instance;
        }

        GameObject host = new("ChessResignUiController");
        Instance = host.AddComponent<ChessResignUiController>();
        return Instance;
    }

    #endregion

    #region Variables

    ChessGameStateController gameStateController;
    ChessTurnManager turnManager;
    ChessSelectionController selectionController;
    ChessWinScreenUI winScreen;
    Canvas rootCanvas;
    Button resignButton;
    GameObject confirmPanel;

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
        EnsureEventSystem();
        EnsureResignButton();
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
        gameStateController.GameEnded -= OnGameEnded;
        gameStateController.GameEnded += OnGameEnded;
    }

    void EnsureCanvas()
    {
        if (rootCanvas != null)
        {
            return;
        }

        rootCanvas = FindFirstObjectByType<Canvas>();
        if (rootCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new("ChessRuntimeCanvas");
        rootCanvas = canvasObject.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    void EnsureResignButton()
    {
        if (resignButton != null)
        {
            return;
        }

        GameObject buttonObject = CreatePanelObject("ResignButton", rootCanvas.transform, new Vector2(180f, 48f), new Vector2(1f, 0f), new Vector2(-18f, 18f));
        buttonObject.AddComponent<Image>().color = new Color(0.75f, 0.2f, 0.2f, 0.95f);
        resignButton = buttonObject.AddComponent<Button>();
        resignButton.onClick.AddListener(OpenConfirm);

        TextMeshProUGUI label = CreateLabel("Label", buttonObject.transform, "Resign", 26);
        label.alignment = TextAlignmentOptions.Center;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    void EnsureConfirmPanel()
    {
        if (confirmPanel != null)
        {
            return;
        }

        confirmPanel = CreatePanelObject("ResignConfirmPanel", rootCanvas.transform, new Vector2(460f, 240f), new Vector2(0.5f, 0.5f), Vector2.zero);
        confirmPanel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        confirmPanel.SetActive(false);

        CreateLabel("Title", confirmPanel.transform, "Resign?", 38, new Vector2(0.5f, 0.78f), Vector2.zero);
        CreateLabel("Body", confirmPanel.transform, "Are you sure you want to resign?", 24, new Vector2(0.5f, 0.58f), Vector2.zero);

        Button yesButton = CreateActionButton(confirmPanel.transform, "YesButton", "Yes, resign", new Vector2(-105f, -74f));
        yesButton.onClick.AddListener(ConfirmResign);

        Button cancelButton = CreateActionButton(confirmPanel.transform, "CancelButton", "Cancel", new Vector2(105f, -74f));
        cancelButton.onClick.AddListener(CloseConfirm);
    }

    #endregion

    #region UI Actions

    public void OpenConfirmFromPauseMenu()
    {
        OpenConfirm();
    }

    public bool IsConfirmOpen()
    {
        return confirmPanel != null && confirmPanel.activeSelf;
    }

    public void CloseConfirmFromPauseMenu()
    {
        CloseConfirm();
    }

    void OpenConfirm()
    {
        if (gameStateController == null || !gameStateController.IsGameplayActive())
        {
            return;
        }

        confirmPanel?.SetActive(true);
    }

    void CloseConfirm()
    {
        confirmPanel?.SetActive(false);
    }

    void ConfirmResign()
    {
        CloseConfirm();
        gameStateController?.ResignCurrentPlayer();
    }

    void OnGameEnded(ChessGameEndResult result)
    {
        CloseConfirm();
        selectionController?.Deselect();
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
            winScreen?.ShowWin(title, result.Reason, $"{result.WinningTeam.Value} wins");
            return;
        }

        winScreen?.ShowDraw(result.Reason);
    }

    void RefreshButtonState()
    {
        if (resignButton == null || gameStateController == null)
        {
            return;
        }

        bool active = gameStateController.IsGameplayActive();
        resignButton.interactable = active;
        resignButton.gameObject.SetActive(active);
        if (!active)
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
