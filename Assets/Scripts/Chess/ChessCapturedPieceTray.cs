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
        Vector3 targetPosition = BuildPosition(trayArea, row, pieceCountInRow, columnStep);
        Quaternion targetRotation = trayArea.rotation * Quaternion.Euler(displayEulerOffset);

        piece.transform.SetParent(trayArea, true);
        piece.transform.SetPositionAndRotation(targetPosition, targetRotation);

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
        float sideDistance = (metrics.HalfBoardWidth + capturedTrayDistanceFromBoard + (capturedTrayColumnSpacing * 0.5f));

        Vector3 leftPosition = metrics.Center - (metrics.Right * sideDistance) + trayForwardOffset;
        leftPosition.y = metrics.SurfaceY + capturedTrayYOffset;

        Vector3 rightPosition = metrics.Center + (metrics.Right * sideDistance) + trayForwardOffset;
        rightPosition.y = metrics.SurfaceY + capturedTrayYOffset;

        Quaternion trayRotation = Quaternion.LookRotation(metrics.Forward, Vector3.up);
        whiteCapturedArea.SetPositionAndRotation(leftPosition, trayRotation);
        blackCapturedArea.SetPositionAndRotation(rightPosition, trayRotation);
    }

    Vector3 BuildPosition(Transform trayArea, int row, int column, float columnStep)
    {
        Vector3 basePosition = trayArea.position;
        return basePosition
            + (trayArea.forward * (row * capturedTrayRowSpacing))
            + (trayArea.right * (column * Mathf.Max(capturedTrayColumnSpacing, columnStep)));
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

        Bounds bounds = new Bounds(tiles[0].transform.position, Vector3.zero);
        for (int i = 1; i < tiles.Length; i++)
        {
            bounds.Encapsulate(tiles[i].transform.position);
        }

        float tileStep = EstimateTileStep(tiles);
        if (tileStep <= 0f)
        {
            return false;
        }

        metrics = new BoardMetrics(
            bounds.center,
            transform.right,
            transform.forward,
            bounds.center.y,
            tileStep * (BoardSquaresPerSide - 1) * 0.5f);
        return true;
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
        public readonly float SurfaceY;
        public readonly float HalfBoardWidth;

        public BoardMetrics(Vector3 center, Vector3 right, Vector3 forward, float surfaceY, float halfBoardWidth)
        {
            Center = center;
            Right = right;
            Forward = forward;
            SurfaceY = surfaceY;
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
