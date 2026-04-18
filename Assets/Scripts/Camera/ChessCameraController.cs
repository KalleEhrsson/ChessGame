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

    [SerializeField] float transitionSpeed = 6f;
    [SerializeField] float tacticalHeight = 9.5f;
    [SerializeField] float tacticalBackOffset = 5f;

    Camera controlledCamera;
    Transform tacticalCameraPoint;

    CameraMode currentMode = CameraMode.FirstPerson;
    bool isTransitioningToTactical;
    bool isTransitioningToFirstPerson;

    Transform firstPersonParent;
    Vector3 firstPersonLocalPosition;
    Quaternion firstPersonLocalRotation;

    const float CompleteThreshold = 0.02f;

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
        ResolveOrCreateTacticalPoint();
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

    #region Camera

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
        if (selectedPiece == null)
        {
            return;
        }

        if (IsInTacticalMode())
        {
            return;
        }

        ResolveCamera();
        if (controlledCamera == null)
        {
            return;
        }

        ResolveOrCreateTacticalPoint();
        CacheFirstPersonLocalPose();

        controlledCamera.transform.SetParent(null, true);

        currentMode = CameraMode.TacticalView;
        isTransitioningToTactical = true;
        isTransitioningToFirstPerson = false;
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

        currentMode = CameraMode.FirstPerson;
        isTransitioningToFirstPerson = true;
        isTransitioningToTactical = false;
    }

    public void SetTransitionSpeed(float speed)
    {
        transitionSpeed = Mathf.Max(0.01f, speed);
    }

    void HandleTransition()
    {
        if (controlledCamera == null)
        {
            return;
        }

        if (currentMode == CameraMode.TacticalView)
        {
            if (tacticalCameraPoint == null)
            {
                ResolveOrCreateTacticalPoint();
            }

            MoveCameraTowards(tacticalCameraPoint.position, tacticalCameraPoint.rotation);
            if (isTransitioningToTactical && IsAtTarget(tacticalCameraPoint.position, tacticalCameraPoint.rotation))
            {
                isTransitioningToTactical = false;
            }

            return;
        }

        if (!isTransitioningToFirstPerson)
        {
            return;
        }

        ResolveFirstPersonWorldPose(out Vector3 targetPosition, out Quaternion targetRotation);
        MoveCameraTowards(targetPosition, targetRotation);

        if (!IsAtTarget(targetPosition, targetRotation))
        {
            return;
        }

        CompleteReturnToFirstPerson();
    }

    void MoveCameraTowards(Vector3 targetPosition, Quaternion targetRotation)
    {
        float blend = Time.deltaTime * transitionSpeed;
        controlledCamera.transform.position = Vector3.Lerp(controlledCamera.transform.position, targetPosition, blend);
        controlledCamera.transform.rotation = Quaternion.Slerp(controlledCamera.transform.rotation, targetRotation, blend);
    }

    bool IsAtTarget(Vector3 targetPosition, Quaternion targetRotation)
    {
        float positionDelta = Vector3.Distance(controlledCamera.transform.position, targetPosition);
        float angleDelta = Quaternion.Angle(controlledCamera.transform.rotation, targetRotation);
        return positionDelta <= CompleteThreshold && angleDelta <= 0.5f;
    }

    void CompleteReturnToFirstPerson()
    {
        if (firstPersonParent != null)
        {
            controlledCamera.transform.SetParent(firstPersonParent, true);
            controlledCamera.transform.localPosition = firstPersonLocalPosition;
            controlledCamera.transform.localRotation = firstPersonLocalRotation;
        }

        isTransitioningToFirstPerson = false;
    }

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

        Camera found = FindFirstObjectByType<Camera>();
        if (found != null)
        {
            controlledCamera = found;
        }
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

    void ResolveOrCreateTacticalPoint()
    {
        if (tacticalCameraPoint == null)
        {
            GameObject existing = GameObject.Find("TacticalCameraPoint");
            if (existing != null)
            {
                tacticalCameraPoint = existing.transform;
            }
        }

        if (tacticalCameraPoint == null)
        {
            GameObject anchorObject = new("TacticalCameraPoint");
            tacticalCameraPoint = anchorObject.transform;
        }

        PositionTacticalPoint();
    }

    void PositionTacticalPoint()
    {
        if (tacticalCameraPoint == null)
        {
            return;
        }

        Bounds boardBounds = ResolveBoardBounds();
        Vector3 center = boardBounds.center;
        Vector3 offset = new Vector3(0f, tacticalHeight, -Mathf.Max(1f, tacticalBackOffset));

        tacticalCameraPoint.position = center + offset;
        tacticalCameraPoint.rotation = Quaternion.LookRotation(center - tacticalCameraPoint.position, Vector3.up);
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
            return new Bounds(Vector3.zero, Vector3.one * 8f);
        }

        Renderer[] renderers = board.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        Collider[] colliders = board.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }

            return bounds;
        }

        return new Bounds(board.transform.position, Vector3.one * 8f);
    }

    #endregion
}
