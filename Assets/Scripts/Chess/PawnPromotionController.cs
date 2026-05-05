using UnityEngine;

[DisallowMultipleComponent]
public class PawnPromotionController : MonoBehaviour
{
    static readonly PieceType[] AllowedPromotionTypes = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

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

    public readonly struct PendingPromotionMove
    {
        public ChessTile FromTile { get; }
        public ChessTile ToTile { get; }
        public ChessPiece Pawn { get; }
        public PieceTeam Team { get; }

        public PendingPromotionMove(ChessTile fromTile, ChessTile toTile, ChessPiece pawn, PieceTeam team)
        {
            FromTile = fromTile;
            ToTile = toTile;
            Pawn = pawn;
            Team = team;
        }
    }

    PromotionSelectionUI selectionUi;
    bool isPromotionPending;
    PendingPromotionMove pendingMove;
    bool previousCursorVisible;
    CursorLockMode previousCursorLockState;
    bool hasStoredCursorState;

    public bool IsPromotionPending => isPromotionPending;
    public static bool IsPromotionSelectionActive => Instance != null && Instance.isPromotionPending;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

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

        ChessTile fromTile = piece.CurrentTile;
        pendingMove = new PendingPromotionMove(fromTile, destination, piece, piece.Team);
        isPromotionPending = true;

        ChessBoard board = ChessBoard.Instance != null ? ChessBoard.Instance : FindFirstObjectByType<ChessBoard>();
        if (board == null || !board.MovePiece(fromTile, destination, null, true))
        {
            CancelPendingPromotion("failed to begin pending promotion move");
            return false;
        }

        ChessPauseManager.GetOrCreate().NotifyRoundActionStarted();
        return true;
    }

    public void NotifyPromotionReadyAfterMove(ChessPiece pawn, ChessTile fromTile, ChessTile toTile)
    {
        if (!isPromotionPending || pendingMove.Pawn != pawn || pawn == null || pawn.CurrentTile != toTile)
        {
            CancelPendingPromotion("promotion landing state is invalid");
            return;
        }

        selectionUi = PromotionSelectionUI.GetOrCreate();
        if (selectionUi == null)
        {
            CancelPendingPromotion("promotion popup UI could not be created");
            return;
        }

        ApplyPromotionCursorState();
        selectionUi.Show(OnPromotionSelected);
    }

    public void ClearPendingState()
    {
        isPromotionPending = false;
        pendingMove = default;
        ChessPauseManager.GetOrCreate().NotifyRoundActionFinished();
        RestoreCursorStateAfterPromotion();
        selectionUi?.Hide();
    }

    void ApplyPromotionCursorState()
    {
        previousCursorVisible = Cursor.visible;
        previousCursorLockState = Cursor.lockState;
        hasStoredCursorState = true;

        ChessCursorStateCoordinator.SetTacticalCursorOverride(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void RestoreCursorStateAfterPromotion()
    {
        if (!hasStoredCursorState)
        {
            ChessCursorStateCoordinator.SetTacticalCursorOverride(false);
            return;
        }

        bool shouldKeepUnlocked = ChessPauseManager.GetOrCreate().ShouldUnlockCursor;
        ChessCursorStateCoordinator.SetTacticalCursorOverride(shouldKeepUnlocked);

        if (!shouldKeepUnlocked)
        {
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockState;
            ChessCursorStateCoordinator.SetTacticalCursorOverride(false);
        }

        hasStoredCursorState = false;
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
                selectionUi?.Show(OnPromotionSelected);
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
            selectionUi?.Show(OnPromotionSelected);
            return;
        }

        CancelPendingPromotion("finalization failed and pending references are no longer valid");
    }

    static bool IsValidPromotionType(PieceType promotionType)
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
        return board != null && board.FinalizePendingHumanPromotion(pendingMove.Pawn, pendingMove.FromTile, pendingMove.ToTile, promotionType);
    }

    bool CanRetryPendingPromotion()
    {
        if (!isPromotionPending || pendingMove.FromTile == null || pendingMove.ToTile == null)
        {
            return false;
        }

        ChessPiece pawn = pendingMove.Pawn;
        return pawn != null && pawn.Type == PieceType.Pawn && pawn.Team == pendingMove.Team && pawn.CurrentTile == pendingMove.ToTile;
    }

    void CancelPendingPromotion(string reason)
    {
        Debug.LogWarning($"[PawnPromotionController] Promotion cancelled: {reason}.");
        ClearPendingState();
    }
}
