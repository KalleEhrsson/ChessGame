using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PromotionSelection3DController : MonoBehaviour
{
    static readonly PieceType[] PromotionOrder = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

    readonly List<GameObject> spawnedOptions = new();
    Action<PieceType> selectionCallback;
    Camera activeCamera;
    ChessCapturedPieceTray capturedTray;

    [SerializeField] float rowHeightOffset = 0.28f;
    [SerializeField] float rowForwardOffset = 0.16f;
    [SerializeField] float optionSpacing = 0.24f;
    [SerializeField] Color ghostTint = new(0.55f, 0.72f, 1f, 0.55f);

    public bool IsSelecting { get; private set; }

    void Update()
    {
        if (!IsSelecting || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        activeCamera ??= Camera.main;
        if (activeCamera == null)
        {
            return;
        }

        Ray ray = activeCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            return;
        }

        PromotionPieceOption option = hit.collider.GetComponentInParent<PromotionPieceOption>();
        option?.NotifyClicked();
    }

    public void Show(PieceTeam team, Action<PieceType> onSelected)
    {
        Hide();
        selectionCallback = onSelected;
        activeCamera = Camera.main;

        ChessBoard board = ChessBoard.Instance != null ? ChessBoard.Instance : FindFirstObjectByType<ChessBoard>();
        if (board == null)
        {
            return;
        }

        capturedTray = board.GetComponent<ChessCapturedPieceTray>();
        if (capturedTray == null)
        {
            capturedTray = ChessCapturedPieceTray.GetOrCreate(board);
        }

        if (capturedTray == null || !capturedTray.TryGetPromotionRowPose(team, rowHeightOffset, rowForwardOffset, out Vector3 origin, out Vector3 right, out Quaternion rotation))
        {
            return;
        }

        for (int i = 0; i < PromotionOrder.Length; i++)
        {
            PieceType type = PromotionOrder[i];
            bool isGhost = !capturedTray.HasCapturedPieceOfType(team, type);
            if (!board.TryGetPiecePrefab(team, type, out GameObject prefab) || prefab == null)
            {
                continue;
            }

            Vector3 position = origin + right * (i * optionSpacing);
            GameObject optionObject = Instantiate(prefab, position, rotation, transform);
            optionObject.name = $"PromotionOption_{team}_{type}";
            ConfigureOptionObject(optionObject, type, isGhost);
            spawnedOptions.Add(optionObject);
        }

        IsSelecting = spawnedOptions.Count > 0;
    }

    public void Hide()
    {
        for (int i = 0; i < spawnedOptions.Count; i++)
        {
            if (spawnedOptions[i] != null)
            {
                Destroy(spawnedOptions[i]);
            }
        }

        spawnedOptions.Clear();
        selectionCallback = null;
        IsSelecting = false;
    }

    public void HandleOptionClicked(PromotionPieceOption option)
    {
        if (!IsSelecting || option == null)
        {
            return;
        }

        Action<PieceType> callback = selectionCallback;
        Hide();
        callback?.Invoke(option.PromotionType);
    }

    void ConfigureOptionObject(GameObject optionObject, PieceType type, bool isGhost)
    {
        ChessPiece piece = optionObject.GetComponent<ChessPiece>();
        if (piece != null)
        {
            piece.SetTile(null);
            Destroy(piece);
        }

        InteractableChessPiece interactable = optionObject.GetComponent<InteractableChessPiece>();
        if (interactable != null)
        {
            Destroy(interactable);
        }

        ChessPieceMotion motion = optionObject.GetComponent<ChessPieceMotion>();
        if (motion != null)
        {
            Destroy(motion);
        }

        Rigidbody[] rigidbodies = optionObject.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Destroy(rigidbodies[i]);
        }

        Collider[] colliders = optionObject.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            Bounds bounds = CalculateWorldBounds(optionObject.transform);
            BoxCollider box = optionObject.AddComponent<BoxCollider>();
            box.center = optionObject.transform.InverseTransformPoint(bounds.center);
            box.size = optionObject.transform.InverseTransformVector(bounds.size);
            colliders = new Collider[] { box };
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = true;
            colliders[i].isTrigger = false;
        }

        PromotionPieceOption option = optionObject.AddComponent<PromotionPieceOption>();
        option.Initialize(this, type, isGhost);

        if (!isGhost)
        {
            return;
        }

        Renderer[] renderers = optionObject.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MaterialPropertyBlock block = new();
            renderers[i].GetPropertyBlock(block);
            block.SetColor("_Color", ghostTint);
            renderers[i].SetPropertyBlock(block);
        }
    }

    static Bounds CalculateWorldBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        return new Bounds(root.position, Vector3.one * 0.2f);
    }
}
