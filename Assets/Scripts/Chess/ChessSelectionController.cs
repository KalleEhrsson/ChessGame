using UnityEngine;

[DisallowMultipleComponent]
public class ChessSelectionController : MonoBehaviour
{
    #region Singleton

    public static ChessSelectionController Instance { get; private set; }

    public static ChessSelectionController GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessSelectionController existing = FindFirstObjectByType<ChessSelectionController>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject controllerObject = new("ChessSelectionController");
        Instance = controllerObject.AddComponent<ChessSelectionController>();
        return Instance;
    }

    #endregion

    #region Variables

    ChessPiece selectedPiece;

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

    #region Selection

    public void SelectPiece(ChessPiece piece)
    {
        if (piece == null)
        {
            return;
        }

        if (selectedPiece == piece)
        {
            return;
        }

        if (selectedPiece != null)
        {
            selectedPiece.SetSelected(false);
        }

        selectedPiece = piece;
        selectedPiece.SetSelected(true);
    }

    public void Deselect()
    {
        if (selectedPiece != null)
        {
            selectedPiece.SetSelected(false);
        }

        selectedPiece = null;
    }

    public bool HasSelection()
    {
        return selectedPiece != null;
    }

    public ChessPiece GetSelected()
    {
        return selectedPiece;
    }

    #endregion
}
