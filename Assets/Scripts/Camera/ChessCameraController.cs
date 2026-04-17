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
    [SerializeField] float tacticalTilt = 58f;

    Camera controlledCamera;
    Transform tacticalCameraPoint;

    CameraMode currentMode = CameraMode.FirstPerson;
    Vector3 firstPersonPosition;
    Quaternion firstPersonRotation;

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
        CacheFirstPersonPose();
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
        return currentMode == CameraMode.FirstPerson;
    }

    public bool IsInTacticalMode()
    {
        return currentMode == CameraMode.TacticalView;
    }

    public void EnterTacticalView(ChessPiece selectedPiece)
    {
        if (selectedPiece == null)
        {
            return;
        }

        if (controlledCamera == null)
        {
            ResolveCamera();
        }

        ResolveOrCreateTacticalPoint();
        CacheFirstPersonPose();
        currentMode = CameraMode.TacticalView;
    }

    public void ExitTacticalView()
    {
        if (controlledCamera == null)
        {
            ResolveCamera();
        }

        currentMode = CameraMode.FirstPerson;
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

        Vector3 targetPosition;
        Quaternion targetRotation;

        if (currentMode == CameraMode.TacticalView)
        {
            if (tacticalCameraPoint == null)
            {
                ResolveOrCreateTacticalPoint();
            }

            targetPosition = tacticalCameraPoint.position;
            targetRotation = tacticalCameraPoint.rotation;
        }
        else
        {
            targetPosition = firstPersonPosition;
            targetRotation = firstPersonRotation;
        }

        float blend = Time.deltaTime * transitionSpeed;
        controlledCamera.transform.position = Vector3.Lerp(controlledCamera.transform.position, targetPosition, blend);
        controlledCamera.transform.rotation = Quaternion.Slerp(controlledCamera.transform.rotation, targetRotation, blend);
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

    void CacheFirstPersonPose()
    {
        if (controlledCamera == null)
        {
            return;
        }

        firstPersonPosition = controlledCamera.transform.position;
        firstPersonRotation = controlledCamera.transform.rotation;
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

        tacticalCameraPoint.position = center + new Vector3(0f, tacticalHeight, -tacticalHeight * 0.5f);
        tacticalCameraPoint.rotation = Quaternion.LookRotation(center - tacticalCameraPoint.position, Vector3.up);
        tacticalCameraPoint.rotation *= Quaternion.Euler(tacticalTilt * -0.15f, 0f, 0f);
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
