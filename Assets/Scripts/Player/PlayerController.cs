using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    #region Variables

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float acceleration = 20f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2f;
    public Transform cameraTransform;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.2f;
    public LayerMask groundLayer;

    private Rigidbody rb;
    private float verticalRotation;
    private bool isGrounded;

    #endregion

    #region Unity

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMouseLook();
        CheckGround();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    #endregion

    #region Movement

    void HandleMovement()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 moveDir = (transform.right * x + transform.forward * z).normalized;

        Vector3 targetVelocity = moveDir * moveSpeed;

        Vector3 velocity = rb.linearVelocity;
        Vector3 velocityChange = targetVelocity - new Vector3(velocity.x, 0, velocity.z);

        velocityChange = Vector3.ClampMagnitude(velocityChange, acceleration);

        rb.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    #endregion

    #region Mouse Look

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * 100f * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * 100f * Time.deltaTime;

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -80f, 80f);

        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    #endregion

    #region Ground

    void CheckGround()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance + 0.1f, groundLayer);
    }

    #endregion
}