using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class ChessUIFactory
{
    public static GameObject CreateFullscreenOverlay(string name, Transform parent)
    {
        GameObject overlay = new(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        overlay.transform.SetParent(parent, false);
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = ChessUITheme.MainBackground;
        return overlay;
    }

    public static RectTransform CreatePanel(string name, Transform parent, Vector2 size, Vector2 anchor)
    {
        GameObject panel = new(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        panel.GetComponent<Image>().color = ChessUITheme.Panel;
        return rect;
    }

    public static TMP_Text CreateText(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, bool wrap = false)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        ChessUITheme.ApplyTextStyle(text, size, ChessUITheme.MainText, alignment, wrap);
        return text;
    }

    public static Button CreateButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredHeight = ChessUITheme.StandardButtonHeight;
        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        GameObject labelGo = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        RectTransform lr = labelGo.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(16f, 6f);
        lr.offsetMax = new Vector2(-16f, -6f);

        TMP_Text txt = labelGo.GetComponent<TextMeshProUGUI>();
        txt.text = label;
        ChessUITheme.ApplyButtonStyle(button, txt);
        return button;
    }

    public static IEnumerator AnimateOpen(CanvasGroup group, RectTransform panel)
    {
        float t = 0f;
        panel.localScale = new Vector3(0.95f, 0.95f, 1f);
        group.alpha = 0f;
        while (t < ChessUITheme.FadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / ChessUITheme.FadeDuration);
            group.alpha = k;
            panel.localScale = Vector3.Lerp(new Vector3(0.95f, 0.95f, 1f), Vector3.one, k);
            yield return null;
        }
    }
}
