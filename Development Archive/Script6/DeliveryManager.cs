using UnityEngine;
using System.Collections.Generic;

public class DeliveryManager : MonoBehaviour
{
    [Header("State (watch while playing)")]
    [SerializeField] private bool hasOrder = false;
    [SerializeField] private int deliveriesCompleted = 0;
    [SerializeField] private string carriedItem = "";
    [SerializeField] private float money = 0f;

    [Header("Timing & Rating")]
    [Tooltip("Match this to the Player's Move Speed.")]
    [SerializeField] private float referenceSpeed = 5f;
    [Tooltip("Roads aren't straight lines. ~1.3 accounts for going around blocks.")]
    [SerializeField] private float routeFactor = 1.3f;
    [SerializeField] private float minIdealTime = 3f;
    [Tooltip("Max time-ratio for each star, best first.")]
    [SerializeField]
    private float[] starThresholds =
        { 1.15f, 1.25f, 1.35f, 1.45f, 1.55f };

    [Header("Payment")]
    [Tooltip("Guaranteed pay for any delivery, before distance.")]
    [SerializeField] private float baseFare = 3f;
    [Tooltip("Extra pay per metre of delivery distance.")]
    [SerializeField] private float payPerDistance = 0.15f;
    [Tooltip("Fare multiplier for each star tier (index 0 = 0 stars ... 5 = 5 stars).")]
    [SerializeField]
    private float[] tipMultipliers = { 0.5f, 0.8f, 1.0f, 1.2f, 1.4f, 1.6f };

    [Header("HUD Panel")]
    [Tooltip("Panel width as a fraction of the screen. 0.25 is a good default.")]
    [Range(0.15f, 0.4f)]
    [SerializeField] private float panelWidthFraction = 0.25f;
    [Tooltip("Never let the panel get wider than this (for big monitors).")]
    [SerializeField] private float maxPanelWidth = 500f;
    [SerializeField] private float panelMargin = 16f;
    [Tooltip("Put the panel on the right instead of the left.")]
    [SerializeField] private bool panelOnRight = false;
    [SerializeField] private Color panelColor = new Color(0.08f, 0.09f, 0.12f, 0.92f);
    [SerializeField] private Color accentColor = new Color(0.3f, 0.75f, 1f);

    private readonly List<DeliveryZone> pickups = new List<DeliveryZone>();
    private readonly List<DeliveryZone> dropoffs = new List<DeliveryZone>();

    private DeliveryZone currentPickup;
    private DeliveryZone currentDropoff;
    private Color carriedColor = Color.white;

    private float orderStartTime;
    private float idealTime;
    private float orderDistance;
    private int totalStars;
    private int complaints;

    private int lastStars = -1;
    private string lastItem = "";
    private float lastPay = 0f;
    private float lastResultTime = -99f;
    private const float resultDuration = 3.5f;

    private PlayerHealth playerHealth;

    public bool HasOrder => hasOrder;
    public int DeliveriesCompleted => deliveriesCompleted;
    public string CarriedItem => carriedItem;
    public float Money => money;

    public int TotalStars => totalStars;
    public int Complaints => complaints;
    public float AverageRating => deliveriesCompleted > 0 ? (float)totalStars / deliveriesCompleted : 0f;

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

