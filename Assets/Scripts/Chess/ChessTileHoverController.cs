using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ChessTileHoverController : MonoBehaviour
{
    #region Singleton

    public static ChessTileHoverController Instance { get; private set; }

    public static ChessTileHoverController GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessTileHoverController existing = FindFirstObjectByType<ChessTileHoverController>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject host = new("ChessTileHoverController");
        Instance = host.AddComponent<ChessTileHoverController>();
        return Instance;
    }

    #endregion

    #region Variables

    [SerializeField] Color hoverColor = new(0.2f, 0.95f, 1f, 1f);
    [SerializeField] LayerMask raycastMask = Physics.DefaultRaycastLayers;

    Camera activeCamera;
    ChessBoard board;
    ChessCameraController cameraController;
    ChessTile currentHoveredTile;

    #endregion

    #region Properties

    public ChessTile CurrentHoveredTile => currentHoveredTile;

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
        ResolveDependencies();
    }

    void Update()
    {
        ResolveDependencies();
        if (cameraController == null || !cameraController.IsInTacticalMode())
        {
            ClearHover();
            return;
        }

        ChessTile hovered = RaycastTileUnderCursor();
        if (hovered == currentHoveredTile)
        {
            return;
        }

        SetHoveredTile(hovered);
    }

    #endregion

    #region API

    public void ClearHover()
    {
        SetHoveredTile(null);
    }

    #endregion

    #region Internals

    void ResolveDependencies()
    {
        if (cameraController == null)
        {
            cameraController = ChessCameraController.GetOrCreate();
        }

        if (board == null)
        {
            board = ChessBoard.Instance;
            if (board == null)
            {
                board = FindFirstObjectByType<ChessBoard>();
            }
        }

        if (activeCamera == null)
        {
            activeCamera = Camera.main;
            if (activeCamera == null)
            {
                activeCamera = FindFirstObjectByType<Camera>();
            }
        }
    }

    ChessTile RaycastTileUnderCursor()
    {
        if (activeCamera == null)
        {
            return null;
        }

        Vector2 cursorPosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Input.mousePosition;
        Ray ray = activeCamera.ScreenPointToRay(cursorPosition);
        int layerMask = raycastMask.value == 0 ? Physics.DefaultRaycastLayers : raycastMask.value;

        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask, QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        if (board != null)
        {
            return board.GetTileFromRaycast(hit);
        }

        return hit.collider != null ? hit.collider.GetComponentInParent<ChessTile>() : null;
    }

    void SetHoveredTile(ChessTile tile)
    {
        if (currentHoveredTile != null)
        {
            currentHoveredTile.SetHoverState(false, hoverColor);
        }

        currentHoveredTile = tile;

        if (currentHoveredTile != null)
        {
            currentHoveredTile.SetHoverState(true, hoverColor);
        }
    }

    #endregion
}
