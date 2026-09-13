using UnityEngine;

public class VehicleMover : MonoBehaviour
{
    private enum LaneState { InLane, Overtaking, Returning }
    private LaneState state = LaneState.InLane;

    private Vector3 direction = Vector3.forward;
    private Vector3 right = Vector3.right;
    private float cruiseSpeed = 5f;
    private float currentSpeed = 0f;

    private float lateralOffset = 0f;
    private float lateralTarget = 0f;

    private Transform overtakeTarget;
    private VehicleMover overtakeCar;
    private bool hasOvertaken = false;

    private float myLength = -1f;

    // Despawn.
    private Vector3 townCenter = Vector3.zero;
    private float despawnRadius = 250f;
    private float maxLifetime = 300f;
    private float age;

    [Header("Forward Sensor")]
    [SerializeField] private float sensorLength = 6f;
    [Tooltip("Start the ray this far ahead of centre. Increase for LONG vehicles.")]
    [SerializeField] private float sensorForwardOffset = 1.6f;
    [SerializeField] private float sensorHeight = 0.5f;
    [Tooltip("Set this to your Traffic layer only.")]
    [SerializeField] private LayerMask trafficMask = ~0;
    [SerializeField] private float acceleration = 10f;

    [Header("Hard Anti-Overlap")]
    [Tooltip("Smallest bumper-to-bumper gap allowed. Cars physically can't get closer.")]
    [SerializeField] private float minGap = 0.6f;

    [Header("Overtaking")]
    [Tooltip("Sideways distance to pull out. Positive = right (default), negative = left.")]
    [SerializeField] private float overtakeOffset = 5.3f;
    [Tooltip("Only overtake if this much faster than the car ahead.")]
    [SerializeField] private float minSpeedAdvantage = 0.5f;
    [Tooltip("Speed multiplier while passing (the 'takeoff').")]
    [SerializeField] private float overtakeBoost = 1.5f;
    [Tooltip("Extra gap before cutting back in, on top of both car lengths.")]
    [SerializeField] private float returnBuffer = 1.5f;
    [SerializeField] private float lateralSpeed = 4f;
    [Tooltip("How far the passing lane must be clear before pulling out.")]
    [SerializeField] private float overtakeClearLength = 10f;
    [Tooltip("How much clear road is needed AHEAD in the lane before cutting back in.")]
    [SerializeField] private float mergeClearLength = 10f;

    public float CruiseSpeed => cruiseSpeed;
    public float Length => myLength > 0f ? myLength : 4f;
    public float CurrentSpeed => currentSpeed;

    public void Init(Vector3 moveDirection, float moveSpeed, Vector3 center, float radius)
    {
        direction = moveDirection.normalized;
        right = Vector3.Cross(Vector3.up, direction).normalized;
        cruiseSpeed = moveSpeed;
        currentSpeed = moveSpeed;
        townCenter = center;
        despawnRadius = radius;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (myLength < 0f) MeasureLength();

        float targetSpeed = cruiseSpeed;
        float accelNow = acceleration;

        Vector3 fwdOrigin = transform.position + direction * sensorForwardOffset + Vector3.up * sensorHeight;
        SenseAhead(fwdOrigin, direction, sensorLength, out VehicleMover carAhead, out float distAhead);

        switch (state)
        {
            case LaneState.InLane:
                lateralTarget = 0f;
                if (carAhead != null)
                {
                    bool slower = carAhead.CruiseSpeed < cruiseSpeed - minSpeedAdvantage;
                    if (!hasOvertaken && slower && OvertakeLaneClear())
                    {
                        state = LaneState.Overtaking;
                        overtakeCar = carAhead;
                        overtakeTarget = carAhead.transform;
                        hasOvertaken = true;
                        lateralTarget = overtakeOffset;
                    }
                    else targetSpeed = BrakeSpeed(distAhead);
                }
                break;

            case LaneState.Overtaking:
                lateralTarget = overtakeOffset;
                targetSpeed = cruiseSpeed * overtakeBoost;
                accelNow = acceleration * 1.6f;

                if (overtakeCar == null || (PastOvertakenCar() && MergeLaneClear()))
                    state = LaneState.Returning;
                else if (carAhead != null && carAhead != overtakeCar)
                    targetSpeed = BrakeSpeed(distAhead);
                break;

            case LaneState.Returning:
                lateralTarget = 0f;
                if (!MergeLaneClear() && Mathf.Abs(lateralOffset) > 0.05f)
                {
                    state = LaneState.Overtaking;
                    lateralTarget = overtakeOffset;
                    break;
                }
                if (carAhead != null && carAhead != overtakeCar)
                    targetSpeed = BrakeSpeed(distAhead);
                if (Mathf.Abs(lateralOffset) < 0.05f)
                {
                    state = LaneState.InLane;
                    overtakeCar = null;
                    overtakeTarget = null;
                }
                break;
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelNow * dt);

        // Intended forward move this frame.
        float forwardStep = currentSpeed * dt;

        // --- HARD ANTI-OVERLAP CLAMP ---
        // Never let the front of this car get closer than minGap to whatever
        // is ahead, no matter what the braking logic decided.
        if (carAhead != null)
        {
            // distAhead is measured from a point sensorForwardOffset ahead of us,
            // so the true bumper-to-thing gap is distAhead, minus the gap we keep.
            float allowed = Mathf.Max(0f, distAhead - minGap);
            if (forwardStep > allowed)
            {
                forwardStep = allowed;
                currentSpeed = 0f; // we're pinned to the car ahead this frame
            }
        }

        float newLateral = Mathf.MoveTowards(lateralOffset, lateralTarget, lateralSpeed * dt);
        float lateralDelta = newLateral - lateralOffset;
        lateralOffset = newLateral;

        transform.position += direction * forwardStep + right * lateralDelta;

        Vector3 vel = direction * currentSpeed + right * (lateralDelta / Mathf.Max(dt, 1e-4f));
        if (vel.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(vel.normalized), 12f * dt);

        age += dt;
        Vector3 flat = transform.position - townCenter; flat.y = 0f;
        bool pastEdge = flat.magnitude > despawnRadius;
        bool headingOut = Vector3.Dot(direction, flat.normalized) > 0f;
        if ((pastEdge && headingOut) || age >= maxLifetime)
            Destroy(gameObject);
    }

