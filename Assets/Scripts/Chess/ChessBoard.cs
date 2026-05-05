using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ChessBoard : MonoBehaviour
{
    static readonly PieceType[] ValidPromotionTypes = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };
    const string BoardObjectName = "ChessBoard";
    const int BoardSize = 8;
    const string BlackPieceFolder = "Assets/Prefabs/ChessPieces/Black";
    const string WhitePieceFolder = "Assets/Prefabs/ChessPieces/White";

    public static ChessBoard Instance { get; private set; }

    #region Variables

    readonly ChessTile[,] tiles = new ChessTile[BoardSize, BoardSize];
    readonly Dictionary<string, ChessTile> tilesByName = new (StringComparer.OrdinalIgnoreCase);

    [Header("Piece Prefabs")]
    [Tooltip("Auto-populated white piece prefabs used when spawning the starting position.")]
    [SerializeField] GameObject[] whitePiecePrefabs = Array.Empty<GameObject>();
    [Tooltip("Auto-populated black piece prefabs used when spawning the starting position.")]
    [SerializeField] GameObject[] blackPiecePrefabs = Array.Empty<GameObject>();

    [Header("Broken Piece Capture FX")]
    [Tooltip("Registry that maps intact pieces to their broken-piece prefabs.")]
    [SerializeField] BrokenPiecePrefabRegistry brokenPiecePrefabRegistry;
    [Tooltip("Runtime container name created under the board for broken piece instances.")]
    [SerializeField] string brokenPiecesRootName = "BrokenPiecesRuntime";
    [Tooltip("Direct impact force applied to broken pieces on capture.")]
    [SerializeField, Min(0f)] float brokenPieceImpactForce = 10f;
    [Tooltip("Radial explosion force applied to nearby broken piece parts.")]
    [SerializeField, Min(0f)] float brokenPieceExplosionForce = 2f;
    [Tooltip("Explosion radius used when applying radial force.")]
    [SerializeField, Min(0.01f)] float brokenPieceExplosionRadius = 0.45f;
    [Tooltip("Upward lift modifier applied with explosion force.")]
    [SerializeField, Min(0f)] float brokenPieceUpwardModifier = 0.1f;
    [SerializeField, Min(0.01f)] float brokenShrinkFadeDuration = 0.45f;
    [SerializeField] AnimationCurve brokenShrinkCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] AnimationCurve brokenFadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] ParticleSystem brokenImpactParticlesPrefab;
    [SerializeField] bool destroyBrokenRootAfterCleanup = true;
    ChessMoveValidator moveValidator;
    ChessTurnManager turnManager;
    ChessGameStateController gameStateController;
    ChessCapturedPieceTray capturedPieceTray;
    ChessTile enPassantTargetTile;
    ChessPiece enPassantVulnerablePawn;
    Transform brokenPiecesRuntimeRoot;
    int halfMoveClock;
    int fullMoveNumber = 1;
    [SerializeField] bool debugMoveAnimationLogs;

    readonly HashSet<int> activeMoveAnimationOwners = new();

#if UNITY_EDITOR
    bool pendingAutoSetup;
    bool delayedHierarchyOrganizeQueued;
    bool delayedHierarchyOrganizeWaitingForUpdate;
#endif
    
    #endregion

    #region Events

    public event Action<ChessPiece, ChessTile, ChessTile> PieceMoved;

    #endregion

    #region State

    public ChessTile GetEnPassantTargetTile() => enPassantTargetTile;
    public ChessPiece GetEnPassantVulnerablePawn() => enPassantVulnerablePawn;
    public int GetHalfMoveClock() => halfMoveClock;
    public int GetFullMoveNumber() => Mathf.Max(1, fullMoveNumber);

    public string GetCastlingRightsFen()
    {
        string rights = string.Empty;
        AppendCastlingRight(PieceTeam.White, true, ref rights);
        AppendCastlingRight(PieceTeam.White, false, ref rights);
        AppendCastlingRight(PieceTeam.Black, true, ref rights);
        AppendCastlingRight(PieceTeam.Black, false, ref rights);
        return string.IsNullOrEmpty(rights) ? "-" : rights;
    }

    public string GetEnPassantTargetFen()
    {
        return enPassantTargetTile != null ? enPassantTargetTile.TileName.ToLowerInvariant() : "-";
    }

    #endregion

    #region Unity

    void Awake()
    {
        RegisterInstance();
        RenameBoardObject();
        if (Application.isPlaying)
        {
            AutoSetupBoard();
            return;
        }

        AutoSetupBoard(false);
#if UNITY_EDITOR
        QueueDelayedHierarchyOrganize();
#endif
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
        if (brokenPiecePrefabRegistry == null)
        {
            brokenPiecePrefabRegistry = GetComponent<BrokenPiecePrefabRegistry>();
        }
        if (Application.isPlaying)
        {
            return;
        }

#if UNITY_EDITOR
        pendingAutoSetup = true;
        UnityEditor.EditorApplication.delayCall -= PerformAutoSetupIfNeeded;
        UnityEditor.EditorApplication.delayCall += PerformAutoSetupIfNeeded;
#endif
    }

