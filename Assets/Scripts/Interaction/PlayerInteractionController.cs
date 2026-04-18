using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerInteractionController : MonoBehaviour
{
    #region Variables

    [SerializeField] float interactDistance = 4f;
    [SerializeField] LayerMask interactLayer = Physics.DefaultRaycastLayers;

    Camera playerCamera;
    InputAction interactAction;
    InputAction cancelAction;

    ChessSelectionController selectionController;
    ChessCameraController cameraController;

    #endregion

    #region Unity

    void Awake()
    {
        EnsureSystems();
        EnsureInput();
        ResolveCamera();
    }

    void OnEnable()
    {
        EnsureSystems();
        EnsureInput();

        interactAction.Enable();
        cancelAction.Enable();

        interactAction.performed += OnInteractPerformed;
        cancelAction.performed += OnCancelPerformed;
    }

    void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPerformed;
            interactAction.Disable();
        }

        if (cancelAction != null)
        {
            cancelAction.performed -= OnCancelPerformed;
            cancelAction.Disable();
        }
    }

    #endregion

    #region Setup

    void EnsureSystems()
    {
        selectionController = ChessSelectionController.GetOrCreate();
        cameraController = ChessCameraController.GetOrCreate();
    }

    void EnsureInput()
    {
        if (interactAction == null)
        {
            interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
        }

        if (cancelAction == null)
        {
            cancelAction = new InputAction("Cancel", InputActionType.Button, "<Mouse>/rightButton");
        }
    }

    void ResolveCamera()
    {
        if (playerCamera != null)
        {
            return;
        }

        playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera != null)
        {
            return;
        }

        playerCamera = Camera.main;
        if (playerCamera != null)
        {
            return;
        }

        playerCamera = FindFirstObjectByType<Camera>();
    }

    #endregion

    #region Interaction

    void OnInteractPerformed(InputAction.CallbackContext _)
    {
        if (selectionController == null || cameraController == null)
        {
            EnsureSystems();
        }

        if (cameraController.IsInTacticalMode() && selectionController.HasSelection())
        {
            DeselectAndReturn();
            return;
        }

        if (!cameraController.IsInFirstPerson())
        {
            return;
        }

        TrySelectFromRaycast();
    }

    void OnCancelPerformed(InputAction.CallbackContext _)
    {
        if (selectionController == null || cameraController == null)
        {
            EnsureSystems();
        }

        if (!cameraController.IsInTacticalMode() || !selectionController.HasSelection())
        {
            return;
        }

        DeselectAndReturn();
    }

    void TrySelectFromRaycast()
    {
        ResolveCamera();
        if (playerCamera == null)
        {
            return;
        }

        Ray ray = new(playerCamera.transform.position, playerCamera.transform.forward);
        int layerMask = interactLayer.value == 0 ? Physics.DefaultRaycastLayers : interactLayer.value;
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, layerMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        ChessPiece piece = hit.collider.GetComponentInParent<ChessPiece>();
        if (piece == null)
        {
            return;
        }

        selectionController.SelectPiece(piece);
        cameraController.EnterTacticalView(piece);
    }

    void DeselectAndReturn()
    {
        selectionController.Deselect();
        cameraController.ExitTacticalView();
    }

    #endregion
}
