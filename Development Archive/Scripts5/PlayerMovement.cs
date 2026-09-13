using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;

    [Header("Animation")]
    [Tooltip("How quickly the Speed parameter eases toward its target. " +
             "Lower = smoother idle/run transition, higher = snappier.")]
    [SerializeField] private float speedSmoothTime = 0.1f;

    private CharacterController controller;
    private Animator animator;
    private float verticalVelocity;

    // Must match the Float parameter name in your Animator Controller ("Speed").
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // The Animator lives on the character model, which is a child object,
        // so we search the children rather than this GameObject.
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical);

        // Prevent diagonal movement from being faster.
        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }

        // Rotate toward movement direction.
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        // CharacterController does not automatically apply gravity.
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity =
            moveDirection * moveSpeed +
            Vector3.up * verticalVelocity;

        controller.Move(velocity * Time.deltaTime);

        // Drive the animation blend tree.
        // 0 = idle, ~0.5 = walk, 1 = full run.
        if (animator != null)
        {
            float inputMagnitude = Mathf.Clamp01(moveDirection.magnitude);
            animator.SetFloat(SpeedHash, inputMagnitude, speedSmoothTime, Time.deltaTime);
        }
    }
}