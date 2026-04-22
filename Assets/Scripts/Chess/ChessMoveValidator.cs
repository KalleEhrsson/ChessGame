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

        moveGenerator.GenerateMoves(piece, out List<ChessTile> rawMoveTiles, out List<ChessTile> rawCaptureTiles);
        FilterLegalTiles(piece, rawMoveTiles, legalMoveTiles);
        FilterLegalTiles(piece, rawCaptureTiles, legalCaptureTiles);
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

            moveGenerator.GenerateMoves(piece, out List<ChessTile> rawMoveTiles, out List<ChessTile> rawCaptureTiles);
            if (rawMoveTiles.Contains(tile) || rawCaptureTiles.Contains(tile))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Validation

    void FilterLegalTiles(ChessPiece piece, List<ChessTile> rawTiles, List<ChessTile> legalTiles)
    {
        for (int i = 0; i < rawTiles.Count; i++)
        {
            ChessTile targetTile = rawTiles[i];
            if (targetTile == null)
            {
                continue;
            }

            MoveSimulationState simulationState = ApplySimulation(piece, targetTile);
            bool leavesKingInCheck = IsKingInCheck(piece.Team);
            RevertSimulation(simulationState);

            if (!leavesKingInCheck)
            {
                legalTiles.Add(targetTile);
            }
        }
    }

    MoveSimulationState ApplySimulation(ChessPiece movingPiece, ChessTile targetTile)
    {
        MoveSimulationState state = new MoveSimulationState
        {
            MovingPiece = movingPiece,
            FromTile = movingPiece.CurrentTile,
            ToTile = targetTile,
            CapturedPiece = targetTile != null ? targetTile.CurrentPiece : null
        };

        if (state.CapturedPiece != null)
        {
            state.CapturedPiece.SetTile(null);
        }

        movingPiece.SetTile(targetTile);
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
            state.CapturedPiece.SetTile(state.ToTile);
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
        public ChessTile FromTile;
        public ChessTile ToTile;
    }

    #endregion
}
