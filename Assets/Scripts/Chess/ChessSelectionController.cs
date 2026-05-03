using System.Collections.Generic;
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
    readonly List<ChessTile> moveTiles = new(32);
    readonly List<ChessTile> captureTiles = new(32);
    ChessTurnManager turnManager;
    ChessGameStateController gameStateController;

    #endregion

    #region Properties

    public IReadOnlyList<ChessTile> MoveTiles => moveTiles;
    public IReadOnlyList<ChessTile> CaptureTiles => captureTiles;

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
        if (!CanSelectPiece(piece))
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

    public bool CanSelectPiece(ChessPiece piece)
    {
        if (piece == null)
        {
            return false;
        }

        gameStateController ??= ChessGameStateController.GetOrCreate();
        if (gameStateController != null && !gameStateController.IsGameplayActive())
        {
            return false;
        }

        if (ChessPauseManager.GetOrCreate().IsPauseRequested)
        {
            return false;
        }

        turnManager ??= ChessTurnManager.GetOrCreate();
        if (turnManager == null)
        {
            return true;
        }

        if (!turnManager.IsHumanTurn())
        {
            return false;
        }

        return piece.Team == turnManager.GetCurrentTurn();
    }

    public void SetMoveOptions(List<ChessTile> moves, List<ChessTile> captures)
    {
        moveTiles.Clear();
        captureTiles.Clear();

        if (moves != null)
        {
            moveTiles.AddRange(moves);
        }

        if (captures != null)
        {
            captureTiles.AddRange(captures);
        }
    }

    public bool IsValidDestination(ChessTile tile)
    {
        if (tile == null)
        {
            return false;
        }

        return moveTiles.Contains(tile) || captureTiles.Contains(tile);
    }

    public void Deselect()
    {
        if (selectedPiece != null)
        {
            selectedPiece.SetSelected(false);
        }

        selectedPiece = null;
        moveTiles.Clear();
        captureTiles.Clear();
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
