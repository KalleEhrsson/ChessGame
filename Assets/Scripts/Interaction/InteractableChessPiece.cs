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
        ChessMoveGenerator moveGenerator = ChessMoveGenerator.GetOrCreate();
        ChessTileHighlighter tileHighlighter = ChessTileHighlighter.GetOrCreate();

        selectionController.SelectPiece(piece);
        cameraController.EnterTacticalView(piece);

        moveGenerator.GenerateMoves(piece, out var moveTiles, out var captureTiles);
        selectionController.SetMoveOptions(moveTiles, captureTiles);
        tileHighlighter.Highlight(selectionController.MoveTiles, selectionController.CaptureTiles);
    }
}
