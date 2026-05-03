using UnityEngine;

[DisallowMultipleComponent]
public class PawnPromotionController : MonoBehaviour
{
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

        EnsureUi();
        if (selectionUi == null)
        {
            isPromotionPending = false;
            ChessPauseManager.GetOrCreate().NotifyRoundActionFinished();
            return false;
        }

        selectionUi.Show(OnPromotionSelected);
        return true;
    }

    public void ClearPendingState()
    {
        isPromotionPending = false;
        pendingMove = default;
        ChessPauseManager.GetOrCreate().NotifyRoundActionFinished();
        if (selectionUi != null)
        {
            selectionUi.Hide();
        }
    }

    #endregion

    #region Helpers

    void EnsureUi()
    {
        if (selectionUi != null)
        {
            return;
        }

        selectionUi = FindFirstObjectByType<PromotionSelectionUI>();
        if (selectionUi != null)
        {
            return;
        }

        GameObject uiObject = new("PromotionSelectionUI");
        selectionUi = uiObject.AddComponent<PromotionSelectionUI>();
    }

    void OnPromotionSelected(PieceType promotionType)
    {
        if (!isPromotionPending)
        {
            return;
        }

        ChessBoard board = ChessBoard.Instance != null ? ChessBoard.Instance : FindFirstObjectByType<ChessBoard>();
        bool moved = board != null && board.MovePiece(pendingMove.FromTile, pendingMove.ToTile, promotionType);
        if (!moved)
        {
            Debug.LogWarning("[PawnPromotionController] Promotion move finalization failed.");
        }

        isPromotionPending = false;
        pendingMove = default;
        ChessPauseManager.GetOrCreate().NotifyRoundActionFinished();
    }

    #endregion
}
