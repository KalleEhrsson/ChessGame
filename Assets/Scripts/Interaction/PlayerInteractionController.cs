using UnityEngine;

public class PlayerInteractionController : MonoBehaviour
{
    #region Variables

    public float interactDistance = 3f;
    public LayerMask interactLayer;

    ChessBoard chessBoard;
    ChessTile currentHighlightedTile;

    #endregion

    #region Unity

    void Awake()
    {
        ResolveBoard();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }

    #endregion

    #region Interaction

    void ResolveBoard()
    {
        chessBoard = ChessBoard.Instance;
        if (chessBoard == null)
        {
            chessBoard = FindFirstObjectByType<ChessBoard>();
        }
    }

    void TryInteract()
    {
        if (chessBoard == null)
        {
            ResolveBoard();
        }

        Camera cameraRef = Camera.main;
        if (cameraRef == null)
        {
            return;
        }

        Ray ray = new Ray(cameraRef.transform.position, cameraRef.transform.forward);
        int raycastMask = interactLayer.value == 0 ? Physics.DefaultRaycastLayers : interactLayer.value;

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, raycastMask))
        {
            return;
        }

        ChessTile tile = ResolveTile(hit);
        if (tile != null)
        {
            SelectTile(tile);
            return;
        }

        InteractableChessPiece piece = hit.collider.GetComponent<InteractableChessPiece>();
        if (piece != null)
        {
            piece.OnInteract();
        }
    }

    ChessTile ResolveTile(RaycastHit hit)
    {
        if (chessBoard != null)
        {
            return chessBoard.GetTileFromRaycast(hit);
        }

        return hit.collider.GetComponentInParent<ChessTile>();
    }

    void SelectTile(ChessTile tile)
    {
        if (currentHighlightedTile != null && currentHighlightedTile != tile)
        {
            currentHighlightedTile.ResetColor();
        }

        tile.Highlight(Color.green);
        currentHighlightedTile = tile;

        Debug.Log($"Tile: {tile.TileName}");
    }

    #endregion
}
