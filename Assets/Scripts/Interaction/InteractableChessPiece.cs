using UnityEngine;

public class InteractableChessPiece : MonoBehaviour
{
    public void OnInteract()
    {
        Debug.Log("Piece Selected: " + name);
        
        ChessCameraController.Instance.EnterTacticalView(transform);
    }
    
}
