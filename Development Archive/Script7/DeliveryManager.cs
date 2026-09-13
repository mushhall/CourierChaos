using UnityEngine;
using System.Collections.Generic;

public class DeliveryManager : MonoBehaviour
{
    // ---- One carried order ----
    private class Order
    {
        public DeliveryZone dest;
        public string item;
        public Color color;
        public float startTime;
        public float idealTime;
        public float distance;
        public float expireTime;
    }

    [Header("Orders")]
    [Tooltip("Most orders the player can carry at once.")]
    [SerializeField] private int maxOrders = 3;
    [Tooltip("How many drop-offs are offered at a pickup.")]
    [SerializeField] private int offerCount = 3;
    [Tooltip("Order expires after idealTime x this. Past it, it's lost.")]
    [SerializeField] private float expiryMultiplier = 2.2f;

    [Header("Timing & Rating")]
    [SerializeField] private float referenceSpeed = 5f;
    [SerializeField] private float routeFactor = 1.3f;
    [SerializeField] private float minIdealTime = 3f;
    [SerializeField]
    private float[] starThresholds = { 1.15f, 1.25f, 1.35f, 1.45f, 1.55f };

    [Header("Payment")]
    [SerializeField] private float baseFare = 3f;
    [SerializeField] private float payPerDistance = 0.15f;
    [SerializeField]
    private float[] tipMultipliers = { 0.5f, 0.8f, 1.0f, 1.2f, 1.4f, 1.6f };

    [Header("HUD Panel")]
    [Range(0.15f, 0.4f)][SerializeField] private float panelWidthFraction = 0.25f;
    [SerializeField] private float maxPanelWidth = 500f;
    [SerializeField] private float panelMargin = 16f;
    [SerializeField] private bool panelOnRight = false;
    [SerializeField] private Color panelColor = new Color(0.08f, 0.09f, 0.12f, 0.92f);
    [SerializeField] private Color accentColor = new Color(0.3f, 0.75f, 1f);

    private readonly List<DeliveryZone> pickups = new List<DeliveryZone>();
    private readonly List<DeliveryZone> dropoffs = new List<DeliveryZone>();
    private readonly List<Order> orders = new List<Order>();

    private float money;
    private int deliveriesCompleted;
    private int totalStars;
    private int complaints;

    // Selection menu state.
    private bool selecting;
    private DeliveryZone selectingPickup;
    private readonly List<DeliveryZone> offered = new List<DeliveryZone>();
    private bool[] chosen;

    // Result flash.
    private int lastStars = -1;
    private string lastMsg = "";
    private float lastPay;
    private bool lastFailed;
    private float lastResultTime = -99f;
    private const float resultDuration = 3.5f;

    private Transform playerT;

    public bool HasOrder => orders.Count > 0;
    public int DeliveriesCompleted => deliveriesCompleted;
    public int Complaints => complaints;
    public float Money => money;
    public float AverageRating => deliveriesCompleted > 0 ? (float)totalStars / deliveriesCompleted : 0f;

    public Transform CurrentTarget
    {
        get
        {
            if (orders.Count > 0) { Order o = NearestOrder(); return o != null ? o.dest.transform : null; }
            DeliveryZone p = NearestPickup();
            return p != null ? p.transform : null;
        }
    }

    private void Start()
    {
        DeliveryZone[] zones = FindObjectsByType<DeliveryZone>(FindObjectsSortMode.None);
        foreach (DeliveryZone z in zones)
        {
            if (z.Type == DeliveryZone.ZoneType.Pickup) pickups.Add(z);
            else dropoffs.Add(z);
        }
        PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
        if (pm != null) playerT = pm.transform;
    }

    private void Update()
    {
        // Expire overdue orders.
        for (int i = orders.Count - 1; i >= 0; i--)
        {
            if (Time.time > orders[i].expireTime)
            {
                complaints++;
                lastStars = 0; lastFailed = true;
                lastMsg = $"{orders[i].dest.PlaceName} expired!";
                lastResultTime = Time.time;
                orders.RemoveAt(i);
            }
        }
    }

    // Called by a zone when the player enters it.
    public void PlayerArrived(DeliveryZone zone)
    {
        if (selecting) return;

        if (zone.Type == DeliveryZone.ZoneType.Pickup)
        {
            if (orders.Count < maxOrders) OpenSelection(zone);
            return;
        }

        // Drop-off: complete a matching order.
        for (int i = 0; i < orders.Count; i++)
        {
            if (orders[i].dest == zone) { CompleteOrder(orders[i]); return; }
        }
    }

