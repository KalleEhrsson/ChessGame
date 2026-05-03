using System;
using System.Collections.Generic;
using UnityEngine;

public static class ChessFenUtility
{
    public readonly struct FenDataView
    {
        public readonly int WhiteKingCount;
        public readonly int BlackKingCount;
        public readonly bool HasPawnOnInvalidRank;
        public readonly bool IsActiveTurnValid;

        public FenDataView(int whiteKingCount, int blackKingCount, bool hasPawnOnInvalidRank, bool isActiveTurnValid)
        {
            WhiteKingCount = whiteKingCount;
            BlackKingCount = blackKingCount;
            HasPawnOnInvalidRank = hasPawnOnInvalidRank;
            IsActiveTurnValid = isActiveTurnValid;
        }
    }
    #region API

    public static string ExportFen(ChessBoard board, PieceTeam activeTurn)
    {
        return ChessFenBuilder.BuildFen(board, activeTurn);
    }

    public static bool TryApplyFen(ChessBoard board, ChessTurnManager turnManager, ChessGameStateController gameStateController, string fen)
    {
        if (board == null || turnManager == null)
        {
            Debug.LogWarning("[ChessFenUtility] Missing board or turn manager.");
            return false;
        }

        if (!TryParseFen(fen, out FenData data))
        {
            return false;
        }

        board.ClearBoardState(false);

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                FenPiece fenPiece = data.Board[x, y];
                if (!fenPiece.HasPiece)
                {
                    continue;
                }

                ChessTile tile = board.GetTile(x, y);
                if (!board.TrySpawnPiece(fenPiece.Team, fenPiece.Type, tile))
                {
                    return false;
                }
            }
        }

        ApplyCastlingRights(board, data.CastlingRights);
        ApplyEnPassantState(board, data.EnPassantTarget);
        board.SetRuntimeState(board.GetEnPassantTargetTile(), board.GetEnPassantVulnerablePawn(), data.HalfMoveClock, data.FullMoveNumber);

        turnManager.SetTurn(data.ActiveTurn);
        gameStateController?.ResetToPlaying();
        gameStateController?.EvaluateEndOfTurn(data.ActiveTurn);
        return true;
    }

    #endregion

    #region Parse

    public static bool TryParseFen(string fen, out FenDataView view, out string error)
    {
        view = default;
        error = string.Empty;
        if (!TryParseFen(fen, out FenData data))
        {
            error = "FEN parse failed.";
            return false;
        }

        int whiteKings = 0;
        int blackKings = 0;
        bool pawnOnInvalidRank = false;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                FenPiece piece = data.Board[x, y];
                if (!piece.HasPiece)
                {
                    continue;
                }
                if (piece.Type == PieceType.King)
                {
                    if (piece.Team == PieceTeam.White) whiteKings++; else blackKings++;
                }
                if (piece.Type == PieceType.Pawn && (y == 0 || y == 7))
                {
                    pawnOnInvalidRank = true;
                }
            }
        }
        view = new FenDataView(whiteKings, blackKings, pawnOnInvalidRank, true);
        return true;
    }

    static bool TryParseFen(string fen, out FenData fenData)
    {
        fenData = default;
        if (string.IsNullOrWhiteSpace(fen))
        {
            Debug.LogWarning("[ChessFenUtility] FEN text is empty.");
            return false;
        }

        string[] fields = fen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 4)
        {
            Debug.LogWarning("[ChessFenUtility] FEN must include at least 4 fields.");
            return false;
        }

        if (!TryParsePlacement(fields[0], out FenPiece[,] board))
        {
            return false;
        }

        PieceTeam active = fields[1] == "b" ? PieceTeam.Black : PieceTeam.White;
        string castling = fields[2];
        string enPassant = fields[3];

        int half = 0;
        int full = 1;
        if (fields.Length > 4)
        {
            int.TryParse(fields[4], out half);
        }

        if (fields.Length > 5)
        {
            int.TryParse(fields[5], out full);
        }

        fenData = new FenData(board, active, castling, enPassant, Mathf.Max(0, half), Mathf.Max(1, full));
        return true;
    }

    static bool TryParsePlacement(string placement, out FenPiece[,] board)
    {
        board = new FenPiece[8, 8];
        string[] ranks = placement.Split('/');
        if (ranks.Length != 8)
        {
            Debug.LogWarning("[ChessFenUtility] Invalid placement rank count.");
            return false;
        }

        for (int rankIndex = 0; rankIndex < 8; rankIndex++)
        {
            int y = 7 - rankIndex;
            int x = 0;
            string rank = ranks[rankIndex];
            for (int i = 0; i < rank.Length; i++)
            {
                char c = rank[i];
                if (char.IsDigit(c))
                {
                    x += c - '0';
                    continue;
                }

                if (x < 0 || x > 7 || !TryMapFenPiece(c, out PieceTeam team, out PieceType type))
                {
                    Debug.LogWarning($"[ChessFenUtility] Invalid piece char '{c}' in FEN.");
                    return false;
                }

                board[x, y] = FenPiece.Create(team, type);
                x++;
            }

            if (x != 8)
            {
                Debug.LogWarning("[ChessFenUtility] Invalid file count in FEN rank.");
                return false;
            }
        }

        return true;
    }

    static bool TryMapFenPiece(char c, out PieceTeam team, out PieceType type)
    {
        team = char.IsUpper(c) ? PieceTeam.White : PieceTeam.Black;
        char lower = char.ToLowerInvariant(c);
        type = lower switch
        {
            'p' => PieceType.Pawn,
            'r' => PieceType.Rook,
            'n' => PieceType.Knight,
            'b' => PieceType.Bishop,
            'q' => PieceType.Queen,
            'k' => PieceType.King,
            _ => PieceType.Pawn
        };

        return lower is 'p' or 'r' or 'n' or 'b' or 'q' or 'k';
    }

    #endregion

    #region Apply

    static void ApplyCastlingRights(ChessBoard board, string castling)
    {
        HashSet<char> rights = new(castling ?? "-");
        MarkCastlePieceState(board, PieceTeam.White, false, rights.Contains('K'), true);
        MarkCastlePieceState(board, PieceTeam.White, false, rights.Contains('Q'), false);
        MarkCastlePieceState(board, PieceTeam.Black, true, rights.Contains('k'), true);
        MarkCastlePieceState(board, PieceTeam.Black, true, rights.Contains('q'), false);

        MarkKingMovedByRights(board, PieceTeam.White, rights.Contains('K') || rights.Contains('Q'));
        MarkKingMovedByRights(board, PieceTeam.Black, rights.Contains('k') || rights.Contains('q'));
    }

    static void MarkCastlePieceState(ChessBoard board, PieceTeam team, bool isBlackRank, bool hasRight, bool kingSide)
    {
        int y = isBlackRank ? 7 : 0;
        int x = kingSide ? 7 : 0;
        ChessPiece rook = board.GetPieceAt(x, y);
        if (rook == null || rook.Type != PieceType.Rook || rook.Team != team)
        {
            return;
        }

        if (hasRight)
        {
            rook.ResetMovedState();
        }
        else
        {
            rook.MarkMoved();
        }
    }

    static void MarkKingMovedByRights(ChessBoard board, PieceTeam team, bool hasAnyRight)
    {
        ChessPiece king = board.GetPieceAt(4, team == PieceTeam.White ? 0 : 7);
        if (king == null || king.Type != PieceType.King || king.Team != team)
        {
            return;
        }

        if (hasAnyRight)
        {
            king.ResetMovedState();
        }
        else
        {
            king.MarkMoved();
        }
    }

    static void ApplyEnPassantState(ChessBoard board, string enPassant)
    {
        if (string.IsNullOrWhiteSpace(enPassant) || enPassant == "-")
        {
            board.SetRuntimeState(null, null, board.GetHalfMoveClock(), board.GetFullMoveNumber());
            return;
        }

        ChessTile target = board.GetTile(enPassant.ToUpperInvariant());
        if (target == null)
        {
            board.SetRuntimeState(null, null, board.GetHalfMoveClock(), board.GetFullMoveNumber());
            return;
        }

        int pawnY = target.Y == 2 ? 3 : target.Y == 5 ? 4 : -1;
        ChessPiece vulnerablePawn = pawnY >= 0 ? board.GetPieceAt(target.X, pawnY) : null;
        if (vulnerablePawn != null && vulnerablePawn.Type != PieceType.Pawn)
        {
            vulnerablePawn = null;
        }

        board.SetRuntimeState(target, vulnerablePawn, board.GetHalfMoveClock(), board.GetFullMoveNumber());
    }

    #endregion

    readonly struct FenData
    {
        public readonly FenPiece[,] Board;
        public readonly PieceTeam ActiveTurn;
        public readonly string CastlingRights;
        public readonly string EnPassantTarget;
        public readonly int HalfMoveClock;
        public readonly int FullMoveNumber;

        public FenData(FenPiece[,] board, PieceTeam activeTurn, string castlingRights, string enPassantTarget, int halfMoveClock, int fullMoveNumber)
        {
            Board = board;
            ActiveTurn = activeTurn;
            CastlingRights = castlingRights;
            EnPassantTarget = enPassantTarget;
            HalfMoveClock = halfMoveClock;
            FullMoveNumber = fullMoveNumber;
        }
    }

    readonly struct FenPiece
    {
        public readonly bool HasPiece;
        public readonly PieceTeam Team;
        public readonly PieceType Type;

        FenPiece(bool hasPiece, PieceTeam team, PieceType type)
        {
            HasPiece = hasPiece;
            Team = team;
            Type = type;
        }

        public static FenPiece Create(PieceTeam team, PieceType type) => new(true, team, type);
    }
}
