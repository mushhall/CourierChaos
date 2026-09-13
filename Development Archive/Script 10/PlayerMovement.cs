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
    [SerializeField] private float speedSmoothTime = 0.1f;

    [Header("Camera")]
    [Tooltip("Movement is relative to this camera. Auto-uses Main Camera if empty.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Knockback")]
    [Tooltip("How quickly the horizontal fling fades. Higher = snappier, less floaty drift.")]
    [SerializeField] private float knockbackDamping = 9f;
    [Tooltip("Extra gravity while flying from a hit, so you arc and drop fast (earthly, not floaty).")]
    [SerializeField] private float airGravityMultiplier = 2.5f;

    private CharacterController controller;
    private Animator animator;
    private float verticalVelocity;

    private float baseMoveSpeed;
    private float boostEndTime;

    private Vector3 knockback;      // horizontal knockback velocity
    private float stunEndTime;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    public bool IsStunned => Time.time < stunEndTime;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        baseMoveSpeed = moveSpeed;
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    // Called by a car when it hits the player.
    public void ApplyKnockback(Vector3 horizontalDir, float force, float upForce, float stunDuration)
    {
        horizontalDir.y = 0f;
        if (horizontalDir.sqrMagnitude > 0.001f) horizontalDir.Normalize();
        else horizontalDir = -transform.forward;

        knockback = horizontalDir * force;
        verticalVelocity = upForce;      // pop up so they land a distance away
        stunEndTime = Time.time + stunDuration;
    }

    private void Update()
    {
        if (moveSpeed != baseMoveSpeed && Time.time >= boostEndTime)
            moveSpeed = baseMoveSpeed;

        bool stunned = IsStunned;

        // Input (ignored while stunned/flying).
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector2 input = stunned ? Vector2.zero : new Vector2(horizontal, vertical);
        if (input.sqrMagnitude > 1f) input.Normalize();

        // Camera-relative move direction.
        Vector3 moveDirection = Vector3.zero;
        if (input.sqrMagnitude > 0.0001f)
        {
            if (cameraTransform != null)
            {
                Vector3 f = cameraTransform.forward; f.y = 0f; f.Normalize();
                Vector3 r = cameraTransform.right; r.y = 0f; r.Normalize();
                moveDirection = f * input.y + r * input.x;
            }
            else moveDirection = new Vector3(input.x, 0f, input.y);
        }

        // Rotate toward movement (not while stunned).
        if (!stunned && moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Gravity — stronger while airborne from a knockback, for a snappy earthly arc.
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        float g = (!controller.isGrounded && IsStunned) ? gravity * airGravityMultiplier : gravity;
        verticalVelocity += g * Time.deltaTime;

        // Horizontal knockback fades out quickly (no floaty drift).
        knockback = Vector3.MoveTowards(knockback, Vector3.zero,
                                        knockbackDamping * knockback.magnitude * Time.deltaTime
                                        + knockbackDamping * Time.deltaTime);

        Vector3 velocity = moveDirection * moveSpeed + knockback + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        // Animation reflects actual horizontal speed (so it animates while flung too).
        if (animator != null)
        {
            Vector3 horiz = moveDirection * moveSpeed + knockback;
            float anim = Mathf.Clamp01(horiz.magnitude / Mathf.Max(0.1f, baseMoveSpeed));
            animator.SetFloat(SpeedHash, anim, speedSmoothTime, Time.deltaTime);
        }
    }

    public void ApplySpeedBoost(float multiplier, float duration)
    {
        moveSpeed = baseMoveSpeed * multiplier;
        boostEndTime = Time.time + duration;
    }
}