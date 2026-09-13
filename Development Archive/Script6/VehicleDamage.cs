using UnityEngine;

public class VehicleDamage : MonoBehaviour
{
    [Tooltip("Heavier vehicles hit harder. Try ~1 for a small car, ~4+ for a lorry.")]
    [SerializeField] private float mass = 1f;

    [Tooltip("Overall damage scaling. Raise to make all crashes hurt more.")]
    [SerializeField] private float damageScale = 4f;

    [Tooltip("Minimum damage even from a slow, light vehicle.")]
    [SerializeField] private float minDamage = 5f;

    [Tooltip("Cap so nothing one-shots the player unfairly.")]
    [SerializeField] private float maxDamage = 90f;

    private VehicleMover mover;

    private void Awake()
    {
        mover = GetComponent<VehicleMover>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null) return;

        // Speed of the car at the moment of impact (falls back to cruise).
        float speed = mover != null ? Mathf.Max(mover.CurrentSpeed, 0.1f) : 5f;

        // Damage ~ momentum (mass x speed), scaled and clamped.
        float damage = mass * speed * damageScale;
        damage = Mathf.Clamp(damage, minDamage, maxDamage);

        health.TakeDamage(damage);
    }
}