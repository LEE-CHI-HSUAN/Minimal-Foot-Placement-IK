using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Config")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 500f;

    [Header("Animation")]
    [SerializeField] Animator animator;
    private readonly int speed = Animator.StringToHash("Speed");

    [Header("Input")]
    [SerializeField] InputAction inputAction;

    private Transform cameraTransform;
    private CharacterController characterController;
    private Vector3 velocity;

    void OnEnable()
    {
        inputAction.Enable();
    }

    void OnDisable()
    {
        inputAction.Disable();
    }

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        if (cameraTransform == null)
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            else
            {
                Debug.LogWarning("No Main Camera found or assigned to cameraTransform. Movement may not be relative to camera.");
            }
        }

        if (animator == null)
        {
            if (!TryGetComponent<Animator>(out animator))
            {
                Debug.LogWarning("No Animator found or assigned.");
            }
        }
    }

    void Update()
    {
        // Check if the character is on the ground
        if (characterController.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small downward force to keep it grounded, prevents "floating" slightly above ground
        }

        // Get input for horizontal and vertical movement
        Vector2 movement = inputAction.ReadValue<Vector2>();
        float horizontalInput = movement.x;
        float verticalInput = movement.y;

        // Calculate movement direction relative to the camera
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        // Ensure movement is on the XZ plane and normalized
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // Calculate the desired horizontal movement direction
        Vector3 moveDirection = (forward * verticalInput + right * horizontalInput).normalized;
        // Visual
        animator.SetFloat(speed, moveDirection.magnitude);

        // Apply movement using CharacterController
        if (moveDirection.magnitude >= 0.1f) // Only move if there's significant input
        {
            // Rotate the character to face the movement direction
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            // Apply horizontal movement
            characterController.Move(moveSpeed * Time.deltaTime * moveDirection);
        }

        // Apply gravity
        velocity.y += Physics.gravity.y * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }
}
