using UnityEngine;

public enum PieceType
{
    Pawn,
    Rook,
    Knight,
    Bishop,
    Queen,
    King
}

public enum PieceTeam
{
    White,
    Black
}

[DisallowMultipleComponent]
public class ChessPiece : MonoBehaviour
{
    #region Properties

    [field: SerializeField] public PieceType Type { get; private set; }
    [field: SerializeField] public PieceTeam Team { get; private set; }
    [field: SerializeField] public ChessTile CurrentTile { get; private set; }
    [field: SerializeField] public bool HasMoved { get; private set; }

    #endregion

    #region Variables

    [SerializeField] bool faceOpponentSide = true;
    [SerializeField] float rotationYawOffset = 90f;
    [SerializeField] Color selectedTint = new(1f, 0.93f, 0.35f, 1f);
    [SerializeField] float selectedEmissionIntensity = 0.5f;

    Renderer[] cachedRenderers = System.Array.Empty<Renderer>();
    MaterialPropertyBlock[] propertyBlocks = System.Array.Empty<MaterialPropertyBlock>();
    Color[] originalColors = System.Array.Empty<Color>();
    Color[] originalEmissionColors = System.Array.Empty<Color>();
    ChessPieceMotion motionComponent;
    bool visualsCached;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    #endregion

    #region Setup

    public void SetIdentity(PieceTeam team, PieceType type)
    {
        Team = team;
        Type = type;
        HasMoved = false;
    }

    public void SetType(PieceType type)
    {
        Type = type;
    }

    public void MarkMoved()
    {
        HasMoved = true;
    }

    public void ResetMovedState()
    {
        HasMoved = false;
    }

    public void SetTile(ChessTile tile, bool updateTransform = true)
    {
        if (CurrentTile == tile)
        {
            if (updateTransform)
            {
                SnapToTile();
            }
            return;
        }

        if (CurrentTile != null && CurrentTile.CurrentPiece == this)
        {
            CurrentTile.SetCurrentPiece(null);
        }

        if (tile != null && tile.CurrentPiece != null && tile.CurrentPiece != this)
        {
            tile.CurrentPiece.ClearTileReference();
        }

        CurrentTile = tile;

        if (CurrentTile != null)
        {
            CurrentTile.SetCurrentPiece(this);
        }

        if (updateTransform)
        {
            SnapToTile();
        }
    }

    public void SnapToTile()
    {
        if (CurrentTile == null)
        {
            return;
        }

        GroundOnTile(CurrentTile);

        if (faceOpponentSide)
        {
            RotateTowardOpponentSide();
        }
    }

    public bool GroundOnTile(ChessTile tile)
    {
        if (tile == null)
        {
            return false;
        }
        ChessBoard board = ChessBoard.Instance;
        if (board == null)
        {
            Debug.LogWarning($"[ChessPiece] GroundOnTile failed for '{name}'. Reason=missing ChessBoard instance.", this);
            return false;
        }

        return board.TrySnapPieceToTile(this, tile);
    }

    public void ClearTileReference()
    {
        CurrentTile = null;
    }

    public ChessPieceMotion GetOrAddMotion()
    {
        if (motionComponent == null)
        {
            motionComponent = GetComponent<ChessPieceMotion>();
        }

        if (motionComponent == null)
        {
            motionComponent = gameObject.AddComponent<ChessPieceMotion>();
        }

        return motionComponent;
    }

    public void SetSelected(bool selected)
    {
        EnsureVisualCache();

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            MaterialPropertyBlock block = propertyBlocks[i];
            renderer.GetPropertyBlock(block);

            Color baseColor = selected ? Color.Lerp(originalColors[i], selectedTint, 0.7f) : originalColors[i];
            Color emissionColor = selected
                ? originalEmissionColors[i] + (selectedTint * selectedEmissionIntensity)
                : originalEmissionColors[i];

            block.SetColor(ColorId, baseColor);
            block.SetColor(BaseColorId, baseColor);
            block.SetColor(EmissionColorId, emissionColor);
            renderer.SetPropertyBlock(block);
        }
    }

    #endregion

    #region Placement

    void RotateTowardOpponentSide()
    {
        ChessBoard board = ChessBoard.Instance;
        if (board == null || !board.TryGetTeamFacingDirection(Team, out Vector3 facingDirection))
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
        if (!Mathf.Approximately(rotationYawOffset, 0f))
        {
            targetRotation *= Quaternion.Euler(0f, rotationYawOffset, 0f);
        }

        transform.rotation = targetRotation;
    }
    
    public bool TryGetBottomY(out float bottomY)
    {
        bottomY = transform.position.y;
        Bounds bounds = default;
        bool hasBounds = false;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            Bounds colliderBounds = collider.bounds;
            if (!IsFinite(colliderBounds))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = colliderBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(colliderBounds);
            }
        }

        if (hasBounds)
        {
            bottomY = bounds.min.y;
            return IsFinite(bottomY);
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            if (!IsFinite(rendererBounds))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = rendererBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(rendererBounds);
            }
        }

        if (hasBounds)
        {
            bottomY = bounds.min.y;
            return IsFinite(bottomY);
        }

        return false;
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    static bool IsFinite(Bounds bounds)
    {
        return IsFinite(bounds.min) && IsFinite(bounds.max);
    }

    void EnsureVisualCache()
    {
        if (visualsCached)
        {
            return;
        }

        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        propertyBlocks = new MaterialPropertyBlock[cachedRenderers.Length];
        originalColors = new Color[cachedRenderers.Length];
        originalEmissionColors = new Color[cachedRenderers.Length];

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            propertyBlocks[i] = new MaterialPropertyBlock();

            if (renderer == null || renderer.sharedMaterial == null)
            {
                originalColors[i] = Color.white;
                originalEmissionColors[i] = Color.black;
                continue;
            }

            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial.HasProperty(BaseColorId))
            {
                originalColors[i] = sharedMaterial.GetColor(BaseColorId);
            }
            else if (sharedMaterial.HasProperty(ColorId))
            {
                originalColors[i] = sharedMaterial.GetColor(ColorId);
            }
            else
            {
                originalColors[i] = Color.white;
            }

            if (sharedMaterial.HasProperty(EmissionColorId))
            {
                originalEmissionColors[i] = sharedMaterial.GetColor(EmissionColorId);
            }
            else
            {
                originalEmissionColors[i] = Color.black;
            }
        }

        visualsCached = true;
    }

    #endregion
}
