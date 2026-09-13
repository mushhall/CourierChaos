using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The object the camera follows. Drag your Player here.")]
    [SerializeField] private Transform target;

    [Header("Positioning")]
    [Tooltip("Offset from the target, in the player's LOCAL space. " +
             "Y = height, negative Z = distance behind the player's back.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 8f, -8f);

    [Tooltip("How high above the target's pivot the camera aims.")]
    [SerializeField] private float lookHeight = 1.5f;

    [Header("Smoothing")]
    [Tooltip("How quickly the camera catches up in POSITION. Lower = tighter.")]
    [SerializeField] private float positionSmoothTime = 0.18f;

    [Tooltip("How quickly the camera swings AROUND to behind the player. " +
             "Lower = lazier/gentler (less nausea), higher = snaps behind faster.")]
    [SerializeField] private float rotationSmoothSpeed = 3f;

    private Vector3 currentVelocity;
    private Quaternion followRotation;   // the camera rig's own eased facing

    private void Start()
    {
        if (target == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) target = pm.transform;
        }
        // Start already lined up behind the player so it doesn't swing in on frame 1.
        if (target != null) followRotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Ease our own yaw toward the player's facing (this lag kills the nausea).
        Quaternion targetYaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        followRotation = Quaternion.Slerp(
            followRotation, targetYaw, rotationSmoothSpeed * Time.deltaTime);

        // Place the camera at the offset rotated behind that eased facing.
        Vector3 desiredPosition = target.position + followRotation * offset;

        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPosition, ref currentVelocity, positionSmoothTime);

        // Always look at the player.
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}