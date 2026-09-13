using UnityEngine;

public class SpeedPowerUp : MonoBehaviour
{
    [Header("Boost")]
    [Tooltip("Speed multiplier while active (1.6 = 60% faster).")]
    [SerializeField] private float speedMultiplier = 1.6f;
    [Tooltip("How long the boost lasts, in seconds.")]
    [SerializeField] private float boostDuration = 10f;

    [Header("Pickup")]
    [Tooltip("How close the player must get to collect it.")]
    [SerializeField] private float pickupRadius = 1.8f;
    [Tooltip("Seconds before it comes back after being grabbed.")]
    [SerializeField] private float respawnDelay = 15f;

    [Header("Visual (a parent holding the capsule + SPEED text)")]
    [Tooltip("The whole visual group to show/hide. Auto-uses first child if empty.")]
    [SerializeField] private Transform visual;
    [Tooltip("Spins only this part (usually the capsule). Optional.")]
    [SerializeField] private Transform spinPart;
    [SerializeField] private float spinSpeed = 120f;
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobSpeed = 2f;

    private PlayerMovement player;
    private bool available = true;
    private float respawnAt;
    private Vector3 visualBaseLocalPos;

    private void Start()
    {
        player = FindFirstObjectByType<PlayerMovement>();

        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);
        if (visual != null)
            visualBaseLocalPos = visual.localPosition;
    }

    private void Update()
    {
        if (!available)
        {
            if (Time.time >= respawnAt) SetAvailable(true);
            return;
        }

        // Bob the whole group up and down.
        if (visual != null)
        {
            Vector3 p = visualBaseLocalPos;
            p.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            visual.localPosition = p;
        }

        // Spin only the designated part (so the SPEED text doesn't spin away).
        if (spinPart != null)
            spinPart.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        if (player == null) return;
        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = player.transform.position; b.y = 0f;
        if (Vector3.Distance(a, b) <= pickupRadius)
        {
            player.ApplySpeedBoost(speedMultiplier, boostDuration);
            SetAvailable(false);
        }
    }

    private void SetAvailable(bool on)
    {
        available = on;
        if (!on) respawnAt = Time.time + respawnDelay;
        // Hides/shows the ENTIRE visual group (capsule + text).
        if (visual != null) visual.gameObject.SetActive(on);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}