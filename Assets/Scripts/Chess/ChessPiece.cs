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

        transform.position = CurrentTile.transform.position;
    }

    public void ClearTileReference()
    {
        CurrentTile = null;
    }

    #endregion
}
