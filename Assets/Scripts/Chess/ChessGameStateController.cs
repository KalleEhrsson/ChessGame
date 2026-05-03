using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChessGameState
{
    Playing,
    Checkmate,
    Stalemate,
    Draw,
    Resignation
}

public readonly struct ChessGameEndResult
{
    public ChessGameState FinalState { get; }
    public PieceTeam? WinningTeam { get; }
    public PieceTeam? LosingTeam { get; }
    public PieceTeam AffectedTurn { get; }
    public string Reason { get; }

    public ChessGameEndResult(ChessGameState finalState, PieceTeam? winningTeam, PieceTeam? losingTeam, PieceTeam affectedTurn, string reason)
    {
        FinalState = finalState;
        WinningTeam = winningTeam;
        LosingTeam = losingTeam;
        AffectedTurn = affectedTurn;
        Reason = reason;
    }
}

[DisallowMultipleComponent]
public class ChessGameStateController : MonoBehaviour
{
    #region Singleton

    public static ChessGameStateController Instance { get; private set; }

    public static ChessGameStateController GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessGameStateController existing = FindFirstObjectByType<ChessGameStateController>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject host = new("ChessGameStateController");
        Instance = host.AddComponent<ChessGameStateController>();
        return Instance;
    }

    #endregion

    #region Variables

    [SerializeField] ChessGameState currentState = ChessGameState.Playing;

    ChessBoard board;
    ChessMoveValidator moveValidator;

    #endregion

    #region Events

    public event Action<ChessGameEndResult> GameEnded;

    #endregion

    #region Properties

    public ChessGameState CurrentState => currentState;

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
        currentState = ChessGameState.Playing;
    }

    #endregion

    #region API

    public bool IsGameplayActive()
    {
        return currentState == ChessGameState.Playing;
    }

    public void EvaluateEndOfTurn(PieceTeam currentTurn)
    {
        if (!IsGameplayActive())
        {
            return;
        }

        ResolveSystems();
        if (moveValidator == null)
        {
            return;
        }

        bool hasAnyLegalMove = HasAnyLegalMove(currentTurn);
        if (hasAnyLegalMove)
        {
            return;
        }

        bool inCheck = moveValidator.IsKingInCheck(currentTurn);
        if (inCheck)
        {
            PieceTeam winner = GetOpponentTeam(currentTurn);
            EndGame(ChessGameState.Checkmate, winner, currentTurn, "Checkmate", currentTurn);
            return;
        }

        EndGame(ChessGameState.Stalemate, null, null, "Stalemate", currentTurn);
    }

    public void ResignCurrentPlayer()
    {
        ResignSide(ChessTurnManager.GetOrCreate().GetCurrentTurn());
    }

    public void ResignSide(PieceTeam side)
    {
        PieceTeam winner = GetOpponentTeam(side);
        EndGame(ChessGameState.Resignation, winner, side, $"{side} resigned", side);
    }


    public bool EndGame(ChessGameState reason, PieceTeam? winner, PieceTeam? loser, string reasonText, PieceTeam affectedTurn)
    {
        if (currentState != ChessGameState.Playing)
        {
            return false;
        }

        currentState = reason;
        ChessGameEndResult result = new(reason, winner, loser, affectedTurn, reasonText);
        Debug.Log($"Game ended: {reasonText}");
        GameEnded?.Invoke(result);
        return true;
    }

    public void DeclareDraw(string reasonText = "Draw")
    {
        PieceTeam turn = ChessTurnManager.GetOrCreate().GetCurrentTurn();
        EndGame(ChessGameState.Draw, null, null, reasonText, turn);
    }
    public bool HasAnyLegalMove(PieceTeam team)
    {
        ResolveSystems();
        if (board == null || moveValidator == null)
        {
            return false;
        }

        ChessPiece[] pieces = board.GetAllPieces();
        for (int i = 0; i < pieces.Length; i++)
        {
            ChessPiece piece = pieces[i];
            if (piece == null || piece.Team != team || piece.CurrentTile == null)
            {
                continue;
            }

            moveValidator.GenerateLegalMoves(piece, out List<ChessTile> legalMoveTiles, out List<ChessTile> legalCaptureTiles);
            if (legalMoveTiles.Count > 0 || legalCaptureTiles.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    public void ResetToPlaying()
    {
        currentState = ChessGameState.Playing;
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

        moveValidator ??= ChessMoveValidator.GetOrCreate();
    }

    static PieceTeam GetOpponentTeam(PieceTeam team)
    {
        return team == PieceTeam.White ? PieceTeam.Black : PieceTeam.White;
    }

    #endregion
}
