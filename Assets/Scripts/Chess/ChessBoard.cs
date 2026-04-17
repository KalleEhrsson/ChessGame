using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ChessBoard : MonoBehaviour
{
    const string BoardObjectName = "ChessBoard";
    const int BoardSize = 8;
    const string BlackPieceFolder = "Assets/Prefabs/ChessPieces/Black";
    const string WhitePieceFolder = "Assets/Prefabs/ChessPieces/White";

    public static ChessBoard Instance { get; private set; }

    #region Variables

    readonly ChessTile[,] tiles = new ChessTile[BoardSize, BoardSize];
    readonly Dictionary<string, ChessTile> tilesByName = new (StringComparer.OrdinalIgnoreCase);
    [SerializeField] GameObject[] whitePiecePrefabs = Array.Empty<GameObject>();
    [SerializeField] GameObject[] blackPiecePrefabs = Array.Empty<GameObject>();

    #endregion

    #region Unity

    void Awake()
    {
        RegisterInstance();
        RenameBoardObject();
        AutoSetupBoard();
    }

    void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SpawnStartingPosition();
    }

    void OnValidate()
    {
        RegisterInstance();
        RenameBoardObject();
        RefreshPiecePrefabReferences();
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
        if (discoveredTiles.Length != BoardSize * BoardSize)
        {
            Debug.LogWarning($"ChessBoard expected {BoardSize * BoardSize} tiles but found {discoveredTiles.Length}.");
        }

        if (discoveredTiles.Length == 0)
        {
            return;
        }

        Transform boardSpaceRoot = ResolveBoardSpaceRoot(discoveredTiles);
        Transform viewer = ResolveViewerTransform();

        List<TilePoint> points = BuildTilePoints(discoveredTiles, boardSpaceRoot);
        if (!TryResolveAxes(points, boardSpaceRoot, viewer, out AxisSelection axisSelection))
        {
            Debug.LogWarning("ChessBoard could not resolve board axes from tile layout.");
            return;
        }

        points.Sort((a, b) => axisSelection.DepthAscending
            ? a.Depth.CompareTo(b.Depth)
            : b.Depth.CompareTo(a.Depth));

        int maxTiles = Mathf.Min(points.Count, BoardSize * BoardSize);
        for (int y = 0; y < BoardSize; y++)
        {
            int rowStart = y * BoardSize;
            if (rowStart >= maxTiles)
            {
                break;
            }

            int rowCount = Mathf.Min(BoardSize, maxTiles - rowStart);
            List<TilePoint> row = points.GetRange(rowStart, rowCount);
            row.Sort((a, b) => axisSelection.FileAscending
                ? a.File.CompareTo(b.File)
                : b.File.CompareTo(a.File));

            for (int x = 0; x < row.Count; x++)
            {
                ChessTile tile = row[x].Tile;
                tile.SetCoordinates(x, y);

                tiles[x, y] = tile;
                tilesByName[tile.TileName] = tile;
            }
        }

        OrganizeTileHierarchy(boardSpaceRoot);
    }

    void SpawnStartingPosition()
    {
        AutoSetupBoard();
        ClearAllPieces();

        SpawnBackRank(PieceTeam.White, "A1", "B1", "C1", "D1", "E1", "F1", "G1", "H1");
        SpawnPawns(PieceTeam.White, 2);

        SpawnBackRank(PieceTeam.Black, "A8", "B8", "C8", "D8", "E8", "F8", "G8", "H8");
        SpawnPawns(PieceTeam.Black, 7);
    }

    void SpawnBackRank(PieceTeam team, string a, string b, string c, string d, string e, string f, string g, string h)
    {
        SpawnPiece(team, PieceType.Rook, a);
        SpawnPiece(team, PieceType.Knight, b);
        SpawnPiece(team, PieceType.Bishop, c);
        SpawnPiece(team, PieceType.Queen, d);
        SpawnPiece(team, PieceType.King, e);
        SpawnPiece(team, PieceType.Bishop, f);
        SpawnPiece(team, PieceType.Knight, g);
        SpawnPiece(team, PieceType.Rook, h);
    }

    void SpawnPawns(PieceTeam team, int rank)
    {
        for (char file = 'A'; file <= 'H'; file++)
        {
            SpawnPiece(team, PieceType.Pawn, $"{file}{rank}");
        }
    }

    ChessPiece SpawnPiece(PieceTeam team, PieceType type, string tileName)
    {
        ChessTile tile = GetTile(tileName);
        if (tile == null)
        {
            Debug.LogWarning($"ChessBoard could not find tile {tileName} for {team} {type}.");
            return null;
        }

        GameObject prefab = LoadPiecePrefab(team, type);
        if (prefab == null)
        {
            return null;
        }

        GameObject pieceObject = Instantiate(prefab, transform);
        pieceObject.name = $"{team}_{type}_{tileName}";

        ChessPiece piece = pieceObject.GetComponent<ChessPiece>();
        if (piece == null)
        {
            piece = pieceObject.AddComponent<ChessPiece>();
        }

        piece.SetIdentity(team, type);
        piece.SetTile(tile);

        return piece;
    }

    GameObject LoadPiecePrefab(PieceTeam team, PieceType type)
    {
        string prefabName = BuildPiecePrefabName(team, type);
        GameObject[] prefabs = team == PieceTeam.White ? whitePiecePrefabs : blackPiecePrefabs;
        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab != null && prefab.name.Equals(prefabName, StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        Debug.LogWarning($"Missing chess piece prefab reference for {prefabName}.");
        return null;
    }

    static string BuildPiecePrefabName(PieceTeam team, PieceType type)
    {
        return $"{team}_{type}";
    }
    
    void RefreshPiecePrefabReferences()
    {
#if UNITY_EDITOR
        whitePiecePrefabs = LoadPiecePrefabsFromFolder(WhitePieceFolder);
        blackPiecePrefabs = LoadPiecePrefabsFromFolder(BlackPieceFolder);
#endif
    }

#if UNITY_EDITOR
    static GameObject[] LoadPiecePrefabsFromFolder(string folderPath)
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:prefab", new[] { folderPath });
        List<GameObject> prefabs = new List<GameObject>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null)
            {
                prefabs.Add(prefab);
            }
        }

        return prefabs.ToArray();
    }
