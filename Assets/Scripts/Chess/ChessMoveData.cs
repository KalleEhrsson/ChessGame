using UnityEngine;

public readonly struct ChessMoveData
{
    #region Fields

    public ChessPiece Piece { get; }
    public ChessTile FromTile { get; }
    public ChessTile ToTile { get; }
    public bool IsCapture { get; }
    public ChessTile CaptureTile { get; }
    public bool IsCastle { get; }
    public ChessTile CastleRookFrom { get; }
    public ChessTile CastleRookTo { get; }
    public bool IsEnPassant { get; }
    public bool IsPromotion { get; }
    public PieceType PromotionPieceType { get; }

    #endregion

    #region Init

    public ChessMoveData(
        ChessPiece piece,
        ChessTile fromTile,
        ChessTile toTile,
        bool isCapture = false,
        ChessTile captureTile = null,
        bool isCastle = false,
        ChessTile castleRookFrom = null,
        ChessTile castleRookTo = null,
        bool isEnPassant = false,
        bool isPromotion = false,
        PieceType promotionPieceType = PieceType.Queen)
    {
        Piece = piece;
        FromTile = fromTile;
        ToTile = toTile;
        IsCapture = isCapture;
        CaptureTile = captureTile;
        IsCastle = isCastle;
        CastleRookFrom = castleRookFrom;
        CastleRookTo = castleRookTo;
        IsEnPassant = isEnPassant;
        IsPromotion = isPromotion;
        PromotionPieceType = promotionPieceType;
    }

    #endregion
}
