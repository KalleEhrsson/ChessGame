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
    InputAction rightClickAction;
    InputAction leftClickAction;

    ChessBoard board;
    ChessSelectionController selectionController;
    ChessCameraController cameraController;
    ChessMoveGenerator moveGenerator;
    ChessTileHighlighter tileHighlighter;
    ChessTileHoverController tileHoverController;

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
        rightClickAction.Enable();
        leftClickAction.Enable();

        interactAction.performed += OnInteractPerformed;
        cancelAction.performed += OnCancelPerformed;
        rightClickAction.performed += OnRightClickPerformed;
        leftClickAction.performed += OnLeftClickPerformed;
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

        if (rightClickAction != null)
        {
            rightClickAction.performed -= OnRightClickPerformed;
            rightClickAction.Disable();
        }

        if (leftClickAction != null)
        {
            leftClickAction.performed -= OnLeftClickPerformed;
            leftClickAction.Disable();
        }
    }

    #endregion

    #region Setup

    void EnsureSystems()
    {
        board = ChessBoard.Instance;
        if (board == null)
        {
            board = FindFirstObjectByType<ChessBoard>();
        }

        selectionController = ChessSelectionController.GetOrCreate();
        cameraController = ChessCameraController.GetOrCreate();
        moveGenerator = ChessMoveGenerator.GetOrCreate();
        tileHighlighter = ChessTileHighlighter.GetOrCreate();
        tileHoverController = ChessTileHoverController.GetOrCreate();
    }

    void EnsureInput()
    {
        interactAction ??= new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
        cancelAction ??= new InputAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
        rightClickAction ??= new InputAction("RightClick", InputActionType.Button, "<Mouse>/rightButton");
        leftClickAction ??= new InputAction("LeftClick", InputActionType.Button, "<Mouse>/leftButton");
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
        EnsureSystems();

        if (cameraController.IsInTacticalMode() && selectionController.HasSelection())
        {
            ResetSelectionFlow();
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
        TryCancelSelection();
    }

    void OnRightClickPerformed(InputAction.CallbackContext _)
    {
        TryCancelSelection();
    }

    void OnLeftClickPerformed(InputAction.CallbackContext _)
    {
        TryMoveFromTileClick();
    }

    void TryCancelSelection()
    {
        EnsureSystems();
        if (!selectionController.HasSelection() || !cameraController.IsInTacticalMode())
        {
            return;
        }

        ResetSelectionFlow();
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

        moveGenerator.GenerateMoves(piece, out var moveTiles, out var captureTiles);
        selectionController.SetMoveOptions(moveTiles, captureTiles);
        tileHighlighter.Highlight(selectionController.MoveTiles, selectionController.CaptureTiles);
    }

    void TryMoveFromTileClick()
    {
        EnsureSystems();
        if (!cameraController.IsInTacticalMode() || !selectionController.HasSelection())
        {
            return;
        }

        ChessTile targetTile = tileHoverController.CurrentHoveredTile;
        if (targetTile == null || !selectionController.IsValidDestination(targetTile))
        {
            return;
        }

        ChessPiece selectedPiece = selectionController.GetSelected();
        if (selectedPiece == null || selectedPiece.CurrentTile == null)
        {
            ResetSelectionFlow();
            return;
        }

        if (board != null)
        {
            board.MovePiece(selectedPiece.CurrentTile, targetTile);
        }

        ResetSelectionFlow();
    }

    void ResetSelectionFlow()
    {
        tileHighlighter.ClearAllHighlights();
        tileHoverController.ClearHover();
        selectionController.Deselect();
        cameraController.ExitTacticalView();
    }

    #endregion
}
