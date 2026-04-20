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
    [SerializeField] bool enableDebugLogs = true;
    [SerializeField] float maxRayDistance = 1000f;

    Camera activeCamera;
    ChessBoard board;
    ChessCameraController cameraController;
    ChessTile currentHoveredTile;
    int lastColliderValidationFrame = -1;

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
            DebugLog("Hover update skipped - tactical mode inactive.");
            ClearHover();
            return;
        }

        EnsureBoardTileColliders();

        ChessTile hovered = GetTileUnderCursor();
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

    public ChessTile GetTileUnderCursor()
    {
        return RaycastTileUnderCursorInternal(logResult: true);
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
            activeCamera = ResolveActiveCamera();
        }
    }

    Camera ResolveActiveCamera()
    {
        if (cameraController != null)
        {
            Camera tacticalCamera = cameraController.GetComponent<Camera>();
            if (tacticalCamera != null)
            {
                return tacticalCamera;
            }
        }

        if (Camera.main != null)
        {
            return Camera.main;
        }

        return FindFirstObjectByType<Camera>();
    }

    void EnsureBoardTileColliders()
    {
        if (Time.frameCount == lastColliderValidationFrame || board == null)
        {
            return;
        }

        ChessTile[] tiles = board.GetAllTiles();
        for (int i = 0; i < tiles.Length; i++)
        {
            ChessTile tile = tiles[i];
            if (tile == null)
            {
                continue;
            }

            tile.EnsureInteractionCollider();
        }

        lastColliderValidationFrame = Time.frameCount;
    }

    ChessTile RaycastTileUnderCursorInternal(bool logResult)
    {
        activeCamera = ResolveActiveCamera();
        if (activeCamera == null)
        {
            if (logResult)
            {
                DebugLog("No active camera found for tactical raycast.");
            }

            return null;
        }

        Vector2 cursorPosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Input.mousePosition;
        Ray ray = activeCamera.ScreenPointToRay(cursorPosition);
        int layerMask = raycastMask.value == 0 ? Physics.DefaultRaycastLayers : raycastMask.value;
        Debug.DrawRay(ray.origin, ray.direction * Mathf.Min(maxRayDistance, 50f), Color.cyan, 0f, false);

        if (logResult)
        {
            DebugLog($"Tactical mode active. Mouse={cursorPosition}. Camera={activeCamera.name}. LayerMask={layerMask}.");
        }

        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, layerMask, QueryTriggerInteraction.Ignore);
        if (!hasHit && layerMask != Physics.DefaultRaycastLayers)
        {
            hasHit = Physics.Raycast(ray, out hit, maxRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hasHit && logResult)
            {
                DebugLog("Layer masked tactical raycast missed; default layer fallback hit a collider.");
            }
        }

        if (!hasHit)
        {
            if (logResult)
            {
                DebugLog("Tactical raycast fired but no collider was hit.");
            }

            return null;
        }

        if (logResult)
        {
            DebugLog($"Tactical raycast hit collider={hit.collider.name} ({hit.collider.GetType().Name}).");
        }

        ChessTile tile;
        if (board != null)
        {
            tile = board.GetTileFromRaycast(hit);
        }
        else
        {
            tile = hit.collider != null ? hit.collider.GetComponentInParent<ChessTile>() : null;
        }

        if (logResult)
        {
            DebugLog(tile != null
                ? $"ChessTile found from raycast: {tile.TileName}."
                : "Raycast hit did not map to a ChessTile.");
        }

        return tile;
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

    void DebugLog(string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }

        Debug.Log($"[ChessTileHoverController] {message}");
    }

    #endregion
}
