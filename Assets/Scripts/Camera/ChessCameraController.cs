using UnityEngine;

[DisallowMultipleComponent]
public class ChessCameraController : MonoBehaviour
{
    public enum CameraMode
    {
        FirstPerson,
        TacticalView
    }

    #region Singleton

    public static ChessCameraController Instance { get; private set; }

    public static ChessCameraController GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        ChessCameraController existing = FindFirstObjectByType<ChessCameraController>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        Camera mainCamera = Camera.main;
        GameObject host = mainCamera != null ? mainCamera.gameObject : new GameObject("ChessCameraController");
        Instance = host.GetComponent<ChessCameraController>();
        if (Instance == null)
        {
            Instance = host.AddComponent<ChessCameraController>();
        }

        return Instance;
    }

    #endregion

    #region Variables

    [Header("Transition")]
    [SerializeField] float transitionDuration = 0.35f;

    [Header("Framing")]
    [SerializeField] float framingPadding = 1.2f;
    [SerializeField] Vector2 tacticalPitchRange = new(50f, 65f);
    [SerializeField] float tacticalYawBias = 38f;
    [SerializeField] float tacticalHeightBlend = 0.6f;

    Camera controlledCamera;

    CameraMode currentMode = CameraMode.FirstPerson;
    bool isTransitioningToTactical;
    bool isTransitioningToFirstPerson;

    Transform firstPersonParent;
    Vector3 firstPersonLocalPosition;
    Quaternion firstPersonLocalRotation;

    Vector3 transitionStartPosition;
    Quaternion transitionStartRotation;
    Vector3 transitionTargetPosition;
    Quaternion transitionTargetRotation;
    float transitionElapsedTime;
    float currentTransitionDuration;

    #endregion

    #region Properties

    public CameraMode CurrentMode => currentMode;

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
        ResolveCamera();
        ChessCursorStateCoordinator.SetTacticalCursorOverride(currentMode == CameraMode.TacticalView);
    }

    void LateUpdate()
    {
        if (controlledCamera == null)
        {
            ResolveCamera();
            if (controlledCamera == null)
            {
                return;
            }
        }

        HandleTransition();
    }

    #endregion

    #region Camera State

    public bool IsInFirstPerson()
    {
        return currentMode == CameraMode.FirstPerson && !isTransitioningToFirstPerson;
    }

    public bool IsInTacticalMode()
    {
        return currentMode == CameraMode.TacticalView || isTransitioningToTactical;
    }

    public void EnterTacticalView(ChessPiece selectedPiece)
    {
        if (selectedPiece == null || IsInTacticalMode())
        {
            return;
        }

        ResolveCamera();
        if (controlledCamera == null)
        {
            return;
        }

        CacheFirstPersonLocalPose();
        transitionStartPosition = controlledCamera.transform.position;
        transitionStartRotation = controlledCamera.transform.rotation;
        controlledCamera.transform.SetParent(null, true);

        SolveTacticalPose(out Vector3 tacticalTargetPosition, out Quaternion tacticalTargetRotation);
        BeginTransition(tacticalTargetPosition, tacticalTargetRotation);

        currentMode = CameraMode.TacticalView;
        isTransitioningToTactical = true;
        isTransitioningToFirstPerson = false;
        ChessCursorStateCoordinator.SetTacticalCursorOverride(currentMode == CameraMode.TacticalView);
    }

    public void ExitTacticalView()
    {
        if (!IsInTacticalMode())
        {
            return;
        }

        ResolveCamera();
        if (controlledCamera == null)
        {
            return;
        }

        transitionStartPosition = controlledCamera.transform.position;
        transitionStartRotation = controlledCamera.transform.rotation;
        ResolveFirstPersonWorldPose(out Vector3 firstPersonTargetPosition, out Quaternion firstPersonTargetRotation);
        BeginTransition(firstPersonTargetPosition, firstPersonTargetRotation);

        currentMode = CameraMode.FirstPerson;
        isTransitioningToFirstPerson = true;
        isTransitioningToTactical = false;
        ChessCursorStateCoordinator.SetTacticalCursorOverride(currentMode == CameraMode.TacticalView);
    }

    public void SetTransitionSpeed(float speed)
    {
        transitionDuration = 1f / Mathf.Max(0.01f, speed);
    }

    #endregion

    #region Transition

    void HandleTransition()
    {
        if (!isTransitioningToTactical && !isTransitioningToFirstPerson)
        {
            return;
        }

        float duration = Mathf.Max(0.001f, currentTransitionDuration);
        transitionElapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(transitionElapsedTime / duration);
        controlledCamera.transform.position = Vector3.Lerp(transitionStartPosition, transitionTargetPosition, t);
        controlledCamera.transform.rotation = Quaternion.Slerp(transitionStartRotation, transitionTargetRotation, t);

        if (t < 1f)
        {
            return;
        }

        controlledCamera.transform.position = transitionTargetPosition;
        controlledCamera.transform.rotation = transitionTargetRotation;

        if (isTransitioningToFirstPerson)
        {
            CompleteReturnToFirstPerson();
        }

        isTransitioningToTactical = false;
        isTransitioningToFirstPerson = false;
    }

    void BeginTransition(Vector3 targetPosition, Quaternion targetRotation)
    {
        transitionTargetPosition = targetPosition;
        transitionTargetRotation = targetRotation;
        transitionElapsedTime = 0f;
        currentTransitionDuration = Mathf.Max(0.001f, transitionDuration);
    }

    void CompleteReturnToFirstPerson()
    {
        if (firstPersonParent != null)
        {
            controlledCamera.transform.SetParent(firstPersonParent, true);
            controlledCamera.transform.localPosition = firstPersonLocalPosition;
            controlledCamera.transform.localRotation = firstPersonLocalRotation;
        }
    }
    
    #endregion

    
    #region Tactical Solver

    void SolveTacticalPose(out Vector3 position, out Quaternion rotation)
    {
        Bounds boardBounds = ResolveBoardBounds();
        Vector3 center = boardBounds.center;

        float minPitch = Mathf.Min(tacticalPitchRange.x, tacticalPitchRange.y);
        float maxPitch = Mathf.Max(tacticalPitchRange.x, tacticalPitchRange.y);
        float pitch = Mathf.Clamp(Mathf.Lerp(minPitch, maxPitch, 0.55f), minPitch, maxPitch);

        Vector3 right = Vector3.right;
        Vector3 forward = Vector3.forward;
        if (TryGetBoardAxes(out Vector3 boardRight, out Vector3 boardForward))
        {
            right = boardRight;
            forward = boardForward;
        }

        Quaternion yawRotation = Quaternion.AngleAxis(tacticalYawBias, Vector3.up);
        Vector3 horizontalDirection = yawRotation * ((forward + right).normalized);
        if (horizontalDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            horizontalDirection = new Vector3(0.7f, 0f, 0.7f).normalized;
        }

        float radius = ResolveFramingDistance(boardBounds, pitch);
        float height = Mathf.Max(boardBounds.size.y + 1.25f, radius * Mathf.Sin(pitch * Mathf.Deg2Rad));
        float flatDistance = radius * Mathf.Cos(pitch * Mathf.Deg2Rad);

        Vector3 horizontalOffset = horizontalDirection * flatDistance;
        Vector3 stagedOffset = Vector3.up * Mathf.Lerp(height, height * 1.15f, tacticalHeightBlend);

        position = center + stagedOffset + horizontalOffset;
        rotation = Quaternion.LookRotation((center - position).normalized, Vector3.up);
    }

    float ResolveFramingDistance(Bounds boardBounds, float pitch)
    {
        float verticalSize = Mathf.Max(1f, boardBounds.size.z * framingPadding);
        float horizontalSize = Mathf.Max(1f, boardBounds.size.x * framingPadding);

        float verticalFov = Mathf.Max(5f, controlledCamera.fieldOfView);
        float horizontalFov = Camera.VerticalToHorizontalFieldOfView(verticalFov, Mathf.Max(0.1f, controlledCamera.aspect));

        float verticalDistance = verticalSize * 0.5f / Mathf.Tan(0.5f * verticalFov * Mathf.Deg2Rad);
        float horizontalDistance = horizontalSize * 0.5f / Mathf.Tan(0.5f * horizontalFov * Mathf.Deg2Rad);

        float baseDistance = Mathf.Max(verticalDistance, horizontalDistance);
        float pitchFactor = 1f / Mathf.Clamp(Mathf.Cos((90f - pitch) * Mathf.Deg2Rad), 0.35f, 1f);
        return baseDistance * pitchFactor;
    }

    Bounds ResolveBoardBounds()
    {
        ChessBoard board = ChessBoard.Instance;
        if (board == null)
        {
            board = FindFirstObjectByType<ChessBoard>();
        }

        if (board == null)
        {
            return new Bounds(Vector3.zero, new Vector3(8f, 1f, 8f));
        }

        ChessTile[] tiles = board.GetAllTiles();
        if (tiles.Length == 0)
        {
            return new Bounds(board.transform.position, new Vector3(8f, 1f, 8f));
        }

        bool hasBounds = false;
        Bounds combined = default;

        for (int i = 0; i < tiles.Length; i++)
        {
            ChessTile tile = tiles[i];
            if (tile == null)
            {
                continue;
            }

            if (TryGetTileBounds(tile, out Bounds tileBounds))
            {
                if (!hasBounds)
                {
                    combined = tileBounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(tileBounds);
                }
            }
        }

        return hasBounds ? combined : new Bounds(board.transform.position, new Vector3(8f, 1f, 8f));
    }

    bool TryGetBoardAxes(out Vector3 right, out Vector3 forward)
    {
        right = Vector3.right;
        forward = Vector3.forward;

        ChessBoard board = ChessBoard.Instance;
        if (board == null)
        {
            board = FindFirstObjectByType<ChessBoard>();
        }

        if (board == null)
        {
            return false;
        }

        ChessTile a1 = board.GetTile("A1");
        ChessTile h1 = board.GetTile("H1");
        ChessTile a8 = board.GetTile("A8");

        if (a1 == null || h1 == null || a8 == null)
        {
            return false;
        }

        right = (h1.transform.position - a1.transform.position);
        right.y = 0f;
        if (right.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        forward = (a8.transform.position - a1.transform.position);
        forward.y = 0f;
        if (forward.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        right.Normalize();
        forward.Normalize();
        return true;
    }

    bool TryGetTileBounds(ChessTile tile, out Bounds bounds)
    {
        bounds = default;

        Renderer tileRenderer = tile.GetComponent<Renderer>();
        if (tileRenderer != null)
        {
            bounds = tileRenderer.bounds;
            return true;
        }

        Collider tileCollider = tile.GetComponent<Collider>();
        if (tileCollider != null)
        {
            bounds = tileCollider.bounds;
            return true;
        }

        bounds = new Bounds(tile.transform.position, Vector3.one * 0.5f);
        return true;
    }

    #endregion

    #region Pose Cache

    void ResolveCamera()
    {
        controlledCamera = GetComponentInChildren<Camera>(true);
        if (controlledCamera != null)
        {
            return;
        }

        if (Camera.main != null)
        {
            controlledCamera = Camera.main;
            return;
        }

        controlledCamera = FindFirstObjectByType<Camera>();
    }

    void CacheFirstPersonLocalPose()
    {
        firstPersonParent = controlledCamera.transform.parent;
        firstPersonLocalPosition = controlledCamera.transform.localPosition;
        firstPersonLocalRotation = controlledCamera.transform.localRotation;
    }

    void ResolveFirstPersonWorldPose(out Vector3 position, out Quaternion rotation)
    {
        if (firstPersonParent != null)
        {
            position = firstPersonParent.TransformPoint(firstPersonLocalPosition);
            rotation = firstPersonParent.rotation * firstPersonLocalRotation;
            return;
        }

        position = controlledCamera.transform.position;
        rotation = controlledCamera.transform.rotation;
    }

    #endregion
}
