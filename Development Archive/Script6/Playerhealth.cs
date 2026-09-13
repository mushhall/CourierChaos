using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Tooltip("Seconds of invulnerability after a hit, so one car isn't dozens of hits.")]
    [SerializeField] private float invulnerabilityTime = 1f;

    private float lastHitTime = -99f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float HealthFraction => Mathf.Clamp01(currentHealth / maxHealth);
    public bool IsDead => currentHealth <= 0f;
    // True briefly after a hit (the health bar can flash while this is on).
    public bool RecentlyHit => Time.time - lastHitTime < invulnerabilityTime;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    // Called by anything that hurts the player (e.g. a car).
    public void TakeDamage(float amount)
    {
        // Ignore hits during the brief window after being hit.
        if (Time.time - lastHitTime < invulnerabilityTime) return;
        if (IsDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        lastHitTime = Time.time;

        Debug.Log($"Hit! -{amount} health. Now {currentHealth}/{maxHealth}.");

        if (IsDead)
        {
            Debug.Log("Player is out of health!");
            // Hook game-over logic here later.
        }
    }

    // Optional: heal (e.g. a repair pickup later).
    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }
}