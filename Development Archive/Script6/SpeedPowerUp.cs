using UnityEngine;

public class SpeedPowerUp : MonoBehaviour
{
    [Header("Boost")]
    [Tooltip("Speed multiplier while active (1.6 = 60% faster).")]
    [SerializeField] private float speedMultiplier = 1.6f;
    [Tooltip("How long the boost lasts, in seconds.")]
    [SerializeField] private float boostDuration = 5f;

    [Header("Pickup")]
    [Tooltip("How close the player must get to collect it.")]
    [SerializeField] private float pickupRadius = 1.8f;
    [Tooltip("Seconds before it comes back after being grabbed.")]
    [SerializeField] private float respawnDelay = 15f;

    [Header("Visual (child object to show/hide, spin & bob)")]
    [Tooltip("The visible model. Auto-uses the first child if left empty.")]
    [SerializeField] private Transform visual;
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
        // Waiting to respawn.
        if (!available)
        {
            if (Time.time >= respawnAt) SetAvailable(true);
            return;
        }

        // Spin + bob the visual so it reads as a pickup.
        if (visual != null)
        {
            visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            Vector3 p = visualBaseLocalPos;
            p.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            visual.localPosition = p;
        }

        // Collect on proximity (flat distance, ignore height).
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
        if (visual != null) visual.gameObject.SetActive(on);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}