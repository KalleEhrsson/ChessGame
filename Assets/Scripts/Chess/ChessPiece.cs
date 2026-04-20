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

    #endregion

    #region Variables

    [SerializeField] bool faceOpponentSide = true;
    [SerializeField] float rotationYawOffset = 0f;
    [SerializeField] Color selectedTint = new(1f, 0.93f, 0.35f, 1f);
    [SerializeField] float selectedEmissionIntensity = 0.5f;

    Renderer[] cachedRenderers = System.Array.Empty<Renderer>();
    MaterialPropertyBlock[] propertyBlocks = System.Array.Empty<MaterialPropertyBlock>();
    Color[] originalColors = System.Array.Empty<Color>();
    Color[] originalEmissionColors = System.Array.Empty<Color>();
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
    }

    public void SetTile(ChessTile tile)
    {
        if (CurrentTile == tile)
        {
            SnapToTile();
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

        SnapToTile();
    }

    public void SnapToTile()
    {
        if (CurrentTile == null)
        {
            return;
        }

        Vector3 targetPosition = CurrentTile.transform.position;
        targetPosition.y = ResolvePlacementY(CurrentTile, targetPosition.y);
        transform.position = targetPosition;

        if (faceOpponentSide)
        {
            RotateTowardOpponentSide();
        }
    }

    public void ClearTileReference()
    {
        CurrentTile = null;
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
    
    public void RotateTowardPosition(Vector3 targetPosition)
    {
        Vector3 facingDirection = targetPosition - transform.position;
        facingDirection.y = 0f;
        if (facingDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
        if (!Mathf.Approximately(rotationYawOffset, 0f))
        {
            targetRotation *= Quaternion.Euler(0f, rotationYawOffset, 0f);
        }

        transform.rotation = targetRotation;
    }

    public void RotateTowardPosition(Vector3 targetPosition)
    {
        Vector3 facingDirection = targetPosition - transform.position;
        facingDirection.y = 0f;
        if (facingDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
        if (!Mathf.Approximately(rotationYawOffset, 0f))
        {
            targetRotation *= Quaternion.Euler(0f, rotationYawOffset, 0f);
        }

        transform.rotation = targetRotation;
    }

    float ResolvePlacementY(ChessTile tile, float fallbackY)
    {
        float boardTop = ResolveTileTopY(tile, fallbackY);
        if (!TryGetPieceBounds(out Bounds pieceBounds))
        {
            return boardTop;
        }

        float bottomOffset = transform.position.y - pieceBounds.min.y;
        return boardTop + bottomOffset;
    }

    float ResolveTileTopY(ChessTile tile, float fallbackY)
    {
        if (tile == null)
        {
            return fallbackY;
        }

        Renderer tileRenderer = tile.GetComponent<Renderer>();
        if (tileRenderer != null)
        {
            return tileRenderer.bounds.max.y;
        }

        Collider tileCollider = tile.GetComponent<Collider>();
        if (tileCollider != null)
        {
            return tileCollider.bounds.max.y;
        }

        return fallbackY;
    }

    bool TryGetPieceBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
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
