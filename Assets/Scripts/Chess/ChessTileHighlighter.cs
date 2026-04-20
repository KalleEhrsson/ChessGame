using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChessTileHighlighter : MonoBehaviour
{
    #region Singleton

    public static ChessTileHighlighter Instance { get; private set; }

    public static ChessTileHighlighter GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessTileHighlighter existing = FindFirstObjectByType<ChessTileHighlighter>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject host = new("ChessTileHighlighter");
        Instance = host.AddComponent<ChessTileHighlighter>();
        return Instance;
    }

    #endregion

    #region Variables

    [SerializeField] Color moveColor = new(0.25f, 0.85f, 0.25f, 1f);
    [SerializeField] Color captureColor = new(0.9f, 0.25f, 0.25f, 1f);

    readonly List<ChessTile> activeMoveTiles = new(32);
    readonly List<ChessTile> activeCaptureTiles = new(32);

    #endregion

    #region Unity

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    #endregion

    #region API

    public void Highlight(IReadOnlyList<ChessTile> moveTiles, IReadOnlyList<ChessTile> captureTiles)
    {
        ClearAllHighlights();

        ApplyGroup(moveTiles, activeMoveTiles, moveColor, ChessTile.HighlightState.Move);
        ApplyGroup(captureTiles, activeCaptureTiles, captureColor, ChessTile.HighlightState.Capture);
    }

    public void ClearAllHighlights()
    {
        ClearGroup(activeMoveTiles);
        ClearGroup(activeCaptureTiles);
    }

    #endregion

    #region Internals

    void ApplyGroup(IReadOnlyList<ChessTile> source, List<ChessTile> destination, Color color, ChessTile.HighlightState state)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            ChessTile tile = source[i];
            if (tile == null)
            {
                continue;
            }

            tile.SetHighlightState(state, color);
            destination.Add(tile);
        }
    }

    void ClearGroup(List<ChessTile> group)
    {
        for (int i = 0; i < group.Count; i++)
        {
            ChessTile tile = group[i];
            if (tile == null)
            {
                continue;
            }

            tile.ClearHighlightState();
        }

        group.Clear();
    }

    #endregion
}
