using UnityEngine;

public class ChessBoardStateTools
{
    readonly ChessBoard board;
    readonly ChessTurnManager turnManager;
    readonly ChessGameStateController gameStateController;

    public ChessBoardStateTools(ChessBoard board, ChessTurnManager turnManager, ChessGameStateController gameStateController)
    {
        this.board = board;
        this.turnManager = turnManager;
        this.gameStateController = gameStateController;
    }

    #region API

    public bool IsReady => board != null && turnManager != null;

    public void ResetBoard()
    {
        if (!IsReady)
        {
            Debug.LogWarning("[ChessBoardStateTools] Missing dependencies for reset.");
            return;
        }

        board.ResetBoardToStartingPosition();
    }

    public void ClearBoard()
    {
        if (!IsReady)
        {
            Debug.LogWarning("[ChessBoardStateTools] Missing dependencies for clear.");
            return;
        }

        board.ClearBoardState();
        turnManager.SetTurn(PieceTeam.White);
        gameStateController?.ResetToPlaying();
    }

    public bool LoadPreset(ChessBoardPreset preset)
    {
        return ChessFenUtility.TryApplyFen(board, turnManager, gameStateController, preset.Fen);
    }

    public string ExportFen()
    {
        if (!IsReady)
        {
            return string.Empty;
        }

        return ChessFenUtility.ExportFen(board, turnManager.GetCurrentTurn());
    }

    public bool ImportFen(string fen)
    {
        return ChessFenUtility.TryApplyFen(board, turnManager, gameStateController, fen);
    }

    public void SetSideToMove(PieceTeam team)
    {
        if (!IsReady)
        {
            return;
        }

        turnManager.SetTurn(team);
        gameStateController?.ResetToPlaying();
        gameStateController?.EvaluateEndOfTurn(team);
    }

    public void SetAiEnabled(bool enabled)
    {
        if (!IsReady)
        {
            return;
        }

        turnManager.SetAiEnabledForBothTeams(enabled);
    }

    public bool SpawnPiece(PieceTeam team, PieceType type, ChessTile tile)
    {
        if (!IsReady)
        {
            return false;
        }

        bool success = board.TrySpawnPiece(team, type, tile);
        if (success)
        {
            gameStateController?.ResetToPlaying();
        }

        return success;
    }

    public bool RemovePiece(ChessTile tile)
    {
        if (!IsReady)
        {
            return false;
        }

        bool success = board.TryRemovePiece(tile);
        if (success)
        {
            gameStateController?.ResetToPlaying();
        }

        return success;
    }

    public bool MovePiece(ChessTile from, ChessTile to)
    {
        if (!IsReady)
        {
            return false;
        }

        bool success = board.TryRelocatePiece(from, to, true);
        if (success)
        {
            gameStateController?.ResetToPlaying();
        }

        return success;
    }

    #endregion
}