    private void OpenSelection(DeliveryZone pickup)
    {
        offered.Clear();
        List<DeliveryZone> pool = new List<DeliveryZone>();
        foreach (DeliveryZone d in dropoffs)
            if (!HasOrderTo(d)) pool.Add(d);

        for (int i = 0; i < offerCount && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            offered.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        if (offered.Count == 0) return;

        chosen = new bool[offered.Count];
        selectingPickup = pickup;
        selecting = true;
        Time.timeScale = 0f;
    }

    private void AcceptSelection()
    {
        int room = maxOrders - orders.Count;
        for (int i = 0; i < offered.Count && room > 0; i++)
        {
            if (chosen[i]) { AddOrder(selectingPickup, offered[i]); room--; }
        }
        CloseSelection();
    }

    private void CloseSelection()
    {
        selecting = false;
        selectingPickup = null;
        offered.Clear();
        Time.timeScale = 1f;
    }

    private void AddOrder(DeliveryZone pickup, DeliveryZone dest)
    {
        float dist = FlatDistance(pickup.transform.position, dest.transform.position);
        float ideal = ComputeIdealTime(dist);
        orders.Add(new Order
        {
            dest = dest,
            item = pickup.ItemName,
            color = pickup.ItemColor,
            startTime = Time.time,
            idealTime = ideal,
            distance = dist,
            expireTime = Time.time + ideal * expiryMultiplier
        });
    }

    private void CompleteOrder(Order o)
    {
        float ratio = (Time.time - o.startTime) / o.idealTime;
        int stars = StarsForRatio(ratio);
        float fare = baseFare + o.distance * payPerDistance;
        float pay = fare * tipMultipliers[Mathf.Clamp(stars, 0, tipMultipliers.Length - 1)];

        money += pay;
        deliveriesCompleted++;
        totalStars += stars;
        if (stars == 0) complaints++;

        lastStars = stars;
        lastFailed = false;
        lastPay = pay;
        lastMsg = $"Delivered to {o.dest.PlaceName}!";
        lastResultTime = Time.time;

        orders.Remove(o);
    }

    private bool HasOrderTo(DeliveryZone d)
    {
        foreach (Order o in orders) if (o.dest == d) return true;
        return false;
    }

    private Order NearestOrder()
    {
        if (playerT == null) return orders.Count > 0 ? orders[0] : null;
        Order best = null; float bd = Mathf.Infinity;
        foreach (Order o in orders)
        {
            float d = FlatDistance(playerT.position, o.dest.transform.position);
            if (d < bd) { bd = d; best = o; }
        }
        return best;
    }

    private DeliveryZone NearestPickup()
    {
        if (pickups.Count == 0) return null;
        if (playerT == null) return pickups[0];
        DeliveryZone best = null; float bd = Mathf.Infinity;
        foreach (DeliveryZone p in pickups)
        {
            float d = FlatDistance(playerT.position, p.transform.position);
            if (d < bd) { bd = d; best = p; }
        }
        return best;
    }

    private float FlatDistance(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    private float ComputeIdealTime(float dist)
    {
        return Mathf.Max(dist / Mathf.Max(0.1f, referenceSpeed) * routeFactor, minIdealTime);
    }

    private int StarsForRatio(float ratio)
    {
        for (int i = 0; i < starThresholds.Length; i++)
            if (ratio <= starThresholds[i]) return starThresholds.Length - i;
        return 0;
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
    private GUIStyle titleStyle, tagStyle, bodyStyle, statStyle, moneyStyle, menuTitle, buttonStyle;
    private GUIStyle panelStyle;
    private Texture2D dot, white1;
    private PlayerHealth health;

    private void EnsureGui()
    {
        if (titleStyle != null) return;
        titleStyle = MakeStyle(28, FontStyle.Bold);
        tagStyle = MakeStyle(13, FontStyle.Bold);
        bodyStyle = MakeStyle(16, FontStyle.Normal); bodyStyle.wordWrap = true;
        statStyle = MakeStyle(16, FontStyle.Normal);
        moneyStyle = MakeStyle(24, FontStyle.Bold);
        menuTitle = MakeStyle(30, FontStyle.Bold); menuTitle.alignment = TextAnchor.MiddleCenter;
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };

        white1 = new Texture2D(1, 1); white1.SetPixel(0, 0, Color.white); white1.Apply();
        dot = MakeCircle(32);

        Texture2D rounded = MakeRounded(40, 14);
        panelStyle = new GUIStyle();
        panelStyle.normal.background = rounded;
        panelStyle.border = new RectOffset(14, 14, 14, 14);

        health = FindFirstObjectByType<PlayerHealth>();
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
        DrawPanel();
        if (selecting) DrawSelectionMenu();
    }

    private void DrawPanel()
    {
        float pw = Mathf.Min(Screen.width * panelWidthFraction, maxPanelWidth);
        float px = panelOnRight ? Screen.width - pw - panelMargin : panelMargin;
        float py = panelMargin, ph = Screen.height - panelMargin * 2f;

        GUI.color = panelColor;
        GUI.Box(new Rect(px, py, pw, ph), GUIContent.none, panelStyle);
        GUI.color = Color.white;

        float pad = 18f, x = px + pad, w = pw - pad * 2f, y = py + pad;

        titleStyle.normal.textColor = accentColor;
        GUI.Label(new Rect(x, y, w, 34), "COURIER", titleStyle);
        moneyStyle.normal.textColor = new Color(0.4f, 0.9f, 0.5f);
        moneyStyle.alignment = TextAnchor.UpperRight;
        GUI.Label(new Rect(x, y + 2, w, 30), $"${money:0.00}", moneyStyle);
        y += 40; Divider(x, y, w); y += 14;

        if (health != null)
        {
            tagStyle.normal.textColor = new Color(0.6f, 0.65f, 0.72f);
            GUI.Label(new Rect(x, y, w, 20), "HEALTH", tagStyle); y += 24;
            GUI.color = new Color(1, 1, 1, 0.15f);
            GUI.DrawTexture(new Rect(x, y, w, 18), white1);
            float f = health.HealthFraction;
            Color hc = Color.Lerp(new Color(0.9f, 0.15f, 0.15f), new Color(0.2f, 0.8f, 0.3f), f);
            if (health.RecentlyHit) hc = Color.white;
            GUI.color = hc; GUI.DrawTexture(new Rect(x, y, w * f, 18), white1);
            GUI.color = Color.white; y += 24; Divider(x, y, w); y += 14;
        }

        // Orders list.
        tagStyle.normal.textColor = new Color(0.6f, 0.65f, 0.72f);
        GUI.Label(new Rect(x, y, w, 20), $"ORDERS  {orders.Count}/{maxOrders}", tagStyle);
        y += 26;

        if (orders.Count == 0)
        {
            bodyStyle.normal.textColor = new Color(0.75f, 0.78f, 0.82f);
            GUI.Label(new Rect(x, y, w, 40), "No orders. Head to the green beam (a shop).", bodyStyle);
            y += 44;
        }
        else
        {
            Order next = NearestOrder();
            foreach (Order o in orders)
            {
                float elapsed = Time.time - o.startTime;
                int stars = StarsForRatio(elapsed / o.idealTime);
                Color tc = TierColor(stars);
                if (stars == 0)
                {
                    float fl = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
                    tc = Color.Lerp(new Color(0.35f, 0, 0), new Color(1, 0.15f, 0.15f), fl);
                }

                GUI.color = o.color;
                GUI.DrawTexture(new Rect(x, y + 2, 16, 16), dot);
                GUI.color = Color.white;

                bodyStyle.normal.textColor = (o == next) ? accentColor : Color.white;
                string prefix = (o == next) ? "> " : "";
                GUI.Label(new Rect(x + 22, y, w - 22, 22), prefix + o.dest.PlaceName, bodyStyle);
                y += 22;

                // Time-left bar.
                float life = o.expireTime - o.startTime;
                float left = Mathf.Clamp01((o.expireTime - Time.time) / life);
                GUI.color = new Color(1, 1, 1, 0.12f);
                GUI.DrawTexture(new Rect(x + 22, y, w - 22, 6), white1);
                GUI.color = tc;
                GUI.DrawTexture(new Rect(x + 22, y, (w - 22) * left, 6), white1);
                GUI.color = Color.white;
                y += 14;
            }
        }

        Divider(x, y, w); y += 14;

        if (Time.time - lastResultTime < resultDuration)
        {
            bodyStyle.normal.textColor = lastFailed ? new Color(0.95f, 0.4f, 0.4f) : new Color(0.4f, 0.9f, 0.5f);
            GUI.Label(new Rect(x, y, w, 40), lastMsg, bodyStyle); y += 26;
            if (!lastFailed)
            {
                statStyle.normal.textColor = new Color(0.4f, 0.9f, 0.5f);
                GUI.Label(new Rect(x, y, w, 22), $"{lastStars}-star   +${lastPay:0.00}", statStyle);
                y += 24;
            }
        }

        // Stats bottom.
        float sy = py + ph - pad - 60;
        Divider(x, sy - 14, w);
        statStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, sy, w, 22), $"Deliveries: {deliveriesCompleted}", statStyle);
        GUI.Label(new Rect(x, sy + 24, w, 22), $"Avg: {AverageRating:0.0}   Complaints: {complaints}", statStyle);
    }

