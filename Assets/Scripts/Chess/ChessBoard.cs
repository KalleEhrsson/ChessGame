using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ChessBoard : MonoBehaviour
{
    #region Singleton

    const string BoardObjectName = "ChessBoard";
    public static ChessBoard Instance { get; private set; }

    #endregion

    #region Variables

    const string TileTag = "ChessTile";

    readonly ChessTile[,] tiles = new ChessTile[8, 8];
    readonly Dictionary<string, ChessTile> tilesByName = new Dictionary<string, ChessTile>(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region Unity

    void Awake()
    {
        RegisterInstance();
        RenameBoardObject();
        AutoSetupBoard();
    }

    void OnValidate()
    {
        RegisterInstance();
        RenameBoardObject();
        AutoSetupBoard();
    }

    #endregion

    #region Setup

    void RegisterInstance()
    {
        if (Instance == null || Instance == this)
        {
            Instance = this;
        }
    }

    void RenameBoardObject()
    {
        if (gameObject.name != BoardObjectName)
        {
            gameObject.name = BoardObjectName;
        }
    }

    public void AutoSetupBoard()
    {
        Array.Clear(tiles, 0, tiles.Length);
        tilesByName.Clear();

        ChessTile[] discoveredTiles = DiscoverTiles();

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

    ChessTile[] DiscoverTiles()
    {
        ChessTile[] directTiles = FindObjectsByType<ChessTile>(FindObjectsSortMode.None);

        GameObject[] taggedObjects;
        try
        {
            taggedObjects = GameObject.FindGameObjectsWithTag(TileTag);
        }
        catch (UnityException)
        {
            return directTiles;
        }

        if (taggedObjects == null || taggedObjects.Length == 0)
        {
            return directTiles;
        }

        HashSet<ChessTile> merged = new HashSet<ChessTile>(directTiles);
        for (int i = 0; i < taggedObjects.Length; i++)
        {
            ChessTile tile = taggedObjects[i].GetComponent<ChessTile>();
            if (tile == null)
            {
                tile = taggedObjects[i].AddComponent<ChessTile>();
            }

            merged.Add(tile);
        }

        ChessTile[] result = new ChessTile[merged.Count];
        merged.CopyTo(result);
        return result;
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

        if (tileName.StartsWith(ChessTile.TileObjectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            tileName = tileName.Substring(ChessTile.TileObjectPrefix.Length);
        }

        return tilesByName.TryGetValue(tileName, out ChessTile tile) ? tile : null;
    }

    public ChessTile GetTileFromRaycast(RaycastHit hit)
    {
        return hit.collider != null ? hit.collider.GetComponentInParent<ChessTile>() : null;
    }

    #endregion
}
