using UnityEngine;

public class PlayerInteractionController : MonoBehaviour
{
    #region Variables

    public float interactDistance = 3f;
    public LayerMask interactLayer;

    #endregion

    #region Unity

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }

    #endregion

    #region Interaction

    void TryInteract()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayer))
        {
            InteractableChessPiece piece = hit.collider.GetComponent<InteractableChessPiece>();

            if (piece != null)
            {
                piece.OnInteract();
            }
        }
    }

    #endregion
}