    private void DrawSelectionMenu()
    {
        GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white1);
        GUI.color = Color.white;

        float w = 460f, rowH = 46f;
        int rows = offered.Count;
        float h = 150f + rows * rowH + 70f;
        float x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;

        GUI.color = panelColor;
        GUI.Box(new Rect(x, y, w, h), GUIContent.none, panelStyle);
        GUI.color = Color.white;

        float pad = 24f, cx = x + pad, cw = w - pad * 2f, cy = y + pad;

        menuTitle.normal.textColor = accentColor;
        GUI.Label(new Rect(cx, cy, cw, 40), $"{selectingPickup.ItemName} orders", menuTitle);
        cy += 46;

        int room = maxOrders - orders.Count;
        int picked = CountChosen();
        statStyle.normal.textColor = new Color(0.75f, 0.78f, 0.82f);
        statStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(cx, cy, cw, 24), $"Pick up to {room}   (selected {picked})", statStyle);
        statStyle.alignment = TextAnchor.UpperLeft;
        cy += 34;

        for (int i = 0; i < offered.Count; i++)
        {
            DeliveryZone d = offered[i];
            float fare = baseFare + FlatDistance(selectingPickup.transform.position, d.transform.position) * payPerDistance;

            Rect row = new Rect(cx, cy, cw, rowH - 8f);
            // Toggle (respect capacity).
            bool canPick = chosen[i] || picked < room;
            GUI.enabled = canPick;
            bool newVal = GUI.Toggle(new Rect(cx, cy + 8, 30, 26), chosen[i], "");
            if (newVal != chosen[i]) { chosen[i] = newVal; picked = CountChosen(); }
            GUI.enabled = true;

            bodyStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(cx + 34, cy, cw - 34, 24), d.PlaceName, bodyStyle);
            statStyle.normal.textColor = new Color(0.4f, 0.9f, 0.5f);
            GUI.Label(new Rect(cx + 34, cy + 22, cw - 34, 20), $"up to ${fare:0.00}", statStyle);
            cy += rowH;
        }

        cy += 8;
        if (GUI.Button(new Rect(cx, cy, cw * 0.48f, 44), "ACCEPT", buttonStyle)) AcceptSelection();
        if (GUI.Button(new Rect(cx + cw * 0.52f, cy, cw * 0.48f, 44), "SKIP", buttonStyle)) CloseSelection();
    }

    private int CountChosen()
    {
        int n = 0; if (chosen != null) foreach (bool b in chosen) if (b) n++; return n;
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
        for (int yy = 0; yy < res; yy++)
            for (int xx = 0; xx < res; xx++)
            {
                float dx = xx - r + 0.5f, dy = yy - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                t.SetPixel(xx, yy, new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.InverseLerp(0.85f, 1f, d))));
            }
        t.Apply(); return t;
    }

    private Texture2D MakeRounded(int size, int radius)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float half = size / 2f;
        for (int yy = 0; yy < size; yy++)
            for (int xx = 0; xx < size; xx++)
            {
                float dx = Mathf.Max(Mathf.Abs(xx + 0.5f - half) - (half - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(yy + 0.5f - half) - (half - radius), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                t.SetPixel(xx, yy, new Color(1, 1, 1, Mathf.Clamp01(radius - dist + 0.5f)));
            }
        t.Apply(); return t;
    }
}