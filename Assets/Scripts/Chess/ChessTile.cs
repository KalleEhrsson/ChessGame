using UnityEngine;

[DisallowMultipleComponent]
public class ChessTile : MonoBehaviour
{
    #region Properties

    public int X { get; private set; } = -1;
    public int Y { get; private set; } = -1;
    public string TileName { get; private set; } = string.Empty;

    #endregion

    #region Variables

    Renderer cachedRenderer;
    Color originalColor;
    bool hasOriginalColor;

    #endregion

    #region Unity

    void Awake()
    {
        CacheRenderer();
    }

    #endregion

    #region Setup

    public void SetCoordinates(int x, int y)
    {
        X = x;
        Y = y;
        TileName = BuildTileName(x, y);
        gameObject.name = TileName;
    }

    static string BuildTileName(int x, int y)
    {
        char file = (char)('A' + x);
        int rank = y + 1;
        return $"{file}{rank}";
    }

    void CacheRenderer()
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
            if (cachedRenderer != null)
            {
                originalColor = cachedRenderer.material.color;
                hasOriginalColor = true;
            }
        }
    }

    #endregion

    #region Visuals

    public void Highlight(Color color)
    {
        CacheRenderer();
        if (cachedRenderer == null)
        {
            return;
        }

        cachedRenderer.material.color = color;
    }

    public void ResetColor()
    {
        CacheRenderer();
        if (cachedRenderer == null || !hasOriginalColor)
        {
            return;
        }

        cachedRenderer.material.color = originalColor;
    }

    #endregion
}
