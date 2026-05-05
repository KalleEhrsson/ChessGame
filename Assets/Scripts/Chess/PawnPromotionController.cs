using UnityEngine;

[DisallowMultipleComponent]
public class PawnPromotionController : MonoBehaviour
{
    static readonly PieceType[] AllowedPromotionTypes = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };
    #region Singleton

    public static PawnPromotionController Instance { get; private set; }

    public static PawnPromotionController GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        PawnPromotionController existing = FindFirstObjectByType<PawnPromotionController>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject host = new("PawnPromotionController");
        Instance = host.AddComponent<PawnPromotionController>();
        return Instance;
    }

    #endregion

    #region Types

    public readonly struct PendingPromotionMove
    {
        public ChessTile FromTile { get; }
        public ChessTile ToTile { get; }
        public PieceTeam Team { get; }

        public PendingPromotionMove(ChessTile fromTile, ChessTile toTile, PieceTeam team)
        {
            FromTile = fromTile;
            ToTile = toTile;
            Team = team;
        }
    }

    #endregion

    #region Variables

    PromotionSelectionUI selectionUi;
    PromotionSelection3DController selection3d;
    bool isPromotionPending;
    PendingPromotionMove pendingMove;

    #endregion

    #region Properties

    public bool IsPromotionPending => isPromotionPending;

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
    }

    #endregion

    #region API

    public bool TryBeginPlayerPromotion(ChessPiece piece, ChessTile destination)
    {
        if (piece == null || destination == null || piece.CurrentTile == null)
        {
            return false;
        }

        if (isPromotionPending || piece.Type != PieceType.Pawn)
        {
            return false;
        }

        int promotionRank = piece.Team == PieceTeam.White ? 7 : 0;
        if (destination.Y != promotionRank)
        {
            return false;
        }

        pendingMove = new PendingPromotionMove(piece.CurrentTile, destination, piece.Team);
        isPromotionPending = true;
        ChessPauseManager.GetOrCreate().NotifyRoundActionStarted();

        EnsureSelectionControllers();
        if (selection3d == null)
        {
            isPromotionPending = false;
            ChessPauseManager.GetOrCreate().NotifyRoundActionFinished();
            return false;
        }

        selectionUi?.Hide();
        ChessCursorStateCoordinator.SetTacticalCursorOverride(true);
        selection3d.Show(piece.Team, OnPromotionSelected);
        if (selection3d.IsSelecting)
        {
            return true;
        }

        isPromotionPending = false;
        pendingMove = default;
        ChessPauseManager.GetOrCreate().NotifyRoundActionFinished();
        ChessCursorStateCoordinator.SetTacticalCursorOverride(false);
        return false;
    }

    public void ClearPendingState()
    {
        isPromotionPending = false;
        pendingMove = default;
        ChessPauseManager.GetOrCreate().NotifyRoundActionFinished();
        ChessCursorStateCoordinator.SetTacticalCursorOverride(false);
        if (selectionUi != null)
        {
            selectionUi.Hide();
        }

        selection3d?.Hide();
    }

    #endregion

    #region Helpers

    void EnsureSelectionControllers()
    {
        if (selection3d != null)
        {
            return;
        }

        selection3d = FindFirstObjectByType<PromotionSelection3DController>();
        if (selection3d == null)
        {
            GameObject selectorObject = new("PromotionSelection3DController");
            selection3d = selectorObject.AddComponent<PromotionSelection3DController>();
        }

        selectionUi = FindFirstObjectByType<PromotionSelectionUI>();
    }

    void OnPromotionSelected(PieceType promotionType)
    {
        if (!isPromotionPending)
        {
            return;
        }

        if (!IsValidPromotionType(promotionType))
        {
            Debug.LogWarning($"[PawnPromotionController] Rejected invalid promotion type '{promotionType}' while promotion is pending.");
            if (CanRetryPendingPromotion())
            {
                TryShowPromotionSelector();
                return;
            }

            CancelPendingPromotion("promotion type was invalid and pending references are no longer safe to retry");
            return;
        }

        if (TryFinalizePendingPromotion(promotionType))
        {
            ClearPendingState();
            return;
        }

        string pawnName = pendingMove.FromTile != null && pendingMove.FromTile.CurrentPiece != null
            ? pendingMove.FromTile.CurrentPiece.name
            : "<missing pawn>";
        string fromName = pendingMove.FromTile != null ? pendingMove.FromTile.TileName : "<null>";
        string toName = pendingMove.ToTile != null ? pendingMove.ToTile.TileName : "<null>";
        Debug.LogWarning($"[PawnPromotionController] Promotion move finalization failed. Pawn={pawnName}, From={fromName}, To={toName}, Type={promotionType}");

        if (CanRetryPendingPromotion())
        {
            TryShowPromotionSelector();
            return;
        }

        CancelPendingPromotion("finalization failed and pending references are no longer valid");
    }

    bool IsValidPromotionType(PieceType promotionType)
    {
        for (int i = 0; i < AllowedPromotionTypes.Length; i++)
        {
            if (AllowedPromotionTypes[i] == promotionType)
            {
                return true;
            }
        }

        return false;
    }

    bool TryFinalizePendingPromotion(PieceType promotionType)
    {
        ChessBoard board = ChessBoard.Instance != null ? ChessBoard.Instance : FindFirstObjectByType<ChessBoard>();
        return board != null && board.MovePiece(pendingMove.FromTile, pendingMove.ToTile, promotionType);
    }

    bool CanRetryPendingPromotion()
    {
        if (!isPromotionPending || pendingMove.FromTile == null || pendingMove.ToTile == null)
        {
            return false;
        }

        ChessPiece pawn = pendingMove.FromTile.CurrentPiece;
        return pawn != null && pawn.Type == PieceType.Pawn && pawn.Team == pendingMove.Team && selection3d != null;
    }

    void TryShowPromotionSelector()
    {
        EnsureSelectionControllers();
        if (selection3d == null)
        {
            CancelPendingPromotion("promotion selector is unavailable for retry");
            return;
        }

        selectionUi?.Hide();
        ChessCursorStateCoordinator.SetTacticalCursorOverride(true);
        selection3d.Show(pendingMove.Team, OnPromotionSelected);
        if (selection3d.IsSelecting)
        {
            return;
        }

        CancelPendingPromotion("promotion selector could not be displayed for retry");
    }

    void CancelPendingPromotion(string reason)
    {
        Debug.LogWarning($"[PawnPromotionController] Promotion cancelled: {reason}.");
        ClearPendingState();
    }


    #endregion
}
