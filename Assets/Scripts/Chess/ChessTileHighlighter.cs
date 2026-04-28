using System.Collections.Generic;
using System.Threading.Tasks;
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
    [SerializeField, Range(1f, 1.3f)] float clickPulseIntensity = 1.12f;
    [SerializeField] float clickPulseDuration = 0.12f;

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

    public void TriggerValidTileFeedback(ChessTile tile)
    {
        if (tile == null)
        {
            return;
        }

        bool isCaptureTile = activeCaptureTiles.Contains(tile);
        Color baseColor = isCaptureTile ? captureColor : moveColor;
        _ = PulseTileAsync(tile, baseColor);
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

    async Task PulseTileAsync(ChessTile tile, Color baseColor)
    {
        if (tile == null)
        {
            return;
        }

        bool wasCapture = activeCaptureTiles.Contains(tile);
        ChessTile.HighlightState highlightState = wasCapture ? ChessTile.HighlightState.Capture : ChessTile.HighlightState.Move;
        Color pulseColor = Color.Lerp(baseColor, Color.white, clickPulseIntensity - 1f);
        tile.SetHighlightState(highlightState, pulseColor);

        float elapsed = 0f;
        float duration = Mathf.Max(0.04f, clickPulseDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            await Task.Yield();
        }

        if (tile == null)
        {
            return;
        }

        bool isActiveMoveTile = activeMoveTiles.Contains(tile);
        bool isActiveCaptureTile = activeCaptureTiles.Contains(tile);
        
        if (!isActiveMoveTile && !isActiveCaptureTile)
        {
            tile.ClearHighlightState();
            return;
        }
        
        ChessTile.HighlightState state = isActiveCaptureTile ? ChessTile.HighlightState.Capture : ChessTile.HighlightState.Move;
        tile.SetHighlightState(state, isActiveCaptureTile ? captureColor : moveColor);
    }

    #endregion
}
