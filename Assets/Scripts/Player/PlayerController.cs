using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    #region Settings

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 45f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Auto Setup")]
    [SerializeField] private float cameraHeight = 1.6f;

    #endregion

    #region State

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Transform cameraPivot;
    private Transform interactionPoint;

    private InputAction moveAction;
    private InputAction lookAction;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private float pitch;

    private readonly HashSet<string> warnedKeys = new();

    #endregion

    #region Unity

    private void Awake()
    {
        EnsureCoreComponents();
        EnsureCameraHierarchy();
        EnsureInputActions();
        LockCursor();
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        lookAction?.Enable();
        LockCursor();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        lookAction?.Disable();
    }

    private void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();
        lookInput = lookAction.ReadValue<Vector2>();
        ApplyLook();
    }

    private void FixedUpdate()
    {
        ApplyMovement();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            LockCursor();
        }
    }

    #endregion

    #region Setup

    private void EnsureCoreComponents()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            WarnOnce("rb_created", "PlayerController: Rigidbody was missing and has been created automatically.");
        }

        if (capsule == null)
        {
            capsule = gameObject.AddComponent<CapsuleCollider>();
            WarnOnce("capsule_created", "PlayerController: CapsuleCollider was missing and has been created automatically.");
        }

        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        capsule.center = new Vector3(0f, 1f, 0f);
        capsule.height = 2f;
        capsule.radius = 0.3f;
    }

    private void EnsureCameraHierarchy()
    {
        Camera existingCamera = GetComponentInChildren<Camera>(true);
        if (existingCamera == null)
        {
            GameObject cameraObject = new("PlayerCamera");
            cameraObject.transform.SetParent(transform, false);
            existingCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            WarnOnce("camera_created", "PlayerController: Camera child was missing and has been created automatically.");
        }

        cameraPivot = existingCamera.transform;
        cameraPivot.SetParent(transform, false);
        cameraPivot.localPosition = new Vector3(0f, cameraHeight, 0f);
        cameraPivot.localRotation = Quaternion.identity;

        Transform foundInteractionPoint = cameraPivot.Find("InteractionPoint");
        if (foundInteractionPoint == null)
        {
            GameObject interactionObject = new("InteractionPoint");
            interactionObject.transform.SetParent(cameraPivot, false);
            foundInteractionPoint = interactionObject.transform;
            WarnOnce("interaction_created", "PlayerController: InteractionPoint was missing and has been created automatically.");
        }

        interactionPoint = foundInteractionPoint;
        interactionPoint.localPosition = Vector3.forward;
        interactionPoint.localRotation = Quaternion.identity;
    }

    private void EnsureInputActions()
    {
        if (moveAction != null && lookAction != null)
        {
            return;
        }

        moveAction = new InputAction(name: "Move", type: InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        lookAction = new InputAction(name: "Look", type: InputActionType.Value, binding: "<Mouse>/delta");
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    #endregion

    #region Movement

    private void ApplyMovement()
    {
        Vector3 desiredMove = (transform.right * moveInput.x + transform.forward * moveInput.y);
        if (desiredMove.sqrMagnitude > 1f)
        {
            desiredMove.Normalize();
        }

        Vector2 desiredHorizontalVelocity = new Vector2(desiredMove.x, desiredMove.z) * moveSpeed;

        Vector3 currentVelocity = rb.linearVelocity;
        Vector2 currentHorizontalVelocity = new Vector2(currentVelocity.x, currentVelocity.z);

        float maxVelocityDelta = acceleration * Time.fixedDeltaTime;
        Vector2 velocityDelta = desiredHorizontalVelocity - currentHorizontalVelocity;
        velocityDelta = Vector2.ClampMagnitude(velocityDelta, maxVelocityDelta);

        Vector2 nextHorizontalVelocity = currentHorizontalVelocity + velocityDelta;

        if (moveInput.sqrMagnitude < 0.0001f && nextHorizontalVelocity.sqrMagnitude < 0.0025f)
        {
            nextHorizontalVelocity = Vector2.zero;
        }

        rb.linearVelocity = new Vector3(nextHorizontalVelocity.x, currentVelocity.y, nextHorizontalVelocity.y);
    }

    #endregion

    #region Look

    private void ApplyLook()
    {
        ChessCameraController cameraController = ChessCameraController.Instance;
        if (cameraController != null && !cameraController.IsInFirstPerson())
        {
            return;
        }

        float yawDelta = lookInput.x * lookSensitivity;
        float pitchDelta = lookInput.y * lookSensitivity;

        pitch = Mathf.Clamp(pitch - pitchDelta, minPitch, maxPitch);

        transform.Rotate(Vector3.up * yawDelta, Space.Self);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    #endregion

    #region Debug

    private void WarnOnce(string key, string message)
    {
        if (warnedKeys.Add(key))
        {
            Debug.LogWarning(message, this);
        }
    }

    #endregion
}
