using UnityEngine;
using UnityEngine.InputSystem;
using System;

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

        GameObject host = ResolveHostGameObject();
        Instance = host.GetComponent<ChessTileHoverController>();
        if (Instance == null)
        {
            Instance = host.AddComponent<ChessTileHoverController>();
        }

        return Instance;
    }

    static GameObject ResolveHostGameObject()
    {
        ChessBoard existingBoard = ChessBoard.Instance;
        if (existingBoard == null)
        {
            existingBoard = FindFirstObjectByType<ChessBoard>();
        }

        if (existingBoard != null)
        {
            return existingBoard.gameObject;
        }

        PlayerInteractionController interactionController = FindFirstObjectByType<PlayerInteractionController>();
        if (interactionController != null)
        {
            return interactionController.gameObject;
        }

        return new GameObject("ChessTileHoverController");
    }

    #endregion

    #region Variables

    [SerializeField] Color hoverColor = new(0.2f, 0.95f, 1f, 1f);
    [SerializeField] LayerMask tileOnlyMask;
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

        RaycastHit[] hits = Array.Empty<RaycastHit>();
        if (tileOnlyMask.value != 0)
        {
            hits = Physics.RaycastAll(ray, maxRayDistance, tileOnlyMask.value, QueryTriggerInteraction.Ignore);
            if (hits.Length > 0 && logResult)
            {
                DebugLog($"Tile-only mask hit {hits.Length} collider(s). Ignoring pieces for tactical hover/click.");
            }
        }

        if (hits.Length == 0)
        {
            hits = Physics.RaycastAll(ray, maxRayDistance, layerMask, QueryTriggerInteraction.Ignore);
        }

        if ((hits == null || hits.Length == 0) && layerMask != Physics.DefaultRaycastLayers)
        {
            hits = Physics.RaycastAll(ray, maxRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0 && logResult)
            {
                DebugLog("Layer masked tactical raycast missed; default layer fallback hit a collider.");
            }
        }

        if (hits == null || hits.Length == 0)
        {
            if (logResult)
            {
                DebugLog("Tactical raycast fired but no collider was hit.");
            }

            return null;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
            {
                continue;
            }

            ChessTile tile = TryMapHitToTile(hit, out bool mappedFromPiece);
            if (logResult)
            {
                string mappedFrom = mappedFromPiece ? "piece" : "tile";
                DebugLog(tile != null
                    ? $"Raycast hit collider={hit.collider.name}, mapped via {mappedFrom} to tile={tile.TileName}."
                    : $"Raycast hit collider={hit.collider.name} but it did not map to a ChessTile.");
            }

            if (tile != null)
            {
                return tile;
            }
        }

        if (logResult)
        {
            DebugLog("Raycast hits found, but none mapped to a ChessTile.");
        }

        return null;
    }

    ChessTile TryMapHitToTile(RaycastHit hit, out bool mappedFromPiece)
    {
        mappedFromPiece = false;

        ChessTile tile = board != null
            ? board.GetTileFromRaycast(hit)
            : hit.collider != null ? hit.collider.GetComponentInParent<ChessTile>() : null;

        if (tile != null)
        {
            return tile;
        }

        ChessPiece piece = hit.collider != null ? hit.collider.GetComponentInParent<ChessPiece>() : null;
        if (piece == null || piece.CurrentTile == null)
        {
            return null;
        }

        mappedFromPiece = true;
        return piece.CurrentTile;
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
