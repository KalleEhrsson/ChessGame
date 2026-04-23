using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChessMoveValidator : MonoBehaviour
{
    #region Singleton

    public static ChessMoveValidator Instance { get; private set; }

    public static ChessMoveValidator GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessMoveValidator existing = FindFirstObjectByType<ChessMoveValidator>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject host = new("ChessMoveValidator");
        Instance = host.AddComponent<ChessMoveValidator>();
        return Instance;
    }

    #endregion

    #region Variables

    ChessBoard board;
    ChessMoveGenerator moveGenerator;

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
        ResolveSystems();
    }

    #endregion

    #region API

    public void GenerateLegalMoves(ChessPiece piece, out List<ChessTile> legalMoveTiles, out List<ChessTile> legalCaptureTiles)
    {
        legalMoveTiles = new List<ChessTile>(16);
        legalCaptureTiles = new List<ChessTile>(16);

        if (piece == null || piece.CurrentTile == null)
        {
            return;
        }

        ResolveSystems();
        if (board == null || moveGenerator == null)
        {
            return;
        }

        List<ChessMoveData> legalMoves = GenerateLegalMovesData(piece);
        for (int i = 0; i < legalMoves.Count; i++)
        {
            ChessMoveData move = legalMoves[i];
            if (move.IsCapture)
            {
                if (!legalCaptureTiles.Contains(move.ToTile))
                {
                    legalCaptureTiles.Add(move.ToTile);
                }

                continue;
            }

            if (!legalMoveTiles.Contains(move.ToTile))
            {
                legalMoveTiles.Add(move.ToTile);
            }
        }
    }

    public bool IsMoveLegalDestination(ChessPiece piece, ChessTile destination)
    {
        if (piece == null || destination == null)
        {
            return false;
        }

        GenerateLegalMoves(piece, out List<ChessTile> legalMoveTiles, out List<ChessTile> legalCaptureTiles);
        return legalMoveTiles.Contains(destination) || legalCaptureTiles.Contains(destination);
    }

    public bool TryGetLegalMove(ChessPiece piece, ChessTile destination, out ChessMoveData moveData, PieceType? requestedPromotionPiece = null)
    {
        moveData = default;
        if (piece == null || destination == null)
        {
            return false;
        }

        ResolveSystems();
        if (board == null || moveGenerator == null)
        {
            return false;
        }

        List<ChessMoveData> legalMoves = GenerateLegalMovesData(piece);
        ChessMoveData? queenPromotionFallback = null;
        ChessMoveData? firstDestinationMove = null;
        for (int i = 0; i < legalMoves.Count; i++)
        {
            ChessMoveData candidate = legalMoves[i];
            if (candidate.ToTile != destination)
            {
                continue;
            }

            firstDestinationMove ??= candidate;
            if (!candidate.IsPromotion)
            {
                moveData = candidate;
                return true;
            }

            if (requestedPromotionPiece.HasValue && candidate.PromotionPieceType == requestedPromotionPiece.Value)
            {
                moveData = candidate;
                return true;
            }

            if (candidate.PromotionPieceType == PieceType.Queen)
            {
                queenPromotionFallback ??= candidate;
            }
        }

        if (queenPromotionFallback.HasValue)
        {
            moveData = queenPromotionFallback.Value;
            return true;
        }

        if (firstDestinationMove.HasValue)
        {
            moveData = firstDestinationMove.Value;
            return true;
        }

        return false;
    }

    public ChessTile GetKingTile(PieceTeam team)
    {
        ResolveSystems();
        if (board == null)
        {
            return null;
        }

        ChessPiece[] pieces = board.GetAllPieces();
        for (int i = 0; i < pieces.Length; i++)
        {
            ChessPiece piece = pieces[i];
            if (piece == null || piece.Team != team || piece.Type != PieceType.King)
            {
                continue;
            }

            return piece.CurrentTile;
        }

        return null;
    }

    public bool IsKingInCheck(PieceTeam team)
    {
        ChessTile kingTile = GetKingTile(team);
        if (kingTile == null)
        {
            return false;
        }

        PieceTeam opponentTeam = GetOpponentTeam(team);
        return IsTileUnderAttack(kingTile, opponentTeam);
    }

    public bool IsTileUnderAttack(ChessTile tile, PieceTeam byTeam)
    {
        if (tile == null)
        {
            return false;
        }

        ResolveSystems();
        if (board == null || moveGenerator == null)
        {
            return false;
        }

        ChessPiece[] pieces = board.GetAllPieces();
        for (int i = 0; i < pieces.Length; i++)
        {
            ChessPiece piece = pieces[i];
            if (piece == null || piece.Team != byTeam || piece.CurrentTile == null)
            {
                continue;
            }

            moveGenerator.GenerateAttackTiles(piece, out List<ChessTile> attacks);
            if (attacks.Contains(tile))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Validation

    List<ChessMoveData> GenerateLegalMovesData(ChessPiece piece)
    {
        List<ChessMoveData> legalMoves = new List<ChessMoveData>(24);
        if (piece == null || piece.CurrentTile == null)
        {
            return legalMoves;
        }

        moveGenerator.GenerateMoves(piece, out List<ChessMoveData> rawMoves);
        for (int i = 0; i < rawMoves.Count; i++)
        {
            ChessMoveData move = rawMoves[i];
            if (!IsRawMoveLegal(piece, move))
            {
                continue;
            }

            legalMoves.Add(move);
        }

        return legalMoves;
    }

    bool IsRawMoveLegal(ChessPiece piece, ChessMoveData move)
    {
        if (piece == null || move.ToTile == null)
        {
            return false;
        }

        if (move.IsCastle && !IsCastleSafe(piece, move))
        {
            return false;
        }

        MoveSimulationState simulationState = ApplySimulation(move);
        bool leavesKingInCheck = IsKingInCheck(piece.Team);
        RevertSimulation(simulationState);
        return !leavesKingInCheck;
    }

    bool IsCastleSafe(ChessPiece king, ChessMoveData move)
    {
        if (king == null || king.CurrentTile == null)
        {
            return false;
        }

        PieceTeam opponentTeam = GetOpponentTeam(king.Team);
        if (IsTileUnderAttack(king.CurrentTile, opponentTeam))
        {
            return false;
        }

        int direction = move.ToTile.X > king.CurrentTile.X ? 1 : -1;
        ChessTile throughTile = board.GetTile(king.CurrentTile.X + direction, king.CurrentTile.Y);
        if (throughTile == null || IsTileUnderAttack(throughTile, opponentTeam))
        {
            return false;
        }

        return true;
    }

    MoveSimulationState ApplySimulation(ChessMoveData move)
    {
        ChessPiece movingPiece = move.Piece;
        MoveSimulationState state = new MoveSimulationState
        {
            MovingPiece = movingPiece,
            FromTile = movingPiece.CurrentTile,
            ToTile = move.ToTile,
            CapturedPiece = move.IsCapture && move.CaptureTile != null ? move.CaptureTile.CurrentPiece : null,
            CaptureTile = move.CaptureTile,
            RookPiece = move.IsCastle && move.CastleRookFrom != null ? move.CastleRookFrom.CurrentPiece : null,
            RookFromTile = move.CastleRookFrom,
            RookToTile = move.CastleRookTo
        };

        if (state.CapturedPiece != null)
        {
            state.CapturedPiece.SetTile(null);
        }

        if (state.RookPiece != null)
        {
            state.RookPiece.SetTile(state.RookToTile);
        }

        movingPiece.SetTile(move.ToTile);
        return state;
    }

    void RevertSimulation(MoveSimulationState state)
    {
        if (state.MovingPiece != null)
        {
            state.MovingPiece.SetTile(state.FromTile);
        }

        if (state.CapturedPiece != null)
        {
            state.CapturedPiece.SetTile(state.CaptureTile);
        }

        if (state.RookPiece != null)
        {
            state.RookPiece.SetTile(state.RookFromTile);
        }
    }

    #endregion

    #region Helpers

    void ResolveSystems()
    {
        if (board == null)
        {
            board = ChessBoard.Instance;
            if (board == null)
            {
                board = FindFirstObjectByType<ChessBoard>();
            }
        }

        moveGenerator ??= ChessMoveGenerator.GetOrCreate();
    }

    static PieceTeam GetOpponentTeam(PieceTeam team)
    {
        return team == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
    }

    struct MoveSimulationState
    {
        public ChessPiece MovingPiece;
        public ChessPiece CapturedPiece;
        public ChessPiece RookPiece;
        public ChessTile FromTile;
        public ChessTile ToTile;
        public ChessTile CaptureTile;
        public ChessTile RookFromTile;
        public ChessTile RookToTile;
    }

    #endregion
}
