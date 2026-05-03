using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PromotionSelectionUI : MonoBehaviour
{
    #region Variables

    Canvas rootCanvas;
    RectTransform panel;
    readonly Dictionary<PieceType, Button> buttonsByType = new();
    Action<PieceType> selectionCallback;

    #endregion

    #region Properties

    public bool IsVisible => panel != null && panel.gameObject.activeSelf;

    #endregion

    #region Unity

    void Awake()
    {
        EnsureBuilt();
        SetVisible(false);
    }

    void Update()
    {
        if (!IsVisible)
        {
            return;
        }

        if (TryHandleHotkey(PieceType.Queen, Key.Digit1, Key.Q))
        {
            return;
        }

        if (TryHandleHotkey(PieceType.Rook, Key.Digit2, Key.R))
        {
            return;
        }

        if (TryHandleHotkey(PieceType.Bishop, Key.Digit3, Key.B))
        {
            return;
        }

        TryHandleHotkey(PieceType.Knight, Key.Digit4, Key.N);
    }

    #endregion

    #region API

    public void Show(Action<PieceType> onSelection)
    {
        EnsureBuilt();
        selectionCallback = onSelection;
        SetVisible(true);
        FocusFirstButton();
    }

    public void Hide()
    {
        selectionCallback = null;
        SetVisible(false);
        EventSystem currentEventSystem = EventSystem.current;
        if (currentEventSystem != null)
        {
            currentEventSystem.SetSelectedGameObject(null);
        }
    }

    #endregion

    #region Setup

    void EnsureBuilt()
    {
        EnsureEventSystem();
        EnsureCanvas();
        EnsurePanel();

        EnsureOptionButton(PieceType.Queen, "Queen", new Color(0.95f, 0.95f, 0.95f, 1f));
        EnsureOptionButton(PieceType.Rook, "Rook", new Color(0.9f, 0.9f, 0.92f, 1f));
        EnsureOptionButton(PieceType.Bishop, "Bishop", new Color(0.9f, 0.92f, 0.9f, 1f));
        EnsureOptionButton(PieceType.Knight, "Knight", new Color(0.92f, 0.9f, 0.9f, 1f));
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    void EnsureCanvas()
    {
        if (rootCanvas != null)
        {
            return;
        }

        Canvas existingCanvas = FindFirstObjectByType<Canvas>();
        if (existingCanvas != null)
        {
            rootCanvas = existingCanvas;
            return;
        }

        GameObject canvasObject = new("PromotionCanvas");
        rootCanvas = canvasObject.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
    }

    void EnsurePanel()
    {
        if (panel != null)
        {
            return;
        }

        Transform existing = rootCanvas.transform.Find("PromotionSelectionPanel");
        if (existing != null)
        {
            panel = existing as RectTransform;
            return;
        }

        GameObject panelObject = new("PromotionSelectionPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel = panelObject.GetComponent<RectTransform>();
        panel.SetParent(rootCanvas.transform, false);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject titleObject = new("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.SetParent(panel, false);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);

        TextMeshProUGUI titleText = titleObject.GetComponent<TextMeshProUGUI>();
        titleText.text = "Promote Pawn";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 24;
        titleText.color = new Color(0.95f, 0.9f, 0.6f, 1f);

        LayoutElement titleLayout = titleObject.AddComponent<LayoutElement>();
        titleLayout.preferredWidth = 260f;
        titleLayout.preferredHeight = 34f;
    }

    void EnsureOptionButton(PieceType type, string label, Color fillColor)
    {
        if (buttonsByType.ContainsKey(type))
        {
            return;
        }

        string objectName = $"{label}Button";
        Transform existing = panel.Find(objectName);
        if (existing != null && existing.TryGetComponent(out Button existingButton))
        {
            buttonsByType[type] = existingButton;
            return;
        }

        GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(panel, false);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = fillColor;

        Button button = buttonObject.GetComponent<Button>();
        PieceType capturedType = type;
        button.onClick.AddListener(() => OnOptionSelected(capturedType));

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 260f;
        layout.preferredHeight = 46f;

        ColorBlock colors = button.colors;
        colors.highlightedColor = fillColor * 0.9f;
        colors.pressedColor = fillColor * 0.8f;
        colors.selectedColor = fillColor * 0.92f;
        button.colors = colors;

        GameObject textObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(buttonRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 20;
        text.color = ChessUITheme.MainText;

        buttonsByType[type] = button;
    }

    #endregion

    #region Helpers

    void SetVisible(bool isVisible)
    {
        if (panel != null)
        {
            panel.gameObject.SetActive(isVisible);
        }
    }

    void FocusFirstButton()
    {
        EventSystem currentEventSystem = EventSystem.current;
        if (currentEventSystem == null)
        {
            return;
        }

        if (buttonsByType.TryGetValue(PieceType.Queen, out Button queenButton) && queenButton != null)
        {
            currentEventSystem.SetSelectedGameObject(queenButton.gameObject);
        }
    }

    bool TryHandleHotkey(PieceType type, Key numberKey, Key letterKey)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        if (!(keyboard[numberKey].wasPressedThisFrame || keyboard[letterKey].wasPressedThisFrame))
        {
            return false;
        }

        Debug.Log($"[PromotionSelectionUI] Hotkey selected: {type}");
        OnOptionSelected(type);
        return true;
    }

    void OnOptionSelected(PieceType type)
    {
        if (!IsVisible)
        {
            return;
        }

        Action<PieceType> callback = selectionCallback;
        Hide();
        callback?.Invoke(type);
    }

    #endregion
}