#endif

    void ClearAllPieces()
    {
        ChessPiece[] pieces = GetComponentsInChildren<ChessPiece>(true);
        for (int i = 0; i < pieces.Length; i++)
        {
            ChessPiece piece = pieces[i];
            if (piece == null)
            {
                continue;
            }

            piece.SetTile(null);
            if (Application.isPlaying)
            {
                Destroy(piece.gameObject);
            }
            else
            {
                DestroyImmediate(piece.gameObject);
            }
        }
    }

    ChessTile[] DiscoverTiles()
    {
        return GetComponentsInChildren<ChessTile>(true);
    }

    Transform ResolveBoardSpaceRoot(IReadOnlyList<ChessTile> discoveredTiles)
    {
        Transform candidate = discoveredTiles[0].transform.parent;
        if (candidate == null)
        {
            return transform;
        }

        for (int i = 1; i < discoveredTiles.Count; i++)
        {
            if (discoveredTiles[i].transform.parent != candidate)
            {
                return transform;
            }
        }

        return candidate;
    }

    Transform ResolveViewerTransform()
    {
        if (Camera.main != null)
        {
            return Camera.main.transform;
        }

        PlayerInteractionController interaction = FindFirstObjectByType<PlayerInteractionController>();
        if (interaction != null)
        {
            return interaction.transform;
        }

        return transform;
    }

    List<TilePoint> BuildTilePoints(IReadOnlyList<ChessTile> discoveredTiles, Transform boardSpaceRoot)
    {
        List<TilePoint> points = new List<TilePoint>(discoveredTiles.Count);
        for (int i = 0; i < discoveredTiles.Count; i++)
        {
            ChessTile tile = discoveredTiles[i];
            Vector3 local = boardSpaceRoot.InverseTransformPoint(tile.transform.position);
            points.Add(new TilePoint(tile, local));
        }

        return points;
    }

    bool TryResolveAxes(List<TilePoint> points, Transform boardSpaceRoot, Transform viewer, out AxisSelection axisSelection)
    {
        axisSelection = default;
        if (points.Count < BoardSize * BoardSize)
        {
            return false;
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 local = points[i].LocalPosition;
            minX = Mathf.Min(minX, local.x);
            maxX = Mathf.Max(maxX, local.x);
            minZ = Mathf.Min(minZ, local.z);
            maxZ = Mathf.Max(maxZ, local.z);
        }

        float rangeX = maxX - minX;
        float rangeZ = maxZ - minZ;
        if (rangeX <= Mathf.Epsilon || rangeZ <= Mathf.Epsilon)
        {
            return false;
        }

        Vector3 viewerLocal = boardSpaceRoot.InverseTransformPoint(viewer.position);
        float boardCenterX = (minX + maxX) * 0.5f;
        float boardCenterZ = (minZ + maxZ) * 0.5f;

        bool depthIsX = Mathf.Abs(viewerLocal.x - boardCenterX) > Mathf.Abs(viewerLocal.z - boardCenterZ);

        if (depthIsX)
        {
            float playerDepth = viewerLocal.x;
            float nearMin = Mathf.Abs(playerDepth - minX);
            float nearMax = Mathf.Abs(playerDepth - maxX);
            bool nearAtMin = nearMin < nearMax;
            axisSelection.DepthAscending = nearAtMin;

            Vector3 viewerRightLocal = boardSpaceRoot.InverseTransformDirection(viewer.right);
            axisSelection.FileAscending = viewerRightLocal.z > 0f;

            for (int i = 0; i < points.Count; i++)
            {
                TilePoint point = points[i];
                point.Depth = point.LocalPosition.x;
                point.File = point.LocalPosition.z;
                points[i] = point;
            }
        }
        else
        {
            float playerDepth = viewerLocal.z;
            float nearMin = Mathf.Abs(playerDepth - minZ);
            float nearMax = Mathf.Abs(playerDepth - maxZ);
            bool nearAtMin = nearMin < nearMax;
            axisSelection.DepthAscending = nearAtMin;

            Vector3 viewerRightLocal = boardSpaceRoot.InverseTransformDirection(viewer.right);
            axisSelection.FileAscending = viewerRightLocal.x > 0f;

            for (int i = 0; i < points.Count; i++)
            {
                TilePoint point = points[i];
                point.Depth = point.LocalPosition.z;
                point.File = point.LocalPosition.x;
                points[i] = point;
            }
        }

        return true;
    }

    void OrganizeTileHierarchy(Transform boardSpaceRoot)
    {
        if (boardSpaceRoot == null)
        {
            return;
        }

        int siblingIndex = 0;
        for (int y = BoardSize - 1; y >= 0; y--)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                ChessTile tile = tiles[x, y];
                if (tile == null || tile.transform.parent != boardSpaceRoot)
                {
                    continue;
                }

                tile.transform.SetSiblingIndex(siblingIndex);
                siblingIndex++;
            }
        }
    }

    #endregion

    #region Queries

    public ChessTile GetTile(int x, int y)
    {
        if (x < 0 || x >= BoardSize || y < 0 || y >= BoardSize)
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

    public ChessPiece GetPieceAt(int x, int y)
    {
        ChessTile tile = GetTile(x, y);
        return tile != null ? tile.CurrentPiece : null;
    }

    public ChessTile GetTileFromRaycast(RaycastHit hit)
    {
        return hit.collider != null ? hit.collider.GetComponentInParent<ChessTile>() : null;
    }

    public bool TryGetTeamFacingDirection(PieceTeam team, out Vector3 direction)
    {
        direction = Vector3.zero;
        int fromRank = team == PieceTeam.White ? 1 : 6;
        int toRank = team == PieceTeam.White ? 2 : 5;

        Vector3 accumulatedDirection = Vector3.zero;
        int validSamples = 0;

        for (int x = 0; x < BoardSize; x++)
        {
            ChessTile fromTile = GetTile(x, fromRank);
            ChessTile toTile = GetTile(x, toRank);
            if (fromTile == null || toTile == null)
            {
                continue;
            }

            Vector3 sampleDirection = toTile.transform.position - fromTile.transform.position;
            sampleDirection.y = 0f;
            if (sampleDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                continue;
            }

            accumulatedDirection += sampleDirection.normalized;
            validSamples++;
        }

        if (validSamples == 0)
        {
            return false;
        }

        direction = accumulatedDirection / validSamples;
        direction.y = 0f;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        direction.Normalize();
        return true;
    }

    public bool MovePiece(ChessTile from, ChessTile to)
    {
        if (from == null || to == null)
        {
            return false;
        }

        ChessPiece movingPiece = from.CurrentPiece;
        if (movingPiece == null)
        {
            return false;
        }

        if (to.CurrentPiece != null && to.CurrentPiece != movingPiece)
        {
            ChessPiece capturedPiece = to.CurrentPiece;
            capturedPiece.SetTile(null);
            if (Application.isPlaying)
            {
                Destroy(capturedPiece.gameObject);
            }
            else
            {
                DestroyImmediate(capturedPiece.gameObject);
            }
        }

        movingPiece.SetTile(to);
        return true;
    }

    #endregion

    #region Types

    struct TilePoint
    {
        public ChessTile Tile;
        public Vector3 LocalPosition;
        public float Depth;
        public float File;

        public TilePoint(ChessTile tile, Vector3 localPosition)
        {
            Tile = tile;
            LocalPosition = localPosition;
            Depth = 0f;
            File = 0f;
        }
    }

    struct AxisSelection
    {
        public bool DepthAscending;
        public bool FileAscending;
    }

    #endregion
}
