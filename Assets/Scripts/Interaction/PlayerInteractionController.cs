using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerInteractionController : MonoBehaviour
{
    #region Variables

    [SerializeField] float interactDistance = 4f;
    [SerializeField] LayerMask interactLayer = Physics.DefaultRaycastLayers;
    [SerializeField] bool enableTacticalClickDebugLogs = true;

    Camera playerCamera;
    InputAction interactAction;
    InputAction cancelAction;
    InputAction rightClickAction;
    InputAction leftClickAction;

    ChessBoard board;
    ChessSelectionController selectionController;
    ChessCameraController cameraController;
    ChessMoveValidator moveValidator;
    ChessTileHighlighter tileHighlighter;
    ChessTileHoverController tileHoverController;
    ChessGameStateController gameStateController;
    ChessUIAudio uiAudio;

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
        moveValidator = ChessMoveValidator.GetOrCreate();
        tileHighlighter = ChessTileHighlighter.GetOrCreate();
        tileHoverController = ChessTileHoverController.GetOrCreate();
        ChessTurnManager.GetOrCreate();
        gameStateController = ChessGameStateController.GetOrCreate();
        uiAudio = ChessUIAudio.GetOrCreate();
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
        if (ChessPieceMotion.IsAnyAnimating)
        {
            return;
        }

        if (gameStateController != null && !gameStateController.IsGameplayActive())
        {
            return;
        }

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
        if (ChessPieceMotion.IsAnyAnimating)
        {
            return;
        }

        if (gameStateController != null && !gameStateController.IsGameplayActive())
        {
            return;
        }

        TryCancelSelection();
    }

    void OnRightClickPerformed(InputAction.CallbackContext _)
    {
        if (ChessPieceMotion.IsAnyAnimating)
        {
            return;
        }

        if (gameStateController != null && !gameStateController.IsGameplayActive())
        {
            return;
        }

        TryCancelSelection();
    }

    void OnLeftClickPerformed(InputAction.CallbackContext _)
    {
        if (ChessPieceMotion.IsAnyAnimating)
        {
            return;
        }

        if (gameStateController != null && !gameStateController.IsGameplayActive())
        {
            return;
        }

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
            uiAudio?.PlayInvalid();
            return;
        }

        ChessPiece piece = hit.collider.GetComponentInParent<ChessPiece>();
        if (piece == null)
        {
            uiAudio?.PlayInvalid();
            return;
        }

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

    void TryMoveFromTileClick()
    {
        EnsureSystems();
        if (!cameraController.IsInTacticalMode() || !selectionController.HasSelection())
        {
            DebugTacticalClick("Left click ignored: tactical mode inactive or no selected piece.");
            return;
        }

        ChessTile targetTile = tileHoverController.GetTileUnderCursor();
        if (targetTile == null)
        {
            DebugTacticalClick("Left click raycast found no ChessTile under cursor.");
            uiAudio?.PlayInvalid();
            return;
        }

        bool validDestination = selectionController.IsValidDestination(targetTile);
        DebugTacticalClick($"Tile click hit {targetTile.TileName}. Valid destination={validDestination}.");
        if (!validDestination)
        {
            uiAudio?.PlayInvalid();
            return;
        }

        tileHighlighter.TriggerValidTileFeedback(targetTile);
        uiAudio?.PlayTileTap();

        ChessPiece selectedPiece = selectionController.GetSelected();
        if (selectedPiece == null || selectedPiece.CurrentTile == null)
        {
            ResetSelectionFlow();
            return;
        }

        if (board != null)
        {
            bool moved = board.MovePiece(selectedPiece.CurrentTile, targetTile);
            if (!moved)
            {
                uiAudio?.PlayInvalid();
                return;
            }

            uiAudio?.PlayValidMoveClick();
        }

        ResetSelectionFlow();
    }

    void DebugTacticalClick(string message)
    {
        if (!enableTacticalClickDebugLogs)
        {
            return;
        }

        Debug.Log($"[PlayerInteractionController] {message}");
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
