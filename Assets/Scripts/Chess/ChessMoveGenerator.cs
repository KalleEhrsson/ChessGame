using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChessMoveGenerator : MonoBehaviour
{
    #region Singleton

    public static ChessMoveGenerator Instance { get; private set; }

    public static ChessMoveGenerator GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessMoveGenerator existing = FindFirstObjectByType<ChessMoveGenerator>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject host = new("ChessMoveGenerator");
        Instance = host.AddComponent<ChessMoveGenerator>();
        return Instance;
    }

    #endregion

    #region Variables

    ChessBoard board;

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
        ResolveBoard();
    }

    #endregion

    #region Public API

    public void GenerateMoves(ChessPiece piece, out List<ChessMoveData> moves)
    {
        moves = new List<ChessMoveData>(24);
        if (piece == null || piece.CurrentTile == null)
        {
            return;
        }

        ResolveBoard();
        if (board == null)
        {
            return;
        }

        switch (piece.Type)
        {
            case PieceType.Pawn:
                GeneratePawnMoves(piece, moves);
                break;
            case PieceType.Rook:
                GenerateSlidingMoves(piece, moves, Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down);
                break;
            case PieceType.Bishop:
                GenerateSlidingMoves(piece, moves,
                    new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1));
                break;
            case PieceType.Queen:
                GenerateSlidingMoves(piece, moves,
                    Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down,
                    new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1));
                break;
            case PieceType.Knight:
                GenerateKnightMoves(piece, moves);
                break;
            case PieceType.King:
                GenerateKingMoves(piece, moves);
                break;
        }
    }

    public void GenerateMoves(ChessPiece piece, out List<ChessTile> moveTiles, out List<ChessTile> captureTiles)
    {
        moveTiles = new List<ChessTile>(16);
        captureTiles = new List<ChessTile>(16);
        GenerateMoves(piece, out List<ChessMoveData> moves);
        for (int i = 0; i < moves.Count; i++)
        {
            ChessMoveData move = moves[i];
            if (move.IsCapture)
            {
                if (!captureTiles.Contains(move.ToTile))
                {
                    captureTiles.Add(move.ToTile);
                }

                continue;
            }

            if (!moveTiles.Contains(move.ToTile))
            {
                moveTiles.Add(move.ToTile);
            }
        }
    }

    public void GenerateAttackTiles(ChessPiece piece, out List<ChessTile> attackTiles)
    {
        attackTiles = new List<ChessTile>(16);
        if (piece == null || piece.CurrentTile == null)
        {
            return;
        }

        ResolveBoard();
        if (board == null)
        {
            return;
        }

        switch (piece.Type)
        {
            case PieceType.Pawn:
                GeneratePawnAttackTiles(piece, attackTiles);
                break;
            case PieceType.Knight:
                GenerateKnightAttackTiles(piece, attackTiles);
                break;
            case PieceType.Bishop:
                GenerateSlidingAttackTiles(piece, attackTiles, new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1));
                break;
            case PieceType.Rook:
                GenerateSlidingAttackTiles(piece, attackTiles, Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down);
                break;
            case PieceType.Queen:
                GenerateSlidingAttackTiles(piece, attackTiles,
                    Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down,
                    new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1));
                break;
            case PieceType.King:
                GenerateKingAttackTiles(piece, attackTiles);
                break;
        }
    }

    #endregion

    #region Generators

    void GeneratePawnMoves(ChessPiece piece, List<ChessMoveData> moves)
    {
        int direction = piece.Team == PieceTeam.White ? 1 : -1;
        int startRank = piece.Team == PieceTeam.White ? 1 : 6;
        int promotionRank = piece.Team == PieceTeam.White ? 7 : 0;
        int x = piece.CurrentTile.X;
        int y = piece.CurrentTile.Y;

        ChessTile oneForward = GetTile(x, y + direction);
        if (IsEmpty(oneForward))
        {
            AddPawnMove(piece, piece.CurrentTile, oneForward, false, null, promotionRank, moves);

            bool isOnStartRank = y == startRank;
            ChessTile twoForward = GetTile(x, y + direction * 2);
            if (isOnStartRank && IsEmpty(twoForward))
            {
                moves.Add(new ChessMoveData(piece, piece.CurrentTile, twoForward));
            }
        }

        TryAddPawnCapture(piece, x - 1, y + direction, promotionRank, moves);
        TryAddPawnCapture(piece, x + 1, y + direction, promotionRank, moves);
        TryAddEnPassant(piece, y + direction, moves);
    }

    void GenerateKnightMoves(ChessPiece piece, List<ChessMoveData> moves)
    {
        int x = piece.CurrentTile.X;
        int y = piece.CurrentTile.Y;

        Vector2Int[] offsets =
        {
            new(1, 2), new(2, 1), new(2, -1), new(1, -2),
            new(-1, -2), new(-2, -1), new(-2, 1), new(-1, 2)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            AddMoveForTeam(piece, GetTile(x + offsets[i].x, y + offsets[i].y), moves);
        }
    }

    void GenerateKingMoves(ChessPiece piece, List<ChessMoveData> moves)
    {
        int x = piece.CurrentTile.X;
        int y = piece.CurrentTile.Y;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                AddMoveForTeam(piece, GetTile(x + dx, y + dy), moves);
            }
        }

        TryAddCastleMove(piece, 7, 6, 5, new[] { 5, 6 }, moves);
        TryAddCastleMove(piece, 0, 2, 3, new[] { 1, 2, 3 }, moves);
    }

    void GenerateSlidingMoves(ChessPiece piece, List<ChessMoveData> moves, params Vector2Int[] directions)
    {
        int originX = piece.CurrentTile.X;
        int originY = piece.CurrentTile.Y;

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2Int direction = directions[i];
            int x = originX + direction.x;
            int y = originY + direction.y;

            while (true)
            {
                ChessTile tile = GetTile(x, y);
                if (tile == null)
                {
                    break;
                }

                ChessPiece occupant = tile.CurrentPiece;
                if (occupant == null)
                {
                    moves.Add(new ChessMoveData(piece, piece.CurrentTile, tile));
                }
                else
                {
                    if (occupant.Team != piece.Team)
                    {
                        moves.Add(new ChessMoveData(piece, piece.CurrentTile, tile, true, tile));
                    }

                    break;
                }

                x += direction.x;
                y += direction.y;
            }
        }
    }

    void GeneratePawnAttackTiles(ChessPiece piece, List<ChessTile> attackTiles)
    {
        int direction = piece.Team == PieceTeam.White ? 1 : -1;
        TryAddAttackTile(GetTile(piece.CurrentTile.X - 1, piece.CurrentTile.Y + direction), attackTiles);
        TryAddAttackTile(GetTile(piece.CurrentTile.X + 1, piece.CurrentTile.Y + direction), attackTiles);
    }

    void GenerateKnightAttackTiles(ChessPiece piece, List<ChessTile> attackTiles)
    {
        int x = piece.CurrentTile.X;
        int y = piece.CurrentTile.Y;
        Vector2Int[] offsets =
        {
            new(1, 2), new(2, 1), new(2, -1), new(1, -2),
            new(-1, -2), new(-2, -1), new(-2, 1), new(-1, 2)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            TryAddAttackTile(GetTile(x + offsets[i].x, y + offsets[i].y), attackTiles);
        }
    }

    void GenerateKingAttackTiles(ChessPiece piece, List<ChessTile> attackTiles)
    {
        int x = piece.CurrentTile.X;
        int y = piece.CurrentTile.Y;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                TryAddAttackTile(GetTile(x + dx, y + dy), attackTiles);
            }
        }
    }

    void GenerateSlidingAttackTiles(ChessPiece piece, List<ChessTile> attackTiles, params Vector2Int[] directions)
    {
        int originX = piece.CurrentTile.X;
        int originY = piece.CurrentTile.Y;
        for (int i = 0; i < directions.Length; i++)
        {
            Vector2Int direction = directions[i];
            int x = originX + direction.x;
            int y = originY + direction.y;
            while (true)
            {
                ChessTile tile = GetTile(x, y);
                if (tile == null)
                {
                    break;
                }

                TryAddAttackTile(tile, attackTiles);
                if (tile.CurrentPiece != null)
                {
                    break;
                }

                x += direction.x;
                y += direction.y;
            }
        }
    }

    #endregion

    #region Helpers

    void ResolveBoard()
    {
        if (board != null)
        {
            return;
        }

        board = ChessBoard.Instance;
        if (board == null)
        {
            board = FindFirstObjectByType<ChessBoard>();
        }
    }

    ChessTile GetTile(int x, int y)
    {
        return board != null ? board.GetTile(x, y) : null;
    }

    bool IsEmpty(ChessTile tile)
    {
        return tile != null && tile.CurrentPiece == null;
    }

    void TryAddPawnCapture(ChessPiece movingPiece, int x, int y, int promotionRank, List<ChessMoveData> moves)
    {
        ChessTile tile = GetTile(x, y);
        if (tile == null || tile.CurrentPiece == null)
        {
            return;
        }

        if (tile.CurrentPiece.Team != movingPiece.Team)
        {
            AddPawnMove(movingPiece, movingPiece.CurrentTile, tile, true, tile, promotionRank, moves);
        }
    }

    void TryAddEnPassant(ChessPiece movingPiece, int targetY, List<ChessMoveData> moves)
    {
        ChessTile enPassantTarget = board.GetEnPassantTargetTile();
        if (enPassantTarget == null || enPassantTarget.Y != targetY)
        {
            return;
        }

        int fileDistance = Mathf.Abs(enPassantTarget.X - movingPiece.CurrentTile.X);
        if (fileDistance != 1)
        {
            return;
        }

        ChessPiece enPassantVictim = board.GetEnPassantVulnerablePawn();
        if (enPassantVictim == null || enPassantVictim.CurrentTile == null || enPassantVictim.Team == movingPiece.Team)
        {
            return;
        }

        if (enPassantVictim.CurrentTile.Y != movingPiece.CurrentTile.Y || enPassantVictim.CurrentTile.X != enPassantTarget.X)
        {
            return;
        }

        moves.Add(new ChessMoveData(
            movingPiece,
            movingPiece.CurrentTile,
            enPassantTarget,
            true,
            enPassantVictim.CurrentTile,
            isEnPassant: true));
    }

    void TryAddCastleMove(ChessPiece king, int rookX, int kingToX, int rookToX, int[] clearSquares, List<ChessMoveData> moves)
    {
        if (king.HasMoved || king.CurrentTile == null)
        {
            return;
        }

        int y = king.CurrentTile.Y;
        ChessTile rookTile = GetTile(rookX, y);
        ChessPiece rook = rookTile != null ? rookTile.CurrentPiece : null;
        if (rook == null || rook.Team != king.Team || rook.Type != PieceType.Rook || rook.HasMoved)
        {
            return;
        }

        for (int i = 0; i < clearSquares.Length; i++)
        {
            ChessTile clearTile = GetTile(clearSquares[i], y);
            if (clearTile == null || clearTile.CurrentPiece != null)
            {
                return;
            }
        }

        ChessTile kingToTile = GetTile(kingToX, y);
        ChessTile rookToTile = GetTile(rookToX, y);
        if (kingToTile == null || rookToTile == null)
        {
            return;
        }

        moves.Add(new ChessMoveData(
            king,
            king.CurrentTile,
            kingToTile,
            false,
            null,
            true,
            rookTile,
            rookToTile));
    }

    void AddMoveForTeam(ChessPiece movingPiece, ChessTile tile, List<ChessMoveData> moves)
    {
        if (tile == null)
        {
            return;
        }

        if (tile.CurrentPiece == null)
        {
            moves.Add(new ChessMoveData(movingPiece, movingPiece.CurrentTile, tile));
            return;
        }

        if (tile.CurrentPiece.Team != movingPiece.Team)
        {
            moves.Add(new ChessMoveData(movingPiece, movingPiece.CurrentTile, tile, true, tile));
        }
    }

    void AddPawnMove(ChessPiece piece, ChessTile from, ChessTile to, bool isCapture, ChessTile captureTile, int promotionRank, List<ChessMoveData> moves)
    {
        if (to == null)
        {
            return;
        }

        if (to.Y == promotionRank)
        {
            PieceType[] promotionTypes = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };
            for (int i = 0; i < promotionTypes.Length; i++)
            {
                moves.Add(new ChessMoveData(piece, from, to, isCapture, captureTile, false, null, null, false, true, promotionTypes[i]));
            }

            return;
        }

        moves.Add(new ChessMoveData(piece, from, to, isCapture, captureTile));
    }

    void TryAddAttackTile(ChessTile tile, List<ChessTile> attackTiles)
    {
        if (tile != null)
        {
            attackTiles.Add(tile);
        }
    }

    #endregion
}
