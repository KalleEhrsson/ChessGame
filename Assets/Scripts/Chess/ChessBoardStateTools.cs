using UnityEngine;

public class ChessBoardStateTools
{
    readonly ChessBoard board;
    readonly ChessTurnManager turnManager;
    readonly ChessGameStateController gameStateController;
    readonly StockfishService stockfishService;
    string lastValidationError;

    public ChessBoardStateTools(ChessBoard board, ChessTurnManager turnManager, ChessGameStateController gameStateController, StockfishService stockfishService)
    {
        this.board = board;
        this.turnManager = turnManager;
        this.gameStateController = gameStateController;
        this.stockfishService = stockfishService;
    }

    #region API

    public bool IsReady => board != null && turnManager != null;

    public async void ResetBoard()
    {
        if (!IsReady)
        {
            Debug.LogWarning("[ChessBoardStateTools] Missing dependencies for reset.");
            return;
        }

        board.ResetBoardToStartingPosition();
        await RebuildStateAndSyncAi("Reset board");
    }

    public async void ClearBoard()
    {
        if (!IsReady)
        {
            Debug.LogWarning("[ChessBoardStateTools] Missing dependencies for clear.");
            return;
        }

        board.ClearBoardState();
        turnManager.SetTurn(PieceTeam.White);
        gameStateController?.ResetToPlaying();
        await RebuildStateAndSyncAi("Clear board");
    }

    public async System.Threading.Tasks.Task<bool> LoadPresetAsync(ChessBoardPreset preset)
    {
        if (!ChessFenUtility.TryParseFen(preset.Fen, out _, out string parseError))
        {
            lastValidationError = $"Preset '{preset.Name}' invalid: {parseError}";
            Debug.LogWarning($"[ChessBoardStateTools] {lastValidationError}");
            return false;
        }

        bool applied = ChessFenUtility.TryApplyFen(board, turnManager, gameStateController, preset.Fen);
        if (!applied)
        {
            return false;
        }
        return await RebuildStateAndSyncAi($"Preset applied: {preset.Name}");
    }

    public string ExportFen()
    {
        if (!IsReady)
        {
            return string.Empty;
        }

        return ChessFenUtility.ExportFen(board, turnManager.GetCurrentTurn());
    }

    public async System.Threading.Tasks.Task<bool> ImportFenAsync(string fen)
    {
        if (!ChessFenUtility.TryApplyFen(board, turnManager, gameStateController, fen))
        {
            return false;
        }
        return await RebuildStateAndSyncAi("FEN import");
    }

    public async void SetSideToMove(PieceTeam team)
    {
        if (!IsReady)
        {
            return;
        }

        turnManager.SetTurn(team);
        gameStateController?.ResetToPlaying();
        gameStateController?.EvaluateEndOfTurn(team);
        await RebuildStateAndSyncAi("Debug side-to-move change");
    }

    public void SetAiEnabled(bool enabled)
    {
        if (!IsReady)
        {
            return;
        }

        turnManager.SetAiEnabledForBothTeams(enabled);
    }

    public async System.Threading.Tasks.Task<bool> SpawnPieceAsync(PieceTeam team, PieceType type, ChessTile tile)
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

        if (!success)
        {
            return false;
        }
        return await RebuildStateAndSyncAi("Debug piece spawn");
    }

    public async System.Threading.Tasks.Task<bool> RemovePieceAsync(ChessTile tile)
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

        if (!success)
        {
            return false;
        }
        return await RebuildStateAndSyncAi("Debug piece remove");
    }

    public async System.Threading.Tasks.Task<bool> MovePieceAsync(ChessTile from, ChessTile to)
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

        if (!success)
        {
            return false;
        }
        return await RebuildStateAndSyncAi("Debug piece move");
    }

    public bool TryBuildFenFromCurrentBoard(out string fen, out string error)
    {
        fen = ChessFenUtility.ExportFen(board, turnManager.GetCurrentTurn());
        error = string.Empty;
        return TryValidateDebugBoardState(fen, out error);
    }

    public bool TryValidateDebugBoardState(string fen, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(fen))
        {
            error = "FEN export failed.";
            return false;
        }
        if (!ChessFenUtility.TryParseFen(fen, out ChessFenUtility.FenDataView data, out error))
        {
            return false;
        }
        if (data.WhiteKingCount != 1 || data.BlackKingCount != 1)
        {
            error = "Board must have exactly one king per side.";
            return false;
        }
        if (data.HasPawnOnInvalidRank)
        {
            error = "Pawns cannot be on first or eighth rank.";
            return false;
        }
        if (!data.IsActiveTurnValid)
        {
            error = "Side to move is invalid.";
            return false;
        }
        return true;
    }

    async System.Threading.Tasks.Task<bool> RebuildStateAndSyncAi(string reason)
    {
        if (!TryBuildFenFromCurrentBoard(out string fen, out string error))
        {
            lastValidationError = error;
            Debug.LogWarning($"[ChessBoardStateTools] Debug state rejected: {error}");
            return false;
        }
        lastValidationError = string.Empty;
        Debug.Log($"[ChessBoardStateTools] Debug state applied: {reason}");
        return turnManager != null && await turnManager.HandleDebugBoardSyncAsync(fen, true, string.Empty);
    }

    #endregion
}