#if UNITY_EDITOR
    void OnDisable()
    {
        UnityEditor.EditorApplication.delayCall -= PerformAutoSetupIfNeeded;
        UnityEditor.EditorApplication.delayCall -= BeginDelayedOrganizeTileHierarchy;
        UnityEditor.EditorApplication.update -= DelayedOrganizeTileHierarchy;
    }
#endif

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

    public void AutoSetupBoard(bool organizeHierarchy = true)
    {
        Array.Clear(tiles, 0, tiles.Length);
        tilesByName.Clear();

        ChessTile[] discoveredTiles = DiscoverTiles();
        EnsureTileColliders(discoveredTiles);
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

        if (organizeHierarchy)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                QueueDelayedHierarchyOrganize();
                return;
            }
#endif
            OrganizeTileHierarchy(boardSpaceRoot);
        }
    }

    void SpawnStartingPosition()
    {
        AutoSetupBoard();
        capturedPieceTray ??= ChessCapturedPieceTray.GetOrCreate(this);
        capturedPieceTray?.ResetTrayState();
        ClearAllPieces();
        ResetSpecialState();
        ChessTurnManager.GetOrCreate().SetTurn(PieceTeam.White);
        ChessGameStateController.GetOrCreate().ResetToPlaying();
        ChessResignUiController.GetOrCreate().ResetForNewGame();
        ChessPauseManager.GetOrCreate().ResetPauseState();
        ChessWinScreenUI.GetOrCreate().Hide();

        SpawnBackRank(PieceTeam.White, "A1", "B1", "C1", "D1", "E1", "F1", "G1", "H1");
        SpawnPawns(PieceTeam.White, 2);

        SpawnBackRank(PieceTeam.Black, "A8", "B8", "C8", "D8", "E8", "F8", "G8", "H8");
        SpawnPawns(PieceTeam.Black, 7);
    }


    public void RestartMatch()
    {
        Debug.Log("[ChessBoard] Restarting match with full runtime reset.");
        Time.timeScale = 1f;

        ChessPauseManager.GetOrCreate().ResetPauseState();
        ChessPauseMenuUI.GetOrCreate().Hide();
        ChessDevSandboxController.Instance?.OpenDevMenuFromGameplay(false);
        ChessResignUiController.GetOrCreate().ResetForNewGame();
        ChessWinScreenUI.GetOrCreate().Hide();
        PawnPromotionController.GetOrCreate().ClearPendingState();

        ChessTileHighlighter.GetOrCreate().ClearAllHighlights();
        ChessSelectionController.GetOrCreate().Deselect();
        ChessTileHoverController.GetOrCreate().ClearHover();
        ChessCameraController.GetOrCreate().ExitTacticalView();
        ChessCursorStateCoordinator.SetTacticalCursorOverride(false);
        ChessCursorStateCoordinator.SetPauseCursorOverride(false);

        ChessTurnManager turnManager = ChessTurnManager.GetOrCreate();
        StockfishService stockfishService = StockfishService.GetOrCreate();
        stockfishService?.CancelThinking();

        SpawnStartingPosition();
        _ = turnManager.HandleDebugBoardSyncAsync(ChessFenBuilder.BuildFen(this, PieceTeam.White), true, string.Empty);
    }

    [ContextMenu("Rebuild Board")]
    void RebuildBoard()
    {
        AutoSetupBoard();
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

    public bool TrySnapPieceToTile(ChessPiece piece, ChessTile tile)
    {
        if (piece == null || tile == null)
        {
            Debug.LogWarning($"[ChessBoard] SnapPieceToTile failed. Piece='{piece?.name ?? "<null>"}', Tile='{tile?.name ?? "<null>"}', Reason=missing piece or tile.");
            return false;
        }

        Vector3 tileCenter = tile.transform.position;
        if (!IsFinite(tileCenter))
        {
            Debug.LogWarning($"[ChessBoard] SnapPieceToTile failed. Piece='{piece.name}', Tile='{tile.name}', Reason=invalid tile center.");
            return false;
        }

        piece.transform.position = new Vector3(tileCenter.x, tileCenter.y, tileCenter.z);

        bool hasTileSurface = TryResolveTileSurfaceY(tile, out float tileSurfaceY);
        bool hasPieceBottom = piece.TryGetBottomY(out float pieceBottomY);
        if (!hasTileSurface || !hasPieceBottom || !IsFinite(pieceBottomY))
        {
            Debug.LogWarning($"[ChessBoard] SnapPieceToTile fallback. Piece='{piece.name}', Tile='{tile.name}', Reason={(hasTileSurface ? "no piece bounds" : "no tile surface")}.");
            return false;
        }

        float verticalCorrection = tileSurfaceY - pieceBottomY;
        if (!IsFinite(verticalCorrection))
        {
            Debug.LogWarning($"[ChessBoard] SnapPieceToTile failed. Piece='{piece.name}', Tile='{tile.name}', Reason=invalid vertical correction.");
            return false;
        }

        piece.transform.position += Vector3.up * verticalCorrection;
        if (!IsFinite(piece.transform.position))
        {
            Debug.LogWarning($"[ChessBoard] SnapPieceToTile failed. Piece='{piece.name}', Tile='{tile.name}', Reason=invalid final position.");
            piece.transform.position = new Vector3(tileCenter.x, tileCenter.y, tileCenter.z);
            return false;
        }

        return true;
    }


    public bool TryGetPiecePrefab(PieceTeam team, PieceType type, out GameObject prefab)
    {
        prefab = LoadPiecePrefab(team, type);
        return prefab != null;
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

    void ResetSpecialState()
    {
        enPassantTargetTile = null;
        enPassantVulnerablePawn = null;
        halfMoveClock = 0;
        fullMoveNumber = 1;
    }

    ChessTile[] DiscoverTiles()
    {
        return GetComponentsInChildren<ChessTile>(true);
    }

    void EnsureTileColliders(IReadOnlyList<ChessTile> discoveredTiles)
    {
        for (int i = 0; i < discoveredTiles.Count; i++)
        {
            ChessTile tile = discoveredTiles[i];
            if (tile == null)
            {
                continue;
            }

            tile.EnsureInteractionCollider();
        }
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
    
#if UNITY_EDITOR
    void PerformAutoSetupIfNeeded()
    {
        UnityEditor.EditorApplication.delayCall -= PerformAutoSetupIfNeeded;
        if (Application.isPlaying || this == null || !pendingAutoSetup)
        {
            return;
        }

        pendingAutoSetup = false;
        AutoSetupBoard();
    }

    void QueueDelayedHierarchyOrganize()
    {
        if (delayedHierarchyOrganizeQueued)
        {
            return;
        }

        delayedHierarchyOrganizeQueued = true;
        UnityEditor.EditorApplication.delayCall += BeginDelayedOrganizeTileHierarchy;
    }

    void BeginDelayedOrganizeTileHierarchy()
    {
        if (this == null)
        {
            delayedHierarchyOrganizeQueued = false;
            delayedHierarchyOrganizeWaitingForUpdate = false;
            return;
        }

        if (delayedHierarchyOrganizeWaitingForUpdate)
        {
            return;
        }

        delayedHierarchyOrganizeWaitingForUpdate = true;
        UnityEditor.EditorApplication.update += DelayedOrganizeTileHierarchy;
    }

    void DelayedOrganizeTileHierarchy()
    {
        UnityEditor.EditorApplication.update -= DelayedOrganizeTileHierarchy;
        delayedHierarchyOrganizeQueued = false;
        delayedHierarchyOrganizeWaitingForUpdate = false;
        if (this == null)
        {
            return;
        }

        ChessTile[] discoveredTiles = DiscoverTiles();
        if (discoveredTiles.Length == 0)
        {
            return;
        }

        Transform boardSpaceRoot = ResolveBoardSpaceRoot(discoveredTiles);
        OrganizeTileHierarchy(boardSpaceRoot);
    }
#endif

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

    public ChessTile[] GetAllTiles()
    {
        List<ChessTile> results = new List<ChessTile>(BoardSize * BoardSize);
        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                ChessTile tile = tiles[x, y];
                if (tile != null)
                {
                    results.Add(tile);
                }
            }
        }

        if (results.Count > 0)
        {
            return results.ToArray();
        }

        return GetComponentsInChildren<ChessTile>(true);
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

    public ChessPiece[] GetAllPieces()
    {
        return GetComponentsInChildren<ChessPiece>(true);
    }

    public void ResetBoardToStartingPosition()
    {
        SpawnStartingPosition();
    }

    public void ClearBoardState(bool resetMetaState = true)
    {
        AutoSetupBoard();
        capturedPieceTray ??= ChessCapturedPieceTray.GetOrCreate(this);
        capturedPieceTray?.ResetTrayState();
        ClearAllPieces();
        if (resetMetaState)
        {
            ResetSpecialState();
        }
    }

    public bool TrySpawnPiece(PieceTeam team, PieceType type, ChessTile tile)
    {
        if (tile == null)
        {
            Debug.LogWarning("[ChessBoard] Cannot spawn piece on a null tile.");
            return false;
        }

        if (tile.CurrentPiece != null)
        {
            if (!TryRemovePiece(tile))
            {
                return false;
            }
        }

        ChessPiece piece = SpawnPiece(team, type, tile.TileName);
        if (piece == null)
        {
            Debug.LogWarning($"[ChessBoard] Failed to spawn {team} {type} on {tile.TileName}.");
            return false;
        }

        return true;
    }

    public bool TryRemovePiece(ChessTile tile)
    {
        if (tile == null)
        {
            Debug.LogWarning("[ChessBoard] Cannot remove from a null tile.");
            return false;
        }

        ChessPiece piece = tile.CurrentPiece;
        if (piece == null)
        {
            Debug.LogWarning($"[ChessBoard] Tile {tile.TileName} has no piece to remove.");
            return false;
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

        return true;
    }

    public bool TryRelocatePiece(ChessTile from, ChessTile to, bool overwriteDestination)
    {
        if (from == null || to == null)
        {
            Debug.LogWarning("[ChessBoard] Cannot relocate piece with null source or destination.");
            return false;
        }

        ChessPiece piece = from.CurrentPiece;
        if (piece == null)
        {
            Debug.LogWarning($"[ChessBoard] Source tile {from.TileName} has no piece to relocate.");
            return false;
        }

        if (to.CurrentPiece != null && to.CurrentPiece != piece)
        {
            if (!overwriteDestination)
            {
                Debug.LogWarning($"[ChessBoard] Destination tile {to.TileName} is occupied.");
                return false;
            }

            TryRemovePiece(to);
        }

        piece.SetTile(to);
        piece.MarkMoved();
        return true;
    }

    public void SetRuntimeState(ChessTile enPassantTile, ChessPiece enPassantPawn, int halfMove, int fullMove)
    {
        enPassantTargetTile = enPassantTile;
        enPassantVulnerablePawn = enPassantPawn;
        halfMoveClock = Mathf.Max(0, halfMove);
        fullMoveNumber = Mathf.Max(1, fullMove);
    }

    #region Dev Preset Sync

    public void RebuildRuntimeStateAfterDevPreset()
    {
        AutoSetupBoard();

        ChessTile[] allTiles = GetAllTiles();
        for (int i = 0; i < allTiles.Length; i++)
        {
            ChessTile tile = allTiles[i];
            if (tile == null)
            {
                continue;
            }

            ChessPiece tilePiece = tile.CurrentPiece;
            if (tilePiece == null)
            {
                continue;
            }

            if (tilePiece.CurrentTile != tile)
            {
                tile.SetCurrentPiece(null);
            }
        }

        ChessPiece[] allPieces = GetAllPieces();
        for (int i = 0; i < allPieces.Length; i++)
        {
            ChessPiece piece = allPieces[i];
            if (piece == null)
            {
                continue;
            }

            ChessTile tile = piece.CurrentTile;
            if (tile == null)
            {
                if (Application.isPlaying) Destroy(piece.gameObject); else DestroyImmediate(piece.gameObject);
                continue;
            }

            if (tile.CurrentPiece != piece)
            {
                piece.SetTile(tile);
            }

            piece.SnapToTile();
        }
    }

    #endregion
    
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

    void AppendCastlingRight(PieceTeam team, bool kingSide, ref string rights)
    {
        ChessPiece king = GetCastleKing(team);
        ChessPiece rook = GetCastleRook(team, kingSide);
        if (king == null || rook == null || king.HasMoved || rook.HasMoved)
        {
            return;
        }

        rights += team == PieceTeam.White
            ? (kingSide ? "K" : "Q")
            : (kingSide ? "k" : "q");
    }

    ChessPiece GetCastleKing(PieceTeam team)
    {
        ChessTile kingStart = GetTile(4, team == PieceTeam.White ? 0 : 7);
        ChessPiece king = kingStart != null ? kingStart.CurrentPiece : null;
        if (king == null || king.Team != team || king.Type != PieceType.King)
        {
            return null;
        }

        return king;
    }

    ChessPiece GetCastleRook(PieceTeam team, bool kingSide)
    {
        ChessTile rookStart = GetTile(kingSide ? 7 : 0, team == PieceTeam.White ? 0 : 7);
        ChessPiece rook = rookStart != null ? rookStart.CurrentPiece : null;
        if (rook == null || rook.Team != team || rook.Type != PieceType.Rook)
        {
            return null;
        }

        return rook;
    }

    public bool MovePiece(ChessTile from, ChessTile to, PieceType? promotionPiece = null)
    {
        if (from == null || to == null)
        {
            Debug.LogWarning($"[ChessBoard] MovePiece rejected: null endpoint. From={from?.TileName ?? "<null>"}, To={to?.TileName ?? "<null>"}");
            return false;
        }

        if (Application.isPlaying && ChessPieceMotion.IsAnyAnimating)
        {
            return false;
        }

        ChessPauseManager pauseManager = ChessPauseManager.GetOrCreate();
        if (pauseManager.IsPauseRequested)
        {
            Debug.LogWarning($"[ChessBoard] MovePiece blocked by pause request. From={from.TileName}, To={to.TileName}, Turn={turnManager?.GetCurrentTurn().ToString() ?? "<unknown>"}");
            return false;
        }

        ChessPiece movingPiece = from.CurrentPiece;
        if (movingPiece == null)
        {
            Debug.LogWarning($"[ChessBoard] MovePiece rejected: no piece on source tile {from.TileName}.");
            return false;
        }

        moveValidator ??= ChessMoveValidator.GetOrCreate();
        turnManager ??= ChessTurnManager.GetOrCreate();
        gameStateController ??= ChessGameStateController.GetOrCreate();

        if (gameStateController != null && !gameStateController.IsGameplayActive())
        {
            Debug.LogWarning($"[ChessBoard] MovePiece blocked because gameplay is not active. State={gameStateController.CurrentState}, From={from.TileName}, To={to.TileName}");
            return false;
        }

        if (turnManager != null && movingPiece.Team != turnManager.GetCurrentTurn())
        {
            Debug.LogWarning($"[ChessBoard] MovePiece rejected by turn. Piece={movingPiece.name}, Team={movingPiece.Team}, Type={movingPiece.Type}, From={from.TileName}, To={to.TileName}, CurrentTurn={turnManager.GetCurrentTurn()}");
            return false;
        }

        ChessMoveData moveData = default;
        if (moveValidator != null && !moveValidator.TryGetLegalMove(movingPiece, to, out moveData, promotionPiece))
        {
            Debug.LogWarning($"[ChessBoard] MovePiece rejected by legal move validation. Piece={movingPiece.name}, Team={movingPiece.Team}, Type={movingPiece.Type}, From={from.TileName}, To={to.TileName}, Turn={turnManager?.GetCurrentTurn().ToString() ?? "<unknown>"}, GameActive={(gameStateController != null && gameStateController.IsGameplayActive())}");
            return false;
        }
        else if (moveValidator == null)
        {
            return false;
        }

        if (moveData.IsPromotion && !promotionPiece.HasValue && turnManager != null && turnManager.IsHumanTurn(movingPiece.Team))
        {
            return false;
        }

        capturedPieceTray ??= ChessCapturedPieceTray.GetOrCreate(this);
        bool isCapture = moveData.IsCapture;
        ChessPiece capturedPiece = moveData.IsCapture && moveData.CaptureTile != null ? moveData.CaptureTile.CurrentPiece : null;

        Vector3 startWorldPosition = GetSafePiecePosition(movingPiece, from, Vector3.zero);
        if (moveData.IsCastle && moveData.CastleRookFrom != null && moveData.CastleRookTo != null)
        {
            ChessPiece rook = moveData.CastleRookFrom.CurrentPiece;
            if (rook == null)
            {
                return false;
            }

            rook.SetTile(moveData.CastleRookTo);
            rook.MarkMoved();
        }

        movingPiece.SetTile(to);
        movingPiece.MarkMoved();
        UpdateSpecialStateAfterMove(movingPiece, moveData, capturedPiece != null);

        ChessPiece animatedPiece = movingPiece;
        if (moveData.IsPromotion)
        {
            PieceType targetPromotion = promotionPiece ?? moveData.PromotionPieceType;
            if (!IsValidPromotionType(targetPromotion))
            {
                Debug.LogWarning($"[ChessBoard] Rejected invalid promotion type '{targetPromotion}' for move {from.TileName}->{to.TileName}.");
                return false;
            }

            animatedPiece = PromotePawn(movingPiece, targetPromotion);
            if (animatedPiece == null)
            {
                return false;
            }
        }

        Vector3 endWorldPosition = GetSafePiecePosition(animatedPiece, to, startWorldPosition);

        if (Application.isPlaying)
        {
            if (!IsFinite(startWorldPosition) || !IsFinite(endWorldPosition))
            {
                Debug.LogError($"[ChessBoard] Invalid move animation position. Piece={animatedPiece.name}, From={from.TileName}, To={to.TileName}, Start={startWorldPosition}, End={endWorldPosition}");
                return false;
            }

            _ = StartMoveAnimationAsync(animatedPiece, from, to, startWorldPosition, endWorldPosition, isCapture, capturedPiece);
            return true;
        }

        if (isCapture && capturedPiece != null)
        {
            ResolveCaptureOnImpact(capturedPiece);
        }

        turnManager?.SwitchTurn();

        if (turnManager != null)
        {
            gameStateController?.EvaluateEndOfTurn(turnManager.GetCurrentTurn());
        }

        PieceMoved?.Invoke(animatedPiece, from, to);
        return true;
    }

    async Task StartMoveAnimationAsync(ChessPiece piece, ChessTile fromTile, ChessTile toTile, Vector3 startWorldPosition, Vector3 endWorldPosition, bool isCapture, ChessPiece capturedPiece)
    {
        if (piece == null)
        {
            return;
        }

        int ownerId = piece.GetInstanceID();
        if (!activeMoveAnimationOwners.Add(ownerId))
        {
            LogMoveAnimation($"Animation ignored because another animation is already active. Piece={piece.name}, From={fromTile?.TileName}, To={toTile?.TileName}, Capture={isCapture}");
            return;
        }

        try
        {
            ChessPauseManager.GetOrCreate().NotifyRoundActionStarted();
            LogMoveAnimation($"{(isCapture ? "Capture" : "Move")} animation started. Piece={piece.name}, From={fromTile?.TileName}, To={toTile?.TileName}, Capture={isCapture}");
            await PlayMoveMotionAsync(piece, startWorldPosition, endWorldPosition, isCapture, capturedPiece, fromTile, toTile);
            LogMoveAnimation($"Animation completed. Piece={piece.name}, From={fromTile?.TileName}, To={toTile?.TileName}, Capture={isCapture}");

            turnManager ??= ChessTurnManager.GetOrCreate();
            gameStateController ??= ChessGameStateController.GetOrCreate();
            turnManager?.SwitchTurn();
            if (turnManager != null)
            {
                gameStateController?.EvaluateEndOfTurn(turnManager.GetCurrentTurn());
            }

            PieceMoved?.Invoke(piece, fromTile, toTile);
        }
        finally
        {
            activeMoveAnimationOwners.Remove(ownerId);
            ChessPauseManager.GetOrCreate().NotifyRoundActionFinished();
        }
    }

    async Task PlayMoveMotionAsync(ChessPiece piece, Vector3 startWorldPosition, Vector3 endWorldPosition, bool isCapture, ChessPiece capturedPiece, ChessTile fromTile, ChessTile toTile)
    {
        if (piece == null)
        {
            return;
        }

        ChessPieceMotion motion = piece.GetOrAddMotion();
        if (motion == null)
        {
            return;
        }

        bool captureResolved = false;
        void ResolveCaptureAtImpact(float impactT)
        {
            if (captureResolved || !isCapture || capturedPiece == null)
            {
                return;
            }

            captureResolved = true;
            Vector3 attackerDirection = endWorldPosition - startWorldPosition;
            if (debugMoveAnimationLogs)
            {
                LogMoveAnimation($"Capture impact callback triggered at t={impactT:0.###}. Captured={capturedPiece.name}");
            }

            ResolveCaptureOnImpact(capturedPiece, attackerDirection);
        }

        await motion.PlayMoveAsync(startWorldPosition, endWorldPosition, isCapture, capturedPiece, fromTile, toTile, debugMoveAnimationLogs, ResolveCaptureAtImpact);
        piece.SnapToTile();

        if (!isCapture || capturedPiece == null || captureResolved)
        {
            return;
        }

        if (debugMoveAnimationLogs)
        {
            LogMoveAnimation("Capture impact callback was not triggered during motion. Resolving on animation completion.");
        }

        Vector3 attackerDirection = endWorldPosition - startWorldPosition;
        ResolveCaptureOnImpact(capturedPiece, attackerDirection);
    }

    void LogMoveAnimation(string message)
    {
        if (!debugMoveAnimationLogs)
        {
            return;
        }

        Debug.Log($"[ChessBoard] {message}", this);
    }

    #region Capture Impact

    void ResolveCaptureOnImpact(ChessPiece capturedPiece)
    {
        ResolveCaptureOnImpact(capturedPiece, Vector3.zero);
    }

    void ResolveCaptureOnImpact(ChessPiece capturedPiece, Vector3 attackerDirection)
    {
        if (capturedPiece == null)
        {
            return;
        }

        Vector3 impactPosition = capturedPiece.transform.position;
        bool spawnedDebris = TrySpawnCaptureDebris(capturedPiece, impactPosition, capturedPiece.transform.rotation, attackerDirection);

        if (!spawnedDebris)
        {
            string expectedPath = BrokenPiecePrefabRegistry.GetExpectedBrokenPrefabAssetPath(capturedPiece.Team, capturedPiece.Type);
            Debug.LogWarning($"[ChessBoard] Missing broken prefab. Captured={capturedPiece.Team}{capturedPiece.Type}, ExpectedPath={expectedPath}");
        }

        bool placedInTray = capturedPieceTray != null && capturedPieceTray.PlaceCapturedPiece(capturedPiece);
        if (!placedInTray)
        {
            capturedPiece.SetTile(null);
            capturedPiece.gameObject.SetActive(false);
        }
    }

    bool TrySpawnCaptureDebris(ChessPiece capturedPiece, Vector3 position, Quaternion rotation, Vector3 attackerDirection)
    {
        if (capturedPiece == null)
        {
            return false;
        }

        GameObject brokenPrefab = LoadBrokenPiecePrefab(capturedPiece.Team, capturedPiece.Type);
        if (brokenPrefab == null)
        {
            return false;
        }

        Transform parent = GetOrCreateBrokenPiecesRoot();
        GameObject debrisRoot = Instantiate(brokenPrefab, position, rotation, parent);
        debrisRoot.name = $"{capturedPiece.Team}{capturedPiece.Type}BrokenRuntime";
        if (ShouldApplyCapturedPieceScale(brokenPrefab.transform))
        {
            debrisRoot.transform.localScale = capturedPiece.transform.lossyScale;
        }

        Rigidbody[] rigidbodies = PrepareBrokenPiecePhysics(debrisRoot);
        IgnoreDebrisCollisionWithPlayers(debrisRoot);
        if (rigidbodies.Length == 0)
        {
            return true;
        }

        Vector3 planarAttackerDirection = new Vector3(attackerDirection.x, 0f, attackerDirection.z);
        if (!IsFinite(planarAttackerDirection) || planarAttackerDirection.sqrMagnitude < 0.0001f)
        {
            planarAttackerDirection = Vector3.zero;
        }

        Vector3 forceDirection = (planarAttackerDirection.normalized + Vector3.down).normalized;
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody body = rigidbodies[i];
            if (body == null)
            {
                continue;
            }

            body.isKinematic = false;
            body.detectCollisions = true;
            body.AddForce(forceDirection * brokenPieceImpactForce, ForceMode.Impulse);
            body.AddExplosionForce(
                brokenPieceExplosionForce,
                position,
                brokenPieceExplosionRadius,
                brokenPieceUpwardModifier,
                ForceMode.Impulse);
        }

        AttachBrokenCleanupEffect(debrisRoot, position);
        return true;
    }

    void IgnoreDebrisCollisionWithPlayers(GameObject debrisRoot)
    {
        if (debrisRoot == null)
        {
            return;
        }

        Collider[] debrisColliders = debrisRoot.GetComponentsInChildren<Collider>(true);
        if (debrisColliders.Length == 0)
        {
            return;
        }

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
        {
            PlayerController player = players[playerIndex];
            if (player == null)
            {
                continue;
            }

            Collider[] playerColliders = player.GetComponentsInChildren<Collider>(true);
            for (int playerColliderIndex = 0; playerColliderIndex < playerColliders.Length; playerColliderIndex++)
            {
                Collider playerCollider = playerColliders[playerColliderIndex];
                if (playerCollider == null)
                {
                    continue;
                }

                for (int debrisColliderIndex = 0; debrisColliderIndex < debrisColliders.Length; debrisColliderIndex++)
                {
                    Collider debrisCollider = debrisColliders[debrisColliderIndex];
                    if (debrisCollider == null)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(debrisCollider, playerCollider, true);
                }
            }
        }
    }

    void AttachBrokenCleanupEffect(GameObject debrisRoot, Vector3 impactPosition)
    {
        if (debrisRoot == null)
        {
            return;
        }

        BrokenPieceCleanupEffect cleanup = debrisRoot.GetComponent<BrokenPieceCleanupEffect>();
        if (cleanup == null)
        {
            cleanup = debrisRoot.AddComponent<BrokenPieceCleanupEffect>();
        }

        cleanup.Initialize(
            brokenShrinkFadeDuration,
            brokenShrinkCurve,
            brokenFadeCurve,
            brokenImpactParticlesPrefab,
            destroyBrokenRootAfterCleanup,
            impactPosition);
    }

    #endregion

    #region Broken Piece Prefabs

    GameObject LoadBrokenPiecePrefab(PieceTeam team, PieceType type)
    {
        if (brokenPiecePrefabRegistry == null)
        {
            brokenPiecePrefabRegistry = GetComponent<BrokenPiecePrefabRegistry>();
        }

        return brokenPiecePrefabRegistry != null
            ? brokenPiecePrefabRegistry.GetBrokenPrefab(team, type)
            : null;
    }

    Transform GetOrCreateBrokenPiecesRoot()
    {
        if (brokenPiecesRuntimeRoot != null)
        {
            return brokenPiecesRuntimeRoot;
        }

        string rootName = string.IsNullOrWhiteSpace(brokenPiecesRootName) ? "BrokenPiecesRuntime" : brokenPiecesRootName;
        GameObject existing = GameObject.Find(rootName);
        if (existing != null)
        {
            brokenPiecesRuntimeRoot = existing.transform;
            return brokenPiecesRuntimeRoot;
        }

        GameObject runtimeRoot = new GameObject(rootName);
        brokenPiecesRuntimeRoot = runtimeRoot.transform;
        return brokenPiecesRuntimeRoot;
    }

    static bool ShouldApplyCapturedPieceScale(Transform prefabTransform)
    {
        if (prefabTransform == null)
        {
            return false;
        }

        Vector3 scale = prefabTransform.localScale;
        return Mathf.Approximately(scale.x, 1f) &&
               Mathf.Approximately(scale.y, 1f) &&
               Mathf.Approximately(scale.z, 1f);
    }

    Rigidbody[] PrepareBrokenPiecePhysics(GameObject brokenRoot)
    {
        if (brokenRoot == null)
        {
            return Array.Empty<Rigidbody>();
        }

        List<Rigidbody> bodies = new List<Rigidbody>(16);
        Transform rootTransform = brokenRoot.transform;
        bool rootIsVisibleMesh = IsVisibleOrCollisionPart(rootTransform);
        for (int i = 0; i < rootTransform.childCount; i++)
        {
            TryAddPhysicsRecursive(rootTransform.GetChild(i), bodies);
        }

        if (rootTransform.childCount == 0 && rootIsVisibleMesh)
        {
            EnsurePhysicsComponents(rootTransform, bodies);
        }

        return bodies.ToArray();
    }

    void TryAddPhysicsRecursive(Transform current, List<Rigidbody> bodies)
    {
        if (current == null)
        {
            return;
        }

        if (IsVisibleOrCollisionPart(current))
        {
            EnsurePhysicsComponents(current, bodies);
        }

        for (int i = 0; i < current.childCount; i++)
        {
            TryAddPhysicsRecursive(current.GetChild(i), bodies);
        }
    }

    static bool IsVisibleOrCollisionPart(Transform part)
    {
        if (part == null)
        {
            return false;
        }

        return part.GetComponent<MeshRenderer>() != null || part.GetComponent<Collider>() != null;
    }

    void EnsurePhysicsComponents(Transform part, List<Rigidbody> bodies)
    {
        if (part == null)
        {
            return;
        }

        Collider collider = part.GetComponent<Collider>();
        if (collider == null)
        {
            MeshFilter meshFilter = part.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                if (CanBuildConvexCollider(meshFilter.sharedMesh))
                {
                    MeshCollider meshCollider = part.gameObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshFilter.sharedMesh;
                    meshCollider.convex = true;
                    collider = meshCollider;
                }
                else
                {
                    collider = part.gameObject.AddComponent<BoxCollider>();
                }
            }
        }

        Rigidbody body = part.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = part.gameObject.AddComponent<Rigidbody>();
        }

        if (body != null)
        {
            bodies.Add(body);
        }
    }

    static bool CanBuildConvexCollider(Mesh mesh)
    {
        if (mesh == null)
        {
            return false;
        }

        if (!mesh.isReadable)
        {
            return false;
        }

        Vector3[] vertices = mesh.vertices;
        if (vertices == null || vertices.Length < 4)
        {
            return false;
        }

        const float minDistanceSqr = 0.000001f;
        int uniqueCount = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            bool isUnique = true;
            for (int j = 0; j < i; j++)
            {
                if ((vertices[i] - vertices[j]).sqrMagnitude <= minDistanceSqr)
                {
                    isUnique = false;
                    break;
                }
            }

            if (!isUnique)
            {
                continue;
            }

            uniqueCount++;
            if (uniqueCount >= 4)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    void UpdateSpecialStateAfterMove(ChessPiece movingPiece, ChessMoveData moveData, bool didCapture)
    {
        bool isPawnMove = movingPiece != null && movingPiece.Type == PieceType.Pawn;
        if (isPawnMove || didCapture)
        {
            halfMoveClock = 0;
        }
        else
        {
            halfMoveClock++;
        }

        if (movingPiece != null && movingPiece.Team == PieceTeam.Black)
        {
            fullMoveNumber++;
        }

        enPassantTargetTile = null;
        enPassantVulnerablePawn = null;
        if (!isPawnMove || moveData.FromTile == null || moveData.ToTile == null)
        {
            return;
        }

        int moveDistance = Mathf.Abs(moveData.ToTile.Y - moveData.FromTile.Y);
        if (moveDistance != 2)
        {
            return;
        }

        int intermediateY = (moveData.FromTile.Y + moveData.ToTile.Y) / 2;
        enPassantTargetTile = GetTile(moveData.FromTile.X, intermediateY);
        enPassantVulnerablePawn = movingPiece;
    }

    ChessPiece PromotePawn(ChessPiece pawn, PieceType promotionType)
    {
        if (pawn == null || pawn.CurrentTile == null || pawn.Type != PieceType.Pawn)
        {
            return pawn;
        }

        if (!IsValidPromotionType(promotionType))
        {
            Debug.LogWarning($"[ChessBoard] PromotePawn rejected invalid promotion type '{promotionType}'.");
            return null;
        }

        ChessTile promotionTile = pawn.CurrentTile;
        GameObject prefab = LoadPiecePrefab(pawn.Team, promotionType);
        if (prefab == null)
        {
            pawn.SetType(promotionType);
            return pawn;
        }

        GameObject promotedObject = Instantiate(prefab, transform);
        promotedObject.name = $"{pawn.Team}_{promotionType}_{promotionTile.TileName}";
        ChessPiece promotedPiece = promotedObject.GetComponent<ChessPiece>();
        if (promotedPiece == null)
        {
            promotedPiece = promotedObject.AddComponent<ChessPiece>();
        }

        promotedPiece.SetIdentity(pawn.Team, promotionType);
        promotedPiece.SetTile(promotionTile);
        promotedPiece.MarkMoved();

        pawn.SetTile(null);
        if (Application.isPlaying)
        {
            Destroy(pawn.gameObject);
        }
        else
        {
            DestroyImmediate(pawn.gameObject);
        }

        return promotedPiece;
    }

    static bool IsValidPromotionType(PieceType promotionType)
    {
        for (int i = 0; i < ValidPromotionTypes.Length; i++)
        {
            if (ValidPromotionTypes[i] == promotionType)
            {
                return true;
            }
        }

        return false;
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
    
    Vector3 GetSafePiecePosition(ChessPiece piece, ChessTile tile, Vector3 fallback)
    {
        if (piece != null && IsFinite(piece.transform.position))
        {
            return piece.transform.position;
        }

        if (tile != null && IsFinite(tile.transform.position))
        {
            return tile.transform.position;
        }

        return fallback;
    }

    static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    static bool TryResolveTileSurfaceY(ChessTile tile, out float tileSurfaceY)
    {
        tileSurfaceY = 0f;
        if (tile == null)
        {
            return false;
        }

        Collider tileCollider = tile.GetComponent<Collider>();
        if (tileCollider != null)
        {
            float colliderTop = tileCollider.bounds.max.y;
            if (IsFinite(colliderTop))
            {
                tileSurfaceY = colliderTop;
                return true;
            }
        }

        Renderer tileRenderer = tile.GetComponent<Renderer>();
        if (tileRenderer != null)
        {
            float rendererTop = tileRenderer.bounds.max.y;
            if (IsFinite(rendererTop))
            {
                tileSurfaceY = rendererTop;
                return true;
            }
        }

        float fallbackY = tile.transform.position.y;
        if (IsFinite(fallbackY))
        {
            tileSurfaceY = fallbackY;
            return true;
        }

        return false;
    }
}
