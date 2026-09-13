using UnityEngine;

public class VehicleDamage : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("Heavier vehicles hit harder. ~1 small car, ~4+ lorry.")]
    [SerializeField] private float mass = 1f;
    [SerializeField] private float damageScale = 4f;
    [SerializeField] private float minDamage = 5f;
    [SerializeField] private float maxDamage = 60f;

    [Header("Knockback (physics-style: mass x speed)")]
    [Tooltip("Overall strength of the knockback. Raise for bigger launches all round.")]
    [SerializeField] private float knockbackScale = 2.5f;
    [Tooltip("How much of the launch goes upward vs along the ground (0-1).")]
    [Range(0f, 1f)][SerializeField] private float upwardBias = 0.35f;
    [Tooltip("Clamp so nothing sends you into orbit.")]
    [SerializeField] private float minKnockback = 4f;
    [SerializeField] private float maxKnockback = 45f;
    [Tooltip("How long the player is stunned (no control) after the hit.")]
    [SerializeField] private float stunDuration = 0.6f;

    private VehicleMover mover;

    private void Awake()
    {
        mover = GetComponent<VehicleMover>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        float speed = mover != null ? Mathf.Max(mover.CurrentSpeed, 0.1f) : 5f;

        // Damage ~ mass x speed.
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            float damage = Mathf.Clamp(mass * speed * damageScale, minDamage, maxDamage);
            health.TakeDamage(damage);
        }

        // Knockback ~ MOMENTUM (mass x speed). Heavy + fast = big launch.
        PlayerMovement pm = other.GetComponentInParent<PlayerMovement>();
        if (pm != null)
        {
            float momentum = mass * speed;
            float force = Mathf.Clamp(momentum * knockbackScale, minKnockback, maxKnockback);

            // Split the total force into horizontal push and upward pop.
            float upForce = force * upwardBias;
            float horizForce = force * (1f - upwardBias);

            Vector3 dir = mover != null ? mover.Direction
                                        : (other.transform.position - transform.position);
            pm.ApplyKnockback(dir, horizForce, upForce, stunDuration);
        }
    }
}