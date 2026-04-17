using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChessBoard : MonoBehaviour
{
    #region Singleton

    public static ChessBoard Instance { get; private set; }

    #endregion

    #region Variables

    readonly ChessTile[,] tiles = new ChessTile[8, 8];
    readonly Dictionary<string, ChessTile> tilesByName = new Dictionary<string, ChessTile>(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region Unity

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        AutoSetupBoard();
    }

    #endregion

    #region Setup

    public void AutoSetupBoard()
    {
        Array.Clear(tiles, 0, tiles.Length);
        tilesByName.Clear();

        ChessTile[] discoveredTiles = FindObjectsByType<ChessTile>(FindObjectsSortMode.None);

        if (discoveredTiles.Length != 64)
        {
            Debug.LogWarning($"ChessBoard expected 64 tiles but found {discoveredTiles.Length}.");
        }

        List<ChessTile> validTiles = new List<ChessTile>(discoveredTiles);
        validTiles.Sort((a, b) =>
        {
            Vector3 posA = a.transform.position;
            Vector3 posB = b.transform.position;

            int byRow = posA.z.CompareTo(posB.z);
            if (byRow != 0)
            {
                return byRow;
            }

            return posA.x.CompareTo(posB.x);
        });

        int maxTiles = Mathf.Min(validTiles.Count, 64);
        for (int i = 0; i < maxTiles; i++)
        {
            int x = i % 8;
            int y = i / 8;

            ChessTile tile = validTiles[i];
            tile.SetCoordinates(x, y);

            tiles[x, y] = tile;
            tilesByName[tile.TileName] = tile;
        }

        LogBoardMapping();
    }

    void LogBoardMapping()
    {
        for (int y = 7; y >= 0; y--)
        {
            string row = string.Empty;
            for (int x = 0; x < 8; x++)
            {
                ChessTile tile = tiles[x, y];
                row += tile != null ? $"[{tile.TileName}]" : "[--]";
            }
            Debug.Log($"Board Row {y + 1}: {row}");
        }
    }

    #endregion

    #region Queries

    public ChessTile GetTile(int x, int y)
    {
        if (x < 0 || x > 7 || y < 0 || y > 7)
        {
            return null;
        }

        return tiles[x, y];
    }

    public ChessTile GetTile(string tileName)
    {
        if (string.IsNullOrWhiteSpace(tileName))
        {
            return null;
        }

        return tilesByName.TryGetValue(tileName, out ChessTile tile) ? tile : null;
    }

    public ChessTile GetTileFromRaycast(RaycastHit hit)
    {
        return hit.collider != null ? hit.collider.GetComponentInParent<ChessTile>() : null;
    }

    #endregion
}
