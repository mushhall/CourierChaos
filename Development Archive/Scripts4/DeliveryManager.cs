using UnityEngine;
using System.Collections.Generic;

public class DeliveryManager : MonoBehaviour
{
    [Header("State (watch while playing)")]
    [SerializeField] private bool hasOrder = false;
    [SerializeField] private int deliveriesCompleted = 0;
    [SerializeField] private string carriedItem = "";

    [Header("Timing & Rating")]
    [Tooltip("Match this to the Player's Move Speed.")]
    [SerializeField] private float referenceSpeed = 5f;
    [Tooltip("Roads aren't straight lines. Bumps the ideal time up a bit " +
             "to account for driving around blocks. ~1.3 is a good start.")]
    [SerializeField] private float routeFactor = 1.3f;
    [Tooltip("Shortest possible ideal time, so very close deliveries stay fair.")]
    [SerializeField] private float minIdealTime = 3f;
    [Tooltip("Max time-ratio for each star, best first. " +
             "<=[0] = 5 stars ... <=[4] = 1 star ... above = complaint.")]
    [SerializeField]
    private float[] starThresholds =
        { 1.15f, 1.35f, 1.55f, 1.75f, 1.95f };

    private readonly List<DeliveryZone> pickups = new List<DeliveryZone>();
    private readonly List<DeliveryZone> dropoffs = new List<DeliveryZone>();

    private DeliveryZone currentPickup;
    private DeliveryZone currentDropoff;
    private Color carriedColor = Color.white;

    private float orderStartTime;
    private float idealTime;
    private int totalStars;
    private int complaints;

    private int lastStars = -1;
    private string lastItem = "";
    private float lastResultTime = -99f;
    private const float resultDuration = 3.5f;

    public bool HasOrder => hasOrder;
    public int DeliveriesCompleted => deliveriesCompleted;
    public string CarriedItem => carriedItem;

    public Transform CurrentTarget
    {
        get
        {
            DeliveryZone z = hasOrder ? currentDropoff : currentPickup;
            return z != null ? z.transform : null;
        }
    }

    private void Start()
    {
        DeliveryZone[] zones =
            FindObjectsByType<DeliveryZone>(FindObjectsSortMode.None);

        foreach (DeliveryZone z in zones)
        {
            if (z.Type == DeliveryZone.ZoneType.Pickup) pickups.Add(z);
            else dropoffs.Add(z);
        }

        if (pickups.Count == 0)
            Debug.LogWarning("[DeliveryManager] No pickup zones in the scene.");
        if (dropoffs.Count == 0)
            Debug.LogWarning("[DeliveryManager] No dropoff zones in the scene.");

        currentPickup = PickRandom(pickups, null);
    }

    public void PlayerArrived(DeliveryZone zone)
    {
        if (!hasOrder && zone == currentPickup)
        {
            hasOrder = true;
            carriedItem = zone.ItemName;
            carriedColor = zone.ItemColor;
            currentDropoff = PickRandom(dropoffs, null);

            orderStartTime = Time.time;
            idealTime = ComputeIdealTime(currentPickup, currentDropoff);

            Debug.Log($"Picked up {carriedItem}! Ideal time ~{idealTime:0.0}s.");
        }
        else if (hasOrder && zone == currentDropoff)
        {
            float ratio = (Time.time - orderStartTime) / idealTime;
            int stars = StarsForRatio(ratio);

            deliveriesCompleted++;
            totalStars += stars;
            if (stars == 0) complaints++;

            lastStars = stars;
            lastItem = carriedItem;
            lastResultTime = Time.time;

            Debug.Log($"Delivered {carriedItem} in {(Time.time - orderStartTime):0.0}s " +
                      $"(ratio {ratio:0.00}) -> {stars} stars.");

            hasOrder = false;
            carriedItem = "";
            currentDropoff = null;
            currentPickup = PickRandom(pickups, currentPickup);
        }
    }

    private float ComputeIdealTime(DeliveryZone from, DeliveryZone to)
    {
        Vector3 a = from.transform.position; a.y = 0f;
        Vector3 b = to.transform.position; b.y = 0f;
        float dist = Vector3.Distance(a, b);
        float t = dist / Mathf.Max(0.1f, referenceSpeed) * routeFactor;
        return Mathf.Max(t, minIdealTime);
    }

    private int StarsForRatio(float ratio)
    {
        for (int i = 0; i < starThresholds.Length; i++)
            if (ratio <= starThresholds[i]) return starThresholds.Length - i;
        return 0;
    }

