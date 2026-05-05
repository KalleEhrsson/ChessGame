using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PromotionSelectionUI : MonoBehaviour
{
    const string PopupRootName = "PromotionPopupRoot";
    const string PopupPanelName = "PromotionPopupPanel";
    const string RookButtonName = "PromotionRookButton";
    const string BishopButtonName = "PromotionBishopButton";
    const string KnightButtonName = "PromotionKnightButton";
    const string QueenButtonName = "PromotionQueenButton";

    static readonly PieceType[] ButtonOrder = { PieceType.Rook, PieceType.Bishop, PieceType.Knight, PieceType.Queen };

    public static PromotionSelectionUI GetOrCreate()
    {
        PromotionSelectionUI existing = FindFirstObjectByType<PromotionSelectionUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            return existing;
        }

        GameObject host = new("PromotionSelectionUI");
        return host.AddComponent<PromotionSelectionUI>();
    }

    readonly Dictionary<PieceType, Button> buttonsByType = new();
    RectTransform popupRoot;
    RectTransform popupPanel;
    CanvasGroup popupCanvasGroup;
    Action<PieceType> selectionCallback;

    public bool IsVisible => popupRoot != null && popupRoot.gameObject.activeSelf;

    void Awake()
    {
        EnsureBuilt();
        SetVisible(false);
    }

    public void Show(Action<PieceType> onSelection)
    {
        EnsureBuilt();
        selectionCallback = onSelection;
        SetVisible(true);
        FocusButton(PieceType.Queen);
    }

    public void Hide()
    {
        selectionCallback = null;
        SetVisible(false);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    void EnsureBuilt()
    {
        Canvas canvas = ChessMasterCanvas.GetOrCreateCanvas();
        EnsureEventSystemSupportsInputSystem();
        Transform existingRoot = canvas.transform.Find(PopupRootName);
        if (existingRoot != null)
        {
            popupRoot = existingRoot as RectTransform;
            popupPanel = popupRoot.Find(PopupPanelName) as RectTransform;
            popupCanvasGroup = popupRoot.GetComponent<CanvasGroup>() ?? popupRoot.gameObject.AddComponent<CanvasGroup>();
        }
        else
        {
            GameObject rootObject = new(PopupRootName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            popupRoot = rootObject.GetComponent<RectTransform>();
            popupRoot.SetParent(canvas.transform, false);
            popupRoot.anchorMin = Vector2.zero;
            popupRoot.anchorMax = Vector2.one;
            popupRoot.offsetMin = Vector2.zero;
            popupRoot.offsetMax = Vector2.zero;

            Image blocker = rootObject.GetComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.45f);
            blocker.raycastTarget = true;

            popupCanvasGroup = rootObject.GetComponent<CanvasGroup>();
        }

        if (popupPanel == null)
        {
            GameObject panelObject = new(PopupPanelName, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            popupPanel = panelObject.GetComponent<RectTransform>();
            popupPanel.SetParent(popupRoot, false);
            popupPanel.anchorMin = new Vector2(0.5f, 0.5f);
            popupPanel.anchorMax = new Vector2(0.5f, 0.5f);
            popupPanel.pivot = new Vector2(0.5f, 0.5f);
            popupPanel.anchoredPosition = Vector2.zero;
            popupPanel.sizeDelta = new Vector2(520f, 220f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.14f, 0.14f, 0.18f, 0.97f);

            HorizontalLayoutGroup layout = panelObject.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 18, 18);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
        }

        EnsureButton(PieceType.Rook, "Rook", RookButtonName);
        EnsureButton(PieceType.Bishop, "Bishop", BishopButtonName);
        EnsureButton(PieceType.Knight, "Knight", KnightButtonName);
        EnsureButton(PieceType.Queen, "Queen", QueenButtonName);
    }

    void EnsureButton(PieceType type, string label, string objectName)
    {
        if (buttonsByType.ContainsKey(type) && buttonsByType[type] != null)
        {
            return;
        }

        Transform existing = popupPanel.Find(objectName);
        if (existing != null && existing.TryGetComponent(out Button existingButton))
        {
            buttonsByType[type] = existingButton;
            return;
        }

        GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(popupPanel, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.9f, 0.9f, 0.95f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        PieceType capturedType = type;
        button.onClick.AddListener(() => OnOptionSelected(capturedType));

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 110f;
        layout.preferredHeight = 150f;

        CreateButtonLabel(buttonObject.transform, label);
        buttonsByType[type] = button;
    }

    void CreateButtonLabel(Transform parent, string label)
    {
        if (parent.Find("Label") != null)
        {
            return;
        }

        bool hasTmp = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro") != null;
        GameObject labelObject = new("Label", typeof(RectTransform));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(parent, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        if (hasTmp)
        {
            TextMeshProUGUI tmp = labelObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 30f;
            tmp.color = Color.black;
        }
        else
        {
            Text text = labelObject.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 30;
            text.color = Color.black;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    static void EnsureEventSystemSupportsInputSystem()
    {
        EventSystem current = EventSystem.current;
        if (current != null && current.GetComponent<InputSystemUIInputModule>() == null)
        {
            current.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    void SetVisible(bool visible)
    {
        if (popupRoot != null)
        {
            popupRoot.gameObject.SetActive(visible);
            if (popupCanvasGroup != null)
            {
                popupCanvasGroup.interactable = visible;
                popupCanvasGroup.blocksRaycasts = visible;
                popupCanvasGroup.alpha = visible ? 1f : 0f;
            }
        }
    }

    void FocusButton(PieceType pieceType)
    {
        if (EventSystem.current == null)
        {
            return;
        }

        for (int i = 0; i < ButtonOrder.Length; i++)
        {
            PieceType candidate = i == 0 ? pieceType : ButtonOrder[i];
            if (buttonsByType.TryGetValue(candidate, out Button button) && button != null)
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                return;
            }
        }
    }

    void OnOptionSelected(PieceType type)
    {
        Action<PieceType> callback = selectionCallback;
        callback?.Invoke(type);
    }
}
