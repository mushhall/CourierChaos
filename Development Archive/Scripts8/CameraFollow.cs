using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The object the camera follows. Drag your Player here.")]
    [SerializeField] private Transform target;

    [Header("Positioning")]
    [Tooltip("Offset from the target in world space. " +
             "Y = height above, negative Z = distance behind.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 8f, -8f);

    [Tooltip("How high above the target's pivot the camera aims. " +
             "Keeps the view on the torso/head instead of the feet.")]
    [SerializeField] private float lookHeight = 1.5f;

    [Header("Smoothing")]
    [Tooltip("How quickly the camera catches up. " +
             "Lower = snappier/tighter, higher = floatier/laggier.")]
    [SerializeField] private float smoothTime = 0.2f;

    private Vector3 currentVelocity;

    private void LateUpdate()
    {
        // LateUpdate runs after movement + animation, which prevents jitter.
        if (target == null) return;

        // Desired position: the target plus a fixed WORLD-space offset.
        // Because the offset never rotates with the player, the camera angle
        // stays constant and world-aligned input remains intuitive.
        Vector3 desiredPosition = target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            smoothTime
        );

        // Keep the target framed, aiming a little above its feet.
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}