    private DeliveryZone PickRandom(List<DeliveryZone> list, DeliveryZone avoid)
    {
        if (list.Count == 0) return null;
        if (list.Count == 1) return list[0];

        DeliveryZone choice;
        do { choice = list[Random.Range(0, list.Count)]; }
        while (choice == avoid);
        return choice;
    }

    // Colour for each star tier.
    private Color TierColor(int stars)
    {
        switch (stars)
        {
            case 5: return new Color(0.00f, 0.50f, 0.00f); // dark green
            case 4: return new Color(0.55f, 0.90f, 0.10f); // lime green
            case 3: return new Color(1.00f, 0.85f, 0.10f); // yellow
            case 2: return new Color(1.00f, 0.55f, 0.10f); // orange
            case 1: return new Color(0.90f, 0.15f, 0.15f); // red
            default: return new Color(0.90f, 0.10f, 0.10f); // complaint red
        }
    }

    // ---------------- On-screen UI ----------------
    private GUIStyle labelStyle;
    private GUIStyle coloredStyle;
    private Texture2D dot;

    private void EnsureGuiAssets()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold };
            labelStyle.normal.textColor = Color.white;
        }
        if (coloredStyle == null)
        {
            coloredStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold };
        }
        if (dot == null)
        {
            int res = 32;
            dot = new Texture2D(res, res, TextureFormat.RGBA32, false);
            float r = res * 0.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float dx = x - r + 0.5f, dy = y - r + 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    float a = 1f - Mathf.InverseLerp(0.85f, 1f, d);
                    dot.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(a)));
                }
            dot.Apply();
        }
    }

    private void OnGUI()
    {
        EnsureGuiAssets();

        string line1;
        if (hasOrder)
            line1 = $"Delivering: {carriedItem}";
        else if (currentPickup != null)
            line1 = $"Order: pick up {currentPickup.ItemName} at the green beam.";
        else
            line1 = "No pickups available.";
        GUI.Label(new Rect(20, 20, 900, 40), line1, labelStyle);

        float avg = deliveriesCompleted > 0 ? (float)totalStars / deliveriesCompleted : 0f;
        GUI.Label(new Rect(20, 55, 900, 40),
            $"Deliveries: {deliveriesCompleted}   Avg: {avg:0.0} stars   Complaints: {complaints}",
            labelStyle);

        if (hasOrder)
        {
            float elapsed = Time.time - orderStartTime;
            float ratio = elapsed / idealTime;
            int liveStars = StarsForRatio(ratio);

            Color tier = TierColor(liveStars);
            if (liveStars == 0)
            {
                // Flash between dark and bright red.
                float f = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
                tier = Color.Lerp(new Color(0.35f, 0f, 0f), new Color(1f, 0.15f, 0.15f), f);
            }

            DrawStars(new Vector2(24, 105), liveStars, tier);

            // Countdown to the complaint deadline. Runs into NEGATIVE past it.
            float complaintAt = idealTime * starThresholds[starThresholds.Length - 1];
            float timeLeft = complaintAt - elapsed;

            string word = liveStars > 0 ? $"{liveStars}-star pace" : "COMPLAINT";
            DrawColored(new Rect(200, 100, 600, 30),
                $"{word}   ({timeLeft:0.0}s)", tier);
        }

        if (Time.time - lastResultTime < resultDuration)
        {
            Color rc = TierColor(lastStars);
            string msg = lastStars > 0
                ? $"Delivered {lastItem}!   {lastStars}-star"
                : $"Complaint! {lastItem} was too slow.";
            DrawColored(new Rect(20, 150, 700, 30), msg, rc);
            DrawStars(new Vector2(24, 185), lastStars, rc);
        }
    }

    private void DrawStars(Vector2 pos, int filled, Color litColor)
    {
        float size = 22f, gap = 6f;
        Color empty = new Color(0.35f, 0.35f, 0.35f, 0.7f);

        Color prev = GUI.color;
        for (int i = 0; i < 5; i++)
        {
            GUI.color = i < filled ? litColor : empty;
            GUI.DrawTexture(new Rect(pos.x + i * (size + gap), pos.y, size, size), dot);
        }
        GUI.color = prev;
    }

    private void DrawColored(Rect r, string text, Color c)
    {
        coloredStyle.normal.textColor = c;
        GUI.Label(r, text, coloredStyle);
    }
}