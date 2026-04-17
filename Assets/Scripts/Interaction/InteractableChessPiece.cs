using UnityEngine;

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

        selectionController.SelectPiece(piece);
        cameraController.EnterTacticalView(piece);
    }
}