        playerHealth = FindFirstObjectByType<PlayerHealth>();
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
            orderDistance = FlatDistance(currentPickup, currentDropoff);
            idealTime = ComputeIdealTime(orderDistance);
        }
        else if (hasOrder && zone == currentDropoff)
        {
            float ratio = (Time.time - orderStartTime) / idealTime;
            int stars = StarsForRatio(ratio);

            // Fare = (base + distance pay) x star tip multiplier.
            float fare = baseFare + orderDistance * payPerDistance;
            float mult = tipMultipliers[Mathf.Clamp(stars, 0, tipMultipliers.Length - 1)];
            float pay = fare * mult;
            money += pay;

            deliveriesCompleted++;
            totalStars += stars;
            if (stars == 0) complaints++;

            lastStars = stars;
            lastItem = carriedItem;
            lastPay = pay;
            lastResultTime = Time.time;

            hasOrder = false;
            carriedItem = "";
            currentDropoff = null;
            currentPickup = PickRandom(pickups, currentPickup);
        }
    }

    private float FlatDistance(DeliveryZone from, DeliveryZone to)
    {
        Vector3 a = from.transform.position; a.y = 0f;
        Vector3 b = to.transform.position; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private float ComputeIdealTime(float dist)
    {
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

    private Color TierColor(int stars)
    {
        switch (stars)
        {
            case 5: return new Color(0.00f, 0.50f, 0.00f);
            case 4: return new Color(0.55f, 0.90f, 0.10f);
            case 3: return new Color(1.00f, 0.85f, 0.10f);
            case 2: return new Color(1.00f, 0.55f, 0.10f);
            case 1: return new Color(0.90f, 0.15f, 0.15f);
            default: return new Color(0.90f, 0.10f, 0.10f);
        }
    }

    // ---------------- HUD ----------------
    private GUIStyle titleStyle, tagStyle, itemStyle, bodyStyle, statStyle, colorStyle, moneyStyle;
    private GUIStyle panelStyle;
    private Texture2D dot, white1;

    private void EnsureGui()
    {
        if (titleStyle != null) return;

        titleStyle = MakeStyle(28, FontStyle.Bold);
        tagStyle = MakeStyle(13, FontStyle.Bold);
        itemStyle = MakeStyle(26, FontStyle.Bold);
        bodyStyle = MakeStyle(17, FontStyle.Normal); bodyStyle.wordWrap = true;
        statStyle = MakeStyle(16, FontStyle.Normal);
        colorStyle = MakeStyle(20, FontStyle.Bold);
        moneyStyle = MakeStyle(24, FontStyle.Bold);

        white1 = new Texture2D(1, 1);
        white1.SetPixel(0, 0, Color.white); white1.Apply();

        dot = MakeCircle(32);

        Texture2D rounded = MakeRounded(40, 14);
        panelStyle = new GUIStyle();
        panelStyle.normal.background = rounded;
        panelStyle.border = new RectOffset(14, 14, 14, 14);
    }

    private GUIStyle MakeStyle(int size, FontStyle fs)
    {
        GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs };
        s.normal.textColor = Color.white;
        return s;
    }

    private void OnGUI()
    {
        EnsureGui();

        float pw = Mathf.Min(Screen.width * panelWidthFraction, maxPanelWidth);
        float px = panelOnRight ? Screen.width - pw - panelMargin : panelMargin;
        float py = panelMargin;
        float ph = Screen.height - panelMargin * 2f;

        GUI.color = panelColor;
        GUI.Box(new Rect(px, py, pw, ph), GUIContent.none, panelStyle);
        GUI.color = Color.white;

        float pad = 18f;
        float x = px + pad, w = pw - pad * 2f, y = py + pad;

        // Header with money on the right.
        titleStyle.normal.textColor = accentColor;
        GUI.Label(new Rect(x, y, w, 34), "COURIER", titleStyle);
        moneyStyle.normal.textColor = new Color(0.4f, 0.9f, 0.5f);
        moneyStyle.alignment = TextAnchor.UpperRight;
        GUI.Label(new Rect(x, y + 2, w, 30), $"${money:0.00}", moneyStyle);
        y += 40; Divider(x, y, w); y += 14;

        // Health bar.
        if (playerHealth != null)
        {
            tagStyle.normal.textColor = new Color(0.6f, 0.65f, 0.72f);
            GUI.Label(new Rect(x, y, w, 20), "HEALTH", tagStyle);
            y += 24;

            float barH = 18f;
            GUI.color = new Color(1, 1, 1, 0.15f);
            GUI.DrawTexture(new Rect(x, y, w, barH), white1);

            float frac = playerHealth.HealthFraction;
            Color fill = Color.Lerp(new Color(0.9f, 0.15f, 0.15f),
                                    new Color(0.2f, 0.8f, 0.3f), frac);
            if (playerHealth.RecentlyHit) fill = Color.white;
            GUI.color = fill;
            GUI.DrawTexture(new Rect(x, y, w * frac, barH), white1);
            GUI.color = Color.white;

            y += barH + 6;
            Divider(x, y, w); y += 14;
        }

        // Order.
        tagStyle.normal.textColor = new Color(0.6f, 0.65f, 0.72f);
        GUI.Label(new Rect(x, y, w, 20), "CURRENT ORDER", tagStyle);
        y += 26;

        string item = hasOrder ? carriedItem
            : (currentPickup != null ? currentPickup.ItemName : "-");
        Color itemCol = hasOrder ? carriedColor
            : (currentPickup != null ? currentPickup.ItemColor : Color.white);

        GUI.color = itemCol;
        GUI.DrawTexture(new Rect(x, y + 4, 22, 22), dot);
        GUI.color = Color.white;
        itemStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 32, y, w - 32, 32), item, itemStyle);
        y += 40;

        bodyStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
        GUI.Label(new Rect(x, y, w, 44),
            hasOrder ? "Deliver to the Blue beam." : "Pick up at the Green beam.",
            bodyStyle);
        y += 48;

        // Show the fare this order is worth.
        if (hasOrder)
        {
            statStyle.normal.textColor = new Color(0.4f, 0.9f, 0.5f);
            float potential = baseFare + orderDistance * payPerDistance;
            GUI.Label(new Rect(x, y, w, 24), $"Worth up to ${potential:0.00}", statStyle);
            y += 28;
        }
        Divider(x, y, w); y += 14;

        // Speed rating.
        tagStyle.normal.textColor = new Color(0.6f, 0.65f, 0.72f);
        GUI.Label(new Rect(x, y, w, 20), "SPEED RATING", tagStyle);
        y += 26;

        if (hasOrder)
        {
            float elapsed = Time.time - orderStartTime;
            int liveStars = StarsForRatio(elapsed / idealTime);
            Color tier = TierColor(liveStars);
            if (liveStars == 0)
            {
                float f = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
                tier = Color.Lerp(new Color(0.35f, 0, 0), new Color(1f, 0.15f, 0.15f), f);
            }

            Stars(x, y, liveStars, tier); y += 32;

            float complaintAt = idealTime * starThresholds[starThresholds.Length - 1];
            float timeLeft = complaintAt - elapsed;
            colorStyle.normal.textColor = tier;
            GUI.Label(new Rect(x, y, w, 28),
                (liveStars > 0 ? $"{liveStars}-star pace" : "COMPLAINT") +
                $"   {timeLeft:0.0}s", colorStyle);
            y += 34;
        }
        else
        {
            Stars(x, y, 0, new Color(0.35f, 0.35f, 0.35f, 0.7f)); y += 32;
            bodyStyle.normal.textColor = new Color(0.7f, 0.72f, 0.76f);
            GUI.Label(new Rect(x, y, w, 26), "Waiting for pickup...", bodyStyle);
            y += 34;
        }

        // Result flash with the fare earned.
        if (Time.time - lastResultTime < resultDuration)
        {
            Divider(x, y, w); y += 14;
            Color rc = TierColor(lastStars);
            colorStyle.normal.textColor = rc;
            GUI.Label(new Rect(x, y, w, 28),
                lastStars > 0 ? $"Delivered! {lastStars}-star" : "Complaint!",
                colorStyle);
            y += 30;
            Stars(x, y, lastStars, rc); y += 30;
            statStyle.normal.textColor = new Color(0.4f, 0.9f, 0.5f);
            GUI.Label(new Rect(x, y, w, 24), $"+${lastPay:0.00}", statStyle);
        }

        // Stats pinned to the bottom.
        float sy = py + ph - pad - 82;
        Divider(x, sy - 14, w);
        float avg = deliveriesCompleted > 0 ? (float)totalStars / deliveriesCompleted : 0f;
        statStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, sy, w, 24), $"Deliveries: {deliveriesCompleted}", statStyle);
        GUI.Label(new Rect(x, sy + 26, w, 24), $"Avg rating: {avg:0.0} stars", statStyle);
        GUI.Label(new Rect(x, sy + 52, w, 24), $"Complaints: {complaints}", statStyle);
    }

    private void Stars(float x, float y, int filled, Color lit)
    {
        float size = 22f, gap = 6f;
        Color empty = new Color(0.35f, 0.35f, 0.35f, 0.7f);
        for (int i = 0; i < 5; i++)
        {
            GUI.color = i < filled ? lit : empty;
            GUI.DrawTexture(new Rect(x + i * (size + gap), y, size, size), dot);
        }
        GUI.color = Color.white;
    }

    private void Divider(float x, float y, float w)
    {
        GUI.color = new Color(1, 1, 1, 0.12f);
        GUI.DrawTexture(new Rect(x, y, w, 1.5f), white1);
        GUI.color = Color.white;
    }

    private Texture2D MakeCircle(int res)
    {
        Texture2D t = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float r = res * 0.5f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.InverseLerp(0.85f, 1f, d))));
            }
        t.Apply(); return t;
    }

    private Texture2D MakeRounded(int size, int radius)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp };
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - dist + 0.5f)));
            }
        t.Apply(); return t;
    }
}