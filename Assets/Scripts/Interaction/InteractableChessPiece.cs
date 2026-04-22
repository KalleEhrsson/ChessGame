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

        ChessGameStateController gameStateController = ChessGameStateController.GetOrCreate();
        if (gameStateController != null && !gameStateController.IsGameplayActive())
        {
            return;
        }

        ChessSelectionController selectionController = ChessSelectionController.GetOrCreate();
        ChessCameraController cameraController = ChessCameraController.GetOrCreate();
        ChessMoveValidator moveValidator = ChessMoveValidator.GetOrCreate();
        ChessTileHighlighter tileHighlighter = ChessTileHighlighter.GetOrCreate();
        ChessUIAudio uiAudio = ChessUIAudio.GetOrCreate();

        if (!selectionController.CanSelectPiece(piece))
        {
            uiAudio?.PlayInvalid();
            return;
        }

        selectionController.SelectPiece(piece);
        uiAudio?.PlaySelectionClick();
        cameraController.EnterTacticalView(piece);

        moveValidator.GenerateLegalMoves(piece, out var moveTiles, out var captureTiles);
        selectionController.SetMoveOptions(moveTiles, captureTiles);
        tileHighlighter.Highlight(selectionController.MoveTiles, selectionController.CaptureTiles);
    }
}
