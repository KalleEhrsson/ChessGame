using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChessCapturedPieceTray : MonoBehaviour
{
    const int BoardSquaresPerSide = 8;
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
    [SerializeField, Min(0.05f)] float capturedTrayRowSpacing = 0.22f;
    [SerializeField, Min(0.05f)] float capturedTrayColumnSpacing = 0.22f;
    [SerializeField, Min(0.05f)] float capturedTrayPiecePadding = 0.02f;
    [SerializeField] float capturedTrayYOffset = -0.02f;
    [SerializeField, Min(0f)] float capturedTraySidePadding = 0.1f;
    [SerializeField] Vector3 displayEulerOffset = Vector3.zero;
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
        DisableBoardGameplayInteraction(piece);

        int row = Mathf.Clamp(GetRowIndex(piece.Type), 0, 5);
        RowKey key = new(piece.Team, row);
        rowCounts.TryGetValue(key, out int pieceCountInRow);
        rowCounts[key] = pieceCountInRow + 1;

        float columnStep = ResolvePieceColumnStep(piece);
        Quaternion targetLocalRotation = Quaternion.Euler(displayEulerOffset);
        Vector3 targetLocalPosition = BuildLocalPosition(row, pieceCountInRow, columnStep);

        piece.transform.SetParent(trayArea, false);
        piece.transform.SetLocalPositionAndRotation(targetLocalPosition, targetLocalRotation);

        placedPieces.Add(pieceId);
        return true;
    }

    #endregion

    #region Helpers

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

        Vector3 trayForwardOffset = metrics.Forward * capturedTraySidePadding;
        float sideDistance = metrics.HalfBoardWidth + capturedTrayDistanceFromBoard + capturedTraySidePadding;

        Vector3 leftPosition = metrics.Center - (metrics.Right * sideDistance) + trayForwardOffset;
        leftPosition.y = metrics.SurfaceY + capturedTrayYOffset;

        Vector3 rightPosition = metrics.Center + (metrics.Right * sideDistance) + trayForwardOffset;
        rightPosition.y = metrics.SurfaceY + capturedTrayYOffset;

        Quaternion trayRotation = Quaternion.LookRotation(metrics.Forward, metrics.Up);
        whiteCapturedArea.SetPositionAndRotation(leftPosition, trayRotation);
        blackCapturedArea.SetPositionAndRotation(rightPosition, trayRotation);

        if (enableCapturedTrayDebugLogs)
        {
            Debug.Log($"[ChessCapturedPieceTray] Board bounds center={metrics.Bounds.center}, size={metrics.Bounds.size}, whiteWorld={whiteCapturedArea.position}, blackWorld={blackCapturedArea.position}, whiteLocal={whiteCapturedArea.localPosition}, blackLocal={blackCapturedArea.localPosition}");
        }

        if (metrics.Bounds.Contains(whiteCapturedArea.position) || metrics.Bounds.Contains(blackCapturedArea.position))
        {
            Debug.LogWarning("[ChessCapturedPieceTray] One or more captured piece anchors is inside the board bounds.");
        }

        ReflowPlacedPieces();
    }

    Vector3 BuildLocalPosition(int row, int column, float columnStep)
    {
        return (Vector3.forward * (row * capturedTrayRowSpacing))
            + (Vector3.right * (column * Mathf.Max(capturedTrayColumnSpacing, columnStep)));
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
            child.localPosition = BuildLocalPosition(row, column, columnStep);
            child.localRotation = Quaternion.Euler(displayEulerOffset);
            columnsByRow[row] = column + 1;
        }
    }

    float ResolvePieceColumnStep(ChessPiece piece)
    {
        if (piece != null && TryGetPieceBounds(piece, out Bounds bounds))
        {
            float width = Mathf.Max(bounds.size.x, bounds.size.z);
            if (width > 0.001f && float.IsFinite(width))
            {
                return width + capturedTrayPiecePadding;
            }
        }

        return capturedTrayColumnSpacing;
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

        float tileStep = EstimateTileStep(tiles);
        if (tileStep <= 0f)
        {
            tileStep = Mathf.Max(bounds.size.x, bounds.size.z) / Mathf.Max(1, BoardSquaresPerSide - 1);
        }

        metrics = new BoardMetrics(
            bounds.center,
            transform.right,
            transform.forward,
            transform.up,
            bounds.center.y,
            bounds,
            tileStep * (BoardSquaresPerSide - 1) * 0.5f);
        return true;
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

    static float EstimateTileStep(ChessTile[] tiles)
    {
        float minSqrDistance = float.MaxValue;

        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == null)
            {
                continue;
            }

            Vector3 tilePosition = tiles[i].transform.position;
            for (int j = i + 1; j < tiles.Length; j++)
            {
                if (tiles[j] == null)
                {
                    continue;
                }

                float sqrDistance = (tiles[j].transform.position - tilePosition).sqrMagnitude;
                if (sqrDistance > 0.0001f && sqrDistance < minSqrDistance)
                {
                    minSqrDistance = sqrDistance;
                }
            }
        }

        if (minSqrDistance == float.MaxValue)
        {
            return 0f;
        }

        return Mathf.Sqrt(minSqrDistance);
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
        bool hasBounds = false;

        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
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

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = piece.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
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
        public readonly Vector3 Right;
        public readonly Vector3 Forward;
        public readonly Vector3 Up;
        public readonly float SurfaceY;
        public readonly Bounds Bounds;
        public readonly float HalfBoardWidth;

        public BoardMetrics(Vector3 center, Vector3 right, Vector3 forward, Vector3 up, float surfaceY, Bounds bounds, float halfBoardWidth)
        {
            Center = center;
            Right = right;
            Forward = forward;
            Up = up;
            SurfaceY = surfaceY;
            Bounds = bounds;
            HalfBoardWidth = halfBoardWidth;
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