    private bool PastOvertakenCar()
    {
        if (overtakeCar == null) return true;
        Vector3 toUs = transform.position - overtakeCar.transform.position;
        float along = Vector3.Dot(direction, toUs);
        float needed = myLength * 0.5f + overtakeCar.Length * 0.5f + returnBuffer;
        return along > needed;
    }

    private bool MergeLaneClear()
    {
        Vector3 laneOrigin = transform.position - right * lateralOffset
                           + direction * sensorForwardOffset + Vector3.up * sensorHeight;
        SenseAhead(laneOrigin, direction, mergeClearLength, out VehicleMover ahead, out _);
        return ahead == null;
    }

    private float BrakeSpeed(float dist)
    {
        float t = Mathf.Clamp01(dist / sensorLength);
        float s = cruiseSpeed * t;
        if (dist < sensorLength * 0.35f) s = 0f;
        return s;
    }

    private void MeasureLength()
    {
        Collider col = GetComponentInChildren<Collider>();
        if (col != null)
        {
            Bounds b = col.bounds;
            myLength = 2f * (Mathf.Abs(direction.x) * b.extents.x
                           + Mathf.Abs(direction.y) * b.extents.y
                           + Mathf.Abs(direction.z) * b.extents.z);
        }
        else myLength = 4f;
    }

    private void SenseAhead(Vector3 origin, Vector3 dir, float length,
                            out VehicleMover car, out float dist)
    {
        car = null; dist = Mathf.Infinity;
        RaycastHit[] hits = Physics.RaycastAll(origin, dir, length, trafficMask,
                                               QueryTriggerInteraction.Ignore);
        float best = Mathf.Infinity;
        foreach (RaycastHit h in hits)
        {
            VehicleMover vm = h.collider.GetComponentInParent<VehicleMover>();
            if (vm == this) continue;
            if (h.distance < best) { best = h.distance; car = vm; }
        }
        if (car != null) dist = best;
    }

    private bool OvertakeLaneClear()
    {
        Vector3 origin = transform.position + right * overtakeOffset
                       + direction * sensorForwardOffset + Vector3.up * sensorHeight;
        SenseAhead(origin, direction, overtakeClearLength, out VehicleMover fwd, out _);
        if (fwd != null) return false;
        SenseAhead(origin, -direction, sensorForwardOffset + 2f, out VehicleMover back, out _);
        if (back != null) return false;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 dir = Application.isPlaying ? direction : transform.forward;
        Vector3 o = transform.position + dir * sensorForwardOffset + Vector3.up * sensorHeight;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(o, o + dir * sensorLength);
    }
}