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

    public void GenerateMoves(ChessPiece piece, out List<ChessTile> moveTiles, out List<ChessTile> captureTiles)
    {
        moveTiles = new List<ChessTile>(16);
        captureTiles = new List<ChessTile>(16);

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
                GeneratePawn(piece, moveTiles, captureTiles);
                break;
            case PieceType.Rook:
                GenerateSliding(piece, moveTiles, captureTiles, Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down);
                break;
            case PieceType.Bishop:
                GenerateSliding(piece, moveTiles, captureTiles,
                    new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1));
                break;
            case PieceType.Queen:
                GenerateSliding(piece, moveTiles, captureTiles,
                    Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down,
                    new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1));
                break;
            case PieceType.Knight:
                GenerateKnight(piece, moveTiles, captureTiles);
                break;
            case PieceType.King:
                GenerateKing(piece, moveTiles, captureTiles);
                break;
        }
    }

    #endregion

    #region Generators

    void GeneratePawn(ChessPiece piece, List<ChessTile> moveTiles, List<ChessTile> captureTiles)
    {
        int direction = piece.Team == PieceTeam.White ? 1 : -1;
        int startRank = piece.Team == PieceTeam.White ? 1 : 6;
        int x = piece.CurrentTile.X;
        int y = piece.CurrentTile.Y;

        ChessTile oneForward = GetTile(x, y + direction);
        if (IsEmpty(oneForward))
        {
            moveTiles.Add(oneForward);

            bool isOnStartRank = y == startRank;
            ChessTile twoForward = GetTile(x, y + direction * 2);
            if (isOnStartRank && IsEmpty(twoForward))
            {
                moveTiles.Add(twoForward);
            }
        }

        TryAddCapture(piece, x - 1, y + direction, captureTiles);
        TryAddCapture(piece, x + 1, y + direction, captureTiles);
    }

    void GenerateKnight(ChessPiece piece, List<ChessTile> moveTiles, List<ChessTile> captureTiles)
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
            AddTileForTeam(piece.Team, GetTile(x + offsets[i].x, y + offsets[i].y), moveTiles, captureTiles);
        }
    }

    void GenerateKing(ChessPiece piece, List<ChessTile> moveTiles, List<ChessTile> captureTiles)
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

                AddTileForTeam(piece.Team, GetTile(x + dx, y + dy), moveTiles, captureTiles);
            }
        }
    }

    void GenerateSliding(ChessPiece piece, List<ChessTile> moveTiles, List<ChessTile> captureTiles, params Vector2Int[] directions)
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
                    moveTiles.Add(tile);
                }
                else
                {
                    if (occupant.Team != piece.Team)
                    {
                        captureTiles.Add(tile);
                    }

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

    void TryAddCapture(ChessPiece movingPiece, int x, int y, List<ChessTile> captureTiles)
    {
        ChessTile tile = GetTile(x, y);
        if (tile == null || tile.CurrentPiece == null)
        {
            return;
        }

        if (tile.CurrentPiece.Team != movingPiece.Team)
        {
            captureTiles.Add(tile);
        }
    }

    void AddTileForTeam(PieceTeam movingTeam, ChessTile tile, List<ChessTile> moveTiles, List<ChessTile> captureTiles)
    {
        if (tile == null)
        {
            return;
        }

        if (tile.CurrentPiece == null)
        {
            moveTiles.Add(tile);
            return;
        }

        if (tile.CurrentPiece.Team != movingTeam)
        {
            captureTiles.Add(tile);
        }
    }

    #endregion
}
