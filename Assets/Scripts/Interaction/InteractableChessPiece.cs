using UnityEngine;

[DisallowMultipleComponent]
public class InteractableChessPiece : MonoBehaviour
{
    public void OnInteract()
    {
        ChessPiece piece = GetComponent<ChessPiece>();
        if (piece == null)
        {
            return;
        }

        ChessSelectionController selectionController = ChessSelectionController.GetOrCreate();
        ChessCameraController cameraController = ChessCameraController.GetOrCreate();
        ChessMoveValidator moveValidator = ChessMoveValidator.GetOrCreate();
        ChessTileHighlighter tileHighlighter = ChessTileHighlighter.GetOrCreate();

        if (!selectionController.CanSelectPiece(piece))
        {
            return;
        }

        selectionController.SelectPiece(piece);
        cameraController.EnterTacticalView(piece);

        moveValidator.GenerateLegalMoves(piece, out var moveTiles, out var captureTiles);
        selectionController.SetMoveOptions(moveTiles, captureTiles);
        tileHighlighter.Highlight(selectionController.MoveTiles, selectionController.CaptureTiles);
    }
}
