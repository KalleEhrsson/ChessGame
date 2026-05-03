using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class ChessUITheme
{
    public static readonly Color MainBackground = new(0.03f, 0.04f, 0.07f, 0.78f);
    public static readonly Color Panel = new(0.08f, 0.09f, 0.13f, 0.95f);
    public static readonly Color PanelSecondary = new(0.11f, 0.12f, 0.16f, 0.95f);
    public static readonly Color GoldAccent = new(0.88f, 0.73f, 0.34f, 1f);
    public static readonly Color Warning = new(0.92f, 0.64f, 0.26f, 1f);
    public static readonly Color Error = new(0.79f, 0.28f, 0.27f, 1f);
    public static readonly Color Success = new(0.44f, 0.8f, 0.58f, 1f);
    public static readonly Color MutedText = new(0.78f, 0.8f, 0.85f, 1f);
    public static readonly Color MainText = new(0.96f, 0.95f, 0.9f, 1f);
    public static readonly Color ButtonNormal = new(0.17f, 0.19f, 0.25f, 1f);
    public static readonly Color ButtonHover = new(0.27f, 0.3f, 0.37f, 1f);
    public static readonly Color ButtonPressed = new(0.12f, 0.13f, 0.17f, 1f);
    public static readonly Color ButtonDisabled = new(0.2f, 0.2f, 0.22f, 0.65f);

    public const float BorderThickness = 2f;
    public const float StandardButtonHeight = 56f;
    public const int TitleSize = 56;
    public const int HeaderSize = 30;
    public const int BodySize = 24;
    public const float FadeDuration = 0.2f;

    public static TMP_FontAsset ResolveFont() => TMP_Settings.defaultFontAsset;

    public static void ApplyTextStyle(TMP_Text text, float size, Color color, TextAlignmentOptions alignment, bool wrap = false)
    {
        text.font = ResolveFont();
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
        text.enableAutoSizing = false;
    }

    public static void ApplyButtonStyle(Button button, TMP_Text label)
    {
        Image image = button.GetComponent<Image>();
        image.color = ButtonNormal;
        ColorBlock cb = button.colors;
        cb.normalColor = ButtonNormal;
        cb.highlightedColor = ButtonHover;
        cb.selectedColor = ButtonHover;
        cb.pressedColor = ButtonPressed;
        cb.disabledColor = ButtonDisabled;
        cb.fadeDuration = 0.08f;
        button.colors = cb;
        ApplyTextStyle(label, BodySize, MainText, TextAlignmentOptions.Center, false);
    }
}
