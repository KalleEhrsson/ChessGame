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

    readonly bool faceOpponentSide = true;
    readonly float rotationYawOffset = 90f;

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

    private void SnapToTile()
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

    private void ClearTileReference()
    {
        CurrentTile = null;
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

    #endregion
}
