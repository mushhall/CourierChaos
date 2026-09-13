using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Positioning")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 8f, -8f);

    [SerializeField] private float lookHeight = 1.5f;

    [Header("Smoothing")]
    [SerializeField] private float smoothTime = 0.2f;

    private Vector3 currentVelocity;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            smoothTime
        );

        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}