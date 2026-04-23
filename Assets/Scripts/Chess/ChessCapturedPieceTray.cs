using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChessCapturedPieceTray : MonoBehaviour
{
    const string WhiteCapturedAreaName = "WhiteCapturedArea";
    const string BlackCapturedAreaName = "BlackCapturedArea";

    #region Variables

    [SerializeField] ChessBoard board;
    [SerializeField] Transform whiteCapturedArea;
    [SerializeField] Transform blackCapturedArea;
    [SerializeField, Min(0.05f)] float horizontalSpacing = 0.22f;
    [SerializeField, Min(0.05f)] float rowSpacing = 0.2f;
    [SerializeField] float sideOffset = 1.4f;
    [SerializeField] float forwardOffset = 0.1f;
    [SerializeField] float verticalOffset = -0.02f;
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
        DisableGameplayInteraction(piece);

        int row = GetRowIndex(piece.Type);
        RowKey key = new(piece.Team, row);
        rowCounts.TryGetValue(key, out int columnIndex);
        rowCounts[key] = columnIndex + 1;

        Vector3 targetPosition = BuildPosition(trayArea, row, columnIndex);
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

        if (whiteCapturedArea == null)
        {
            whiteCapturedArea = FindOrCreateTrayTransform(WhiteCapturedAreaName, false);
        }

        if (blackCapturedArea == null)
        {
            blackCapturedArea = FindOrCreateTrayTransform(BlackCapturedAreaName, true);
        }
    }

    Transform FindOrCreateTrayTransform(string trayName, bool usePositiveSide)
    {
        Transform existing = transform.Find(trayName);
        if (existing != null)
        {
            return existing;
        }

        ChessTile[] tiles = board != null ? board.GetAllTiles() : null;
        Vector3 boardCenter = transform.position;
        if (tiles != null && tiles.Length > 0)
        {
            Bounds bounds = new Bounds(tiles[0].transform.position, Vector3.zero);
            for (int i = 1; i < tiles.Length; i++)
            {
                bounds.Encapsulate(tiles[i].transform.position);
            }

            boardCenter = bounds.center;
        }

        Vector3 sideDirection = usePositiveSide ? transform.right : -transform.right;
        Vector3 trayPosition = boardCenter + (sideDirection * sideOffset) + (transform.forward * forwardOffset);
        trayPosition.y += verticalOffset;

        GameObject trayObject = new(trayName);
        trayObject.transform.SetParent(transform, true);
        trayObject.transform.SetPositionAndRotation(trayPosition, transform.rotation);
        return trayObject.transform;
    }

    Vector3 BuildPosition(Transform trayArea, int row, int column)
    {
        Vector3 rowDirection = trayArea.forward;
        Vector3 basePosition = trayArea.position;
        return basePosition
            + (trayArea.right * (column * horizontalSpacing))
            + (rowDirection * (row * rowSpacing));
    }

    static int GetRowIndex(PieceType pieceType)
    {
        return pieceType switch
        {
            PieceType.Pawn => 0,
            PieceType.Rook => 1,
            PieceType.Knight => 2,
            PieceType.Bishop => 3,
            PieceType.Queen => 4,
            PieceType.King => 5,
            _ => 5
        };
    }

    static void DisableGameplayInteraction(ChessPiece piece)
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
            colliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = piece.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
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
