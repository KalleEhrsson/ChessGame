using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChessCapturedPieceTray : MonoBehaviour
{
    public enum CapturedTraySideAxis
    {
        FileLeftRight,
        RankForwardBack
    }

    const string TrayRootName = "CapturedPiecesAnchors";
    const string WhiteCapturedAreaName = "WhiteCapturedPiecesAnchor";
    const string BlackCapturedAreaName = "BlackCapturedPiecesAnchor";
    const string LegacyWhiteCapturedAreaName = "WhiteCapturedArea";
    const string LegacyBlackCapturedAreaName = "BlackCapturedArea";

    #region Variables

    [SerializeField] ChessBoard board;
    [SerializeField] Transform capturedAnchorsRoot;
    [SerializeField] Transform whiteCapturedArea;
    [SerializeField] Transform blackCapturedArea;
    [SerializeField, Min(0.05f)] float capturedTrayDistanceFromBoard = 0.35f;
    [SerializeField, Min(0.05f), Tooltip("Distance between different piece-type rows.")] float capturedTrayRowSpacing = 0.32f;
    [SerializeField, Min(0.01f), Tooltip("Distance between pieces of the same type.")] float capturedTrayColumnSpacing = 0.08f;
    [SerializeField, Min(0.01f), Tooltip("Extra spacing added from piece bounds.")] float capturedTrayPiecePadding = 0.02f;
    [SerializeField, Min(0.01f)] float minCapturedTrayColumnSpacing = 0.08f;
    [SerializeField, Min(0.01f)] float maxCapturedTrayColumnSpacing = 0.16f;
    [SerializeField, Min(0.05f)] float minCapturedTrayRowSpacing = 0.28f;
    [SerializeField, Min(0.05f)] float maxCapturedTrayRowSpacing = 0.5f;
    [SerializeField] float capturedTrayYOffset = -0.02f;
    [SerializeField, Min(0f)] float capturedTraySidePadding = 0.1f;
    [SerializeField] CapturedTraySideAxis capturedTraySideAxis = CapturedTraySideAxis.FileLeftRight;
    [SerializeField] bool placeBothCapturedTraysOnSameSide;
    [SerializeField, Min(0f)] float sameSideTraySeparation = 0.6f;
    [SerializeField] Vector3 displayEulerOffset = Vector3.zero;
    [SerializeField, Min(0.1f)] float maxCapturedTrayGroundingCorrection = 2.5f;
    [SerializeField] bool enableCapturedTrayDebugLogs;

    readonly Dictionary<RowKey, int> rowCounts = new();
    readonly HashSet<int> placedPieces = new();

    #endregion

    #region Setup

    public static ChessCapturedPieceTray GetOrCreate(ChessBoard targetBoard)
    {
        if (targetBoard == null)
        {
            return null;
        }

        ChessCapturedPieceTray tray = targetBoard.GetComponent<ChessCapturedPieceTray>();
        if (tray == null)
        {
            tray = targetBoard.gameObject.AddComponent<ChessCapturedPieceTray>();
        }

        tray.Initialize(targetBoard);
        return tray;
    }

    public void Initialize(ChessBoard targetBoard)
    {
        board = targetBoard != null ? targetBoard : board;
        ResolveTrayTransforms();
    }

    public void ResetTrayState()
    {
        rowCounts.Clear();
        placedPieces.Clear();
        ResolveTrayTransforms();
    }

    #endregion

    #region Capture Placement

    public bool PlaceCapturedPiece(ChessPiece piece)
    {
        if (piece == null)
        {
            return false;
        }

        ResolveTrayTransforms();

        Transform trayArea = piece.Team == PieceTeam.White ? whiteCapturedArea : blackCapturedArea;
        if (trayArea == null)
        {
            return false;
        }

        int pieceId = piece.GetInstanceID();
        if (placedPieces.Contains(pieceId))
        {
            return true;
        }

        piece.SetSelected(false);
        piece.SetTile(null);

        int row = Mathf.Clamp(GetRowIndex(piece.Type), 0, 5);
        RowKey key = new(piece.Team, row);
        rowCounts.TryGetValue(key, out int pieceCountInRow);
        rowCounts[key] = pieceCountInRow + 1;

        float rowStep = ResolveRowStepForArea(trayArea);
        float columnStep = ResolvePieceColumnStep(piece);
        Quaternion targetLocalRotation = Quaternion.Euler(displayEulerOffset);
        Vector3 targetLocalPosition = BuildLocalPosition(row, pieceCountInRow, columnStep, rowStep);

        piece.transform.SetParent(trayArea, false);
        piece.transform.SetLocalPositionAndRotation(targetLocalPosition, targetLocalRotation);
        GroundPieceOnTray(piece, trayArea);

        DisableBoardGameplayInteraction(piece);

        placedPieces.Add(pieceId);
        return true;
    }


    public bool HasCapturedPieceOfType(PieceTeam team, PieceType type)
    {
        Transform trayArea = team == PieceTeam.White ? whiteCapturedArea : blackCapturedArea;
        if (trayArea == null)
        {
            return false;
        }

        for (int i = 0; i < trayArea.childCount; i++)
        {
            ChessPiece piece = trayArea.GetChild(i).GetComponent<ChessPiece>();
            if (piece != null && piece.Team == team && piece.Type == type)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetPromotionRowPose(PieceTeam team, float heightOffset, float forwardOffset, out Vector3 rowOrigin, out Vector3 rowRight, out Quaternion rowRotation)
    {
        ResolveTrayTransforms();
        Transform trayArea = team == PieceTeam.White ? whiteCapturedArea : blackCapturedArea;
        if (trayArea == null)
        {
            rowOrigin = default;
            rowRight = Vector3.right;
            rowRotation = Quaternion.identity;
            return false;
        }

        rowOrigin = trayArea.position + (trayArea.up * heightOffset) + (trayArea.forward * forwardOffset);
        rowRight = trayArea.right;
        rowRotation = trayArea.rotation * Quaternion.Euler(displayEulerOffset);
        return true;
    }

    #endregion

    #region Helpers

    void OnValidate()
    {
        minCapturedTrayColumnSpacing = Mathf.Max(0.01f, minCapturedTrayColumnSpacing);
        maxCapturedTrayColumnSpacing = Mathf.Max(minCapturedTrayColumnSpacing, maxCapturedTrayColumnSpacing);
        minCapturedTrayRowSpacing = Mathf.Max(0.05f, minCapturedTrayRowSpacing);
        maxCapturedTrayRowSpacing = Mathf.Max(minCapturedTrayRowSpacing, maxCapturedTrayRowSpacing);
        capturedTrayColumnSpacing = Mathf.Clamp(capturedTrayColumnSpacing, minCapturedTrayColumnSpacing, maxCapturedTrayColumnSpacing);
        capturedTrayRowSpacing = Mathf.Clamp(capturedTrayRowSpacing, minCapturedTrayRowSpacing, maxCapturedTrayRowSpacing);

        if (!Application.isPlaying)
        {
            ResolveTrayTransforms();
        }
    }

    void ResolveTrayTransforms()
    {
        if (board == null)
        {
            board = GetComponent<ChessBoard>();
        }

        if (capturedAnchorsRoot == null)
        {
            capturedAnchorsRoot = FindOrCreateAnchorsRoot();
        }

        if (whiteCapturedArea == null)
        {
            whiteCapturedArea = FindOrCreateTrayTransform(WhiteCapturedAreaName, LegacyWhiteCapturedAreaName);
        }

        if (blackCapturedArea == null)
        {
            blackCapturedArea = FindOrCreateTrayTransform(BlackCapturedAreaName, LegacyBlackCapturedAreaName);
        }

        RepositionTrayAnchors();
    }

    Transform FindOrCreateAnchorsRoot()
    {
        Transform root = transform.Find(TrayRootName);
        if (root != null)
        {
            return root;
        }

        GameObject rootObject = new(TrayRootName);
        root = rootObject.transform;
        root.SetParent(transform, false);
        root.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        return root;
    }

    Transform FindOrCreateTrayTransform(string trayName, string legacyName)
    {
        Transform existing = capturedAnchorsRoot != null ? capturedAnchorsRoot.Find(trayName) : null;
        if (existing == null && !string.IsNullOrWhiteSpace(legacyName))
        {
            existing = transform.Find(legacyName);
            if (existing != null)
            {
                existing.name = trayName;
                existing.SetParent(capturedAnchorsRoot, true);
            }
        }

        if (existing != null)
        {
            return existing;
        }

        GameObject trayObject = new(trayName);
        Transform trayTransform = trayObject.transform;
        trayTransform.SetParent(capturedAnchorsRoot, false);
        trayTransform.localRotation = Quaternion.identity;
        return trayTransform;
    }

    void RepositionTrayAnchors()
    {
        if (whiteCapturedArea == null || blackCapturedArea == null)
        {
            return;
        }

        if (!TryGetBoardMetrics(out BoardMetrics metrics))
        {
            Debug.LogWarning("[ChessCapturedPieceTray] Cannot place captured piece anchors because board bounds or tile spacing could not be calculated.");
            return;
        }

        Vector3 sideAxis = capturedTraySideAxis == CapturedTraySideAxis.RankForwardBack ? metrics.RankAxis : metrics.FileAxis;
        Vector3 parallelAxis = capturedTraySideAxis == CapturedTraySideAxis.RankForwardBack ? metrics.FileAxis : metrics.RankAxis;

        float sideDistance = metrics.GetHalfExtentAlong(sideAxis) + capturedTrayDistanceFromBoard + capturedTraySidePadding;
        Vector3 upOffset = metrics.Up * capturedTrayYOffset;

        Vector3 whitePosition = metrics.Center + (sideAxis * sideDistance) + upOffset;
        Vector3 blackPosition = metrics.Center - (sideAxis * sideDistance) + upOffset;
        if (placeBothCapturedTraysOnSameSide)
        {
            float sameSideOffset = Mathf.Max(sameSideTraySeparation, capturedTrayColumnSpacing * 2f);
            whitePosition = metrics.Center + (sideAxis * sideDistance) + (parallelAxis * (sameSideOffset * -0.5f)) + upOffset;
            blackPosition = metrics.Center + (sideAxis * sideDistance) + (parallelAxis * (sameSideOffset * 0.5f)) + upOffset;
        }

        Quaternion whiteTrayRotation = Quaternion.LookRotation(sideAxis, metrics.Up);
        Quaternion blackTrayRotation = placeBothCapturedTraysOnSameSide
            ? Quaternion.LookRotation(sideAxis, metrics.Up)
            : Quaternion.LookRotation(-sideAxis, metrics.Up);
        whiteCapturedArea.SetPositionAndRotation(whitePosition, whiteTrayRotation);
        blackCapturedArea.SetPositionAndRotation(blackPosition, blackTrayRotation);

        if (enableCapturedTrayDebugLogs)
        {
            /*
            Debug.Log(
                $"[ChessCapturedPieceTray] axis={capturedTraySideAxis}, center={metrics.Center}, tileBoardSize={metrics.Bounds.size}, " +
                $"fileAxis={metrics.FileAxis}, rankAxis={metrics.RankAxis}, sideAxis={sideAxis}, parallelAxis={parallelAxis}, " +
                $"whiteWorld={whiteCapturedArea.position}, blackWorld={blackCapturedArea.position}, " +
                $"whiteLocal={whiteCapturedArea.localPosition}, blackLocal={blackCapturedArea.localPosition}");
            */
        }

        if (metrics.IsInsideBoardExtents(whiteCapturedArea.position) || metrics.IsInsideBoardExtents(blackCapturedArea.position))
        {
            Debug.LogWarning("[ChessCapturedPieceTray] One or more captured piece anchors is inside the board bounds.");
        }

        ReflowPlacedPieces();
    }

    Vector3 BuildLocalPosition(int row, int column, float columnStep, float rowStep)
    {
        Vector3 localPosition = (Vector3.forward * (row * rowStep))
            + (Vector3.right * (column * columnStep));
        localPosition.y = 0f;
        return localPosition;
    }

    void ReflowPlacedPieces()
    {
        ReflowArea(whiteCapturedArea, PieceTeam.White);
        ReflowArea(blackCapturedArea, PieceTeam.Black);
    }

    void ReflowArea(Transform trayArea, PieceTeam team)
    {
        if (trayArea == null)
        {
            return;
        }

        Dictionary<int, int> columnsByRow = new();
        float rowStep = ResolveRowStepForArea(trayArea);
        for (int i = 0; i < trayArea.childCount; i++)
        {
            Transform child = trayArea.GetChild(i);
            if (child == null)
            {
                continue;
            }

            ChessPiece piece = child.GetComponent<ChessPiece>();
            if (piece == null || piece.Team != team)
            {
                continue;
            }

            int row = Mathf.Clamp(GetRowIndex(piece.Type), 0, 5);
            columnsByRow.TryGetValue(row, out int column);
            float columnStep = ResolvePieceColumnStep(piece);
            child.SetLocalPositionAndRotation(BuildLocalPosition(row, column, columnStep, rowStep), Quaternion.Euler(displayEulerOffset));
            GroundPieceOnTray(piece, trayArea);
            columnsByRow[row] = column + 1;
        }
    }

    void GroundPieceOnTray(ChessPiece piece, Transform trayArea)
    {
        if (piece == null || trayArea == null)
        {
            return;
        }

        Vector3 trayUp = SafeNormalized(trayArea.up, Vector3.up);
        Vector3 desiredBottomWorldPoint = trayArea.position + (trayUp * capturedTrayYOffset);
        float desiredBottomProjection = Vector3.Dot(desiredBottomWorldPoint, trayUp);
        string boundsSource = "none";
        if (!TryGetCapturedPieceBounds(piece.transform, out Bounds pieceBounds, out boundsSource))
        {
            return;
        }

        float currentBottomProjection = GetBoundsMinProjection(pieceBounds, trayUp);
        float correction = desiredBottomProjection - currentBottomProjection;
        float clampedCorrection = Mathf.Clamp(correction, -maxCapturedTrayGroundingCorrection, maxCapturedTrayGroundingCorrection);

        if (!Mathf.Approximately(clampedCorrection, correction))
        {
            Debug.LogWarning(
                $"[ChessCapturedPieceTray] Grounding correction clamped for {piece.name}. " +
                $"requested={correction:F4}, clamped={clampedCorrection:F4}, bounds={pieceBounds}, " +
                $"pieceWorld={piece.transform.position}, trayWorld={trayArea.position}");
        }

        piece.transform.position += trayUp * clampedCorrection;

        if (enableCapturedTrayDebugLogs)
        {
            Debug.Log(
                $"[ChessCapturedPieceTray] Grounding {piece.name}: boundsSource={boundsSource}, " +
                $"currentBottom={currentBottomProjection:F4}, desiredBottom={desiredBottomProjection:F4}, correction={clampedCorrection:F4}, " +
                $"finalWorld={piece.transform.position}, finalLocal={piece.transform.localPosition}");
        }
    }

    float ResolvePieceColumnStep(ChessPiece piece)
    {
        if (piece != null && TryGetPieceBounds(piece, out Bounds bounds))
        {
            float width = Mathf.Max(bounds.size.x, bounds.size.z);
            if (width > 0.001f && float.IsFinite(width))
            {
                return Mathf.Clamp(width + capturedTrayPiecePadding, minCapturedTrayColumnSpacing, maxCapturedTrayColumnSpacing);
            }
        }

        return Mathf.Clamp(capturedTrayColumnSpacing, minCapturedTrayColumnSpacing, maxCapturedTrayColumnSpacing);
    }

    float ResolveRowStepForArea(Transform trayArea)
    {
        float largestFootprint = 0f;
        for (int i = 0; i < trayArea.childCount; i++)
        {
            Transform child = trayArea.GetChild(i);
            if (child == null)
            {
                continue;
            }

            ChessPiece piece = child.GetComponent<ChessPiece>();
            if (piece == null)
            {
                continue;
            }

            if (TryGetPieceBounds(piece, out Bounds bounds))
            {
                largestFootprint = Mathf.Max(largestFootprint, Mathf.Max(bounds.size.x, bounds.size.z));
            }
        }

        float rowFromBounds = largestFootprint > 0.001f
            ? largestFootprint + capturedTrayPiecePadding
            : minCapturedTrayRowSpacing;
        float preferredRowSpacing = Mathf.Max(capturedTrayRowSpacing, rowFromBounds);
        return Mathf.Clamp(preferredRowSpacing, minCapturedTrayRowSpacing, maxCapturedTrayRowSpacing);
    }

    bool TryGetBoardMetrics(out BoardMetrics metrics)
    {
        metrics = default;
        ChessTile[] tiles = board != null ? board.GetAllTiles() : null;
        if (tiles == null || tiles.Length == 0)
        {
            return false;
        }

        if (!TryBuildBoardBounds(tiles, out Bounds bounds))
        {
            return false;
        }

        if (!TryResolveBoardAxes(tiles, out Vector3 fileAxis, out Vector3 rankAxis))
        {
            fileAxis = SafeNormalized(transform.right, Vector3.right);
            rankAxis = SafeNormalized(transform.forward, Vector3.forward);
        }

        Vector3 upAxis = SafeNormalized(board != null ? board.transform.up : transform.up, Vector3.up);
        fileAxis = Vector3.ProjectOnPlane(fileAxis, upAxis).normalized;
        rankAxis = Vector3.ProjectOnPlane(rankAxis, upAxis).normalized;
        if (fileAxis.sqrMagnitude < 0.9f || rankAxis.sqrMagnitude < 0.9f)
        {
            fileAxis = SafeNormalized(transform.right, Vector3.right);
            rankAxis = SafeNormalized(transform.forward, Vector3.forward);
        }

        metrics = new BoardMetrics(
            bounds.center,
            fileAxis,
            rankAxis,
            upAxis,
            bounds.center.y,
            bounds);
        return true;
    }

    bool TryResolveBoardAxes(ChessTile[] tiles, out Vector3 fileAxis, out Vector3 rankAxis)
    {
        fileAxis = Vector3.zero;
        rankAxis = Vector3.zero;

        if (TryGetNamedTilePosition(tiles, "A1", out Vector3 a1) &&
            TryGetNamedTilePosition(tiles, "H1", out Vector3 h1) &&
            TryGetNamedTilePosition(tiles, "A8", out Vector3 a8))
        {
            fileAxis = SafeNormalized(h1 - a1, Vector3.right);
            rankAxis = SafeNormalized(a8 - a1, Vector3.forward);
            return true;
        }

        float maxDistance = -1f;
        Vector3 origin = Vector3.zero;
        Vector3 furthest = Vector3.zero;
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == null) continue;
            for (int j = i + 1; j < tiles.Length; j++)
            {
                if (tiles[j] == null) continue;
                float dist = (tiles[j].transform.position - tiles[i].transform.position).sqrMagnitude;
                if (dist > maxDistance)
                {
                    maxDistance = dist;
                    origin = tiles[i].transform.position;
                    furthest = tiles[j].transform.position;
                }
            }
        }

        if (maxDistance <= 0f)
        {
            return false;
        }

        Vector3 diagonal = (furthest - origin).normalized;
        Vector3 fallbackRight = SafeNormalized(transform.right, Vector3.right);
        Vector3 fallbackForward = SafeNormalized(transform.forward, Vector3.forward);
        fileAxis = Mathf.Abs(Vector3.Dot(diagonal, fallbackRight)) >= Mathf.Abs(Vector3.Dot(diagonal, fallbackForward))
            ? fallbackRight
            : fallbackForward;
        rankAxis = Vector3.Cross(SafeNormalized(transform.up, Vector3.up), fileAxis).normalized;
        if (rankAxis == Vector3.zero)
        {
            rankAxis = fallbackForward;
        }
        return true;
    }

    static bool TryGetNamedTilePosition(ChessTile[] tiles, string tileName, out Vector3 position)
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            ChessTile tile = tiles[i];
            if (tile != null && tile.name == tileName)
            {
                position = tile.transform.position;
                return true;
            }
        }

        position = default;
        return false;
    }

    static Vector3 SafeNormalized(Vector3 value, Vector3 fallback)
    {
        return value.sqrMagnitude > 0.0001f ? value.normalized : fallback;
    }

    static bool TryBuildBoardBounds(ChessTile[] tiles, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < tiles.Length; i++)
        {
            ChessTile tile = tiles[i];
            if (tile == null)
            {
                continue;
            }

            if (TryGetTileBounds(tile, out Bounds tileBounds))
            {
                if (!hasBounds)
                {
                    bounds = tileBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(tileBounds);
                }

                continue;
            }

            if (!hasBounds)
            {
                bounds = new Bounds(tile.transform.position, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(tile.transform.position);
            }
        }

        return hasBounds;
    }

    static bool TryGetTileBounds(ChessTile tile, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        Renderer[] renderers = tile.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = tile.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (!hasBounds)
            {
                bounds = colliders[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
        }

        return hasBounds;
    }

    static int GetRowIndex(PieceType pieceType)
    {
        return pieceType switch
        {
            PieceType.Queen => 0,
            PieceType.Rook => 1,
            PieceType.Bishop => 2,
            PieceType.Knight => 3,
            PieceType.Pawn => 4,
            PieceType.King => 5,
            _ => 5
        };
    }

    void DisableBoardGameplayInteraction(ChessPiece piece)
    {
        InteractableChessPiece interactable = piece.GetComponent<InteractableChessPiece>();
        if (interactable != null)
        {
            interactable.enabled = false;
        }

        ChessPieceMotion motion = piece.GetComponent<ChessPieceMotion>();
        if (motion != null)
        {
            motion.enabled = false;
        }

        Collider[] colliders = piece.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            collider.enabled = false;
        }


        Rigidbody[] rigidbodies = piece.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }
    }

    static bool TryGetPieceBounds(ChessPiece piece, out Bounds bounds)
    {
        bounds = default;
        if (TryGetCombinedRendererBounds(piece.transform, out bounds))
        {
            return true;
        }

        if (TryGetCombinedColliderBounds(piece.transform, out bounds))
        {
            return true;
        }

        return false;
    }

    static float GetBoundsMinProjection(Bounds bounds, Vector3 axis)
    {
        Vector3 safeAxis = SafeNormalized(axis, Vector3.up);
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float minProjection = float.PositiveInfinity;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    float projection = Vector3.Dot(corner, safeAxis);

                    if (projection < minProjection)
                    {
                        minProjection = projection;
                    }
                }
            }
        }

        return minProjection;
    }

    static bool TryGetCapturedPieceBounds(Transform root, out Bounds bounds, out string boundsSource)
    {
        boundsSource = "none";

        if (TryGetCombinedRendererBounds(root, out bounds))
        {
            boundsSource = "renderer";
            return true;
        }

        if (TryGetCombinedColliderBounds(root, out bounds))
        {
            boundsSource = "collider";
            return true;
        }

        return false;
    }

    static bool TryGetCombinedRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    static bool TryGetCombinedColliderBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    readonly struct BoardMetrics
    {
        public readonly Vector3 Center;
        public readonly Vector3 FileAxis;
        public readonly Vector3 RankAxis;
        public readonly Vector3 Up;
        public readonly float SurfaceY;
        public readonly Bounds Bounds;
        public BoardMetrics(Vector3 center, Vector3 fileAxis, Vector3 rankAxis, Vector3 up, float surfaceY, Bounds bounds)
        {
            Center = center;
            FileAxis = fileAxis;
            RankAxis = rankAxis;
            Up = up;
            SurfaceY = surfaceY;
            Bounds = bounds;
        }

        public float GetHalfExtentAlong(Vector3 axis)
        {
            Vector3 extents = Bounds.extents;
            return Mathf.Abs(Vector3.Dot(axis, Vector3.right)) * extents.x
                + Mathf.Abs(Vector3.Dot(axis, Vector3.up)) * extents.y
                + Mathf.Abs(Vector3.Dot(axis, Vector3.forward)) * extents.z;
        }

        public bool IsInsideBoardExtents(Vector3 worldPosition)
        {
            Vector3 relative = worldPosition - Center;
            float fileHalf = GetHalfExtentAlong(FileAxis);
            float rankHalf = GetHalfExtentAlong(RankAxis);
            float fileDistance = Mathf.Abs(Vector3.Dot(relative, FileAxis));
            float rankDistance = Mathf.Abs(Vector3.Dot(relative, RankAxis));
            return fileDistance <= fileHalf && rankDistance <= rankHalf;
        }
    }

    readonly struct RowKey
    {
        public readonly PieceTeam Team;
        public readonly int Row;

        public RowKey(PieceTeam team, int row)
        {
            Team = team;
            Row = row;
        }
    }

    #endregion
}
