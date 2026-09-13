using UnityEngine;

public class VehicleMover : MonoBehaviour
{
    private Vector3 direction = Vector3.forward;
    private float cruiseSpeed = 5f;
    private float currentSpeed = 0f;

    // Despawn rules.
    private Vector3 townCenter = Vector3.zero;
    private float despawnRadius = 250f;
    private float maxLifetime = 30f;
    private float age;

    [Header("Traffic Sensor")]
    [Tooltip("How far ahead the car looks for traffic.")]
    [SerializeField] private float sensorLength = 4f;
    [Tooltip("Sideways start of the ray from the car's centre (front bumper).")]
    [SerializeField] private float sensorForwardOffset = 1.5f;
    [Tooltip("How high off the ground the ray sits.")]
    [SerializeField] private float sensorHeight = 0.5f;
    [Tooltip("Which layers count as traffic to brake for.")]
    [SerializeField] private LayerMask trafficMask = ~0;
    [Tooltip("How quickly the car speeds up / slows down.")]
    [SerializeField] private float acceleration = 8f;

    public void Init(Vector3 moveDirection, float moveSpeed,
                     Vector3 center, float radius)
    {
        direction = moveDirection.normalized;
        cruiseSpeed = moveSpeed;
        currentSpeed = moveSpeed;   // start at speed
        townCenter = center;
        despawnRadius = radius;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    private void Update()
    {
        // Decide target speed: full cruise, or slow/stop if something's ahead.
        float targetSpeed = cruiseSpeed;

        Vector3 origin = transform.position
                       + direction * sensorForwardOffset
                       + Vector3.up * sensorHeight;

        if (Physics.Raycast(origin, direction, out RaycastHit hit,
                            sensorLength, trafficMask, QueryTriggerInteraction.Ignore))
        {
            // Closer the car ahead, the more we slow. Stop when very close.
            float t = Mathf.Clamp01(hit.distance / sensorLength);
            targetSpeed = cruiseSpeed * t;
            if (hit.distance < sensorLength * 0.35f) targetSpeed = 0f;
        }

        // Ease toward the target speed so braking looks smooth.
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed,
                                         acceleration * Time.deltaTime);

        transform.position += direction * currentSpeed * Time.deltaTime;

        age += Time.deltaTime;

        Vector3 flatOffset = transform.position - townCenter;
        flatOffset.y = 0f;
        bool pastEdge = flatOffset.magnitude > despawnRadius;
        bool headingOut = Vector3.Dot(direction, flatOffset.normalized) > 0f;

        if ((pastEdge && headingOut) || age >= maxLifetime)
        {
            Destroy(gameObject);
        }
    }

    // Visualise the sensor ray in the editor when the car is selected.
    private void OnDrawGizmosSelected()
    {
        Vector3 dir = Application.isPlaying ? direction : transform.forward;
        Vector3 origin = transform.position + dir * sensorForwardOffset + Vector3.up * sensorHeight;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + dir * sensorLength);
    }
}