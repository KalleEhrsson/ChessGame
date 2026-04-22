using System.Text;
using UnityEngine;

public static class ChessFenBuilder
{
    #region API

    public static string BuildFen(ChessBoard board, PieceTeam activeColor)
    {
        if (board == null)
        {
            return string.Empty;
        }

        StringBuilder fenBuilder = new StringBuilder(96);
        BuildPiecePlacement(board, fenBuilder);
        fenBuilder.Append(' ');
        fenBuilder.Append(activeColor == PieceTeam.White ? 'w' : 'b');
        fenBuilder.Append(' ');
        fenBuilder.Append(ResolveCastlingRights());
        fenBuilder.Append(' ');
        fenBuilder.Append(ResolveEnPassantTarget());
        fenBuilder.Append(' ');
        fenBuilder.Append(ResolveHalfMoveClock());
        fenBuilder.Append(' ');
        fenBuilder.Append(ResolveFullMoveNumber());

        return fenBuilder.ToString();
    }

    #endregion

    #region Build

    static void BuildPiecePlacement(ChessBoard board, StringBuilder fenBuilder)
    {
        for (int y = 7; y >= 0; y--)
        {
            int emptyCount = 0;
            for (int x = 0; x < 8; x++)
            {
                ChessTile tile = board.GetTile(x, y);
                ChessPiece piece = tile != null ? tile.CurrentPiece : null;
                if (piece == null)
                {
                    emptyCount++;
                    continue;
                }

                if (emptyCount > 0)
                {
                    fenBuilder.Append(emptyCount);
                    emptyCount = 0;
                }

                fenBuilder.Append(ToFenPieceChar(piece));
            }

            if (emptyCount > 0)
            {
                fenBuilder.Append(emptyCount);
            }

            if (y > 0)
            {
                fenBuilder.Append('/');
            }
        }
    }

    static char ToFenPieceChar(ChessPiece piece)
    {
        char pieceChar = piece.Type switch
        {
            PieceType.Pawn => 'p',
            PieceType.Rook => 'r',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            _ => ' '
        };

        return piece.Team == PieceTeam.White ? char.ToUpperInvariant(pieceChar) : pieceChar;
    }

    static string ResolveCastlingRights()
    {
        return "-";
    }

    static string ResolveEnPassantTarget()
    {
        return "-";
    }

    static int ResolveHalfMoveClock()
    {
        return 0;
    }

    static int ResolveFullMoveNumber()
    {
        return 1;
    }

    #endregion
}
