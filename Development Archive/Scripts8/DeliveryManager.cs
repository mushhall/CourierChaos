using UnityEngine;
using System.Collections.Generic;

public class DeliveryManager : MonoBehaviour
{
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

    private class Offer
    {
        public DeliveryZone pickup;
        public DeliveryZone dest;
        public float expireTime;
    }

    [Header("Orders")]
    [SerializeField] private int maxOrders = 3;

    [Header("Order Offers (the live job board)")]
    [Tooltip("Most offers a single pickup can hold at once.")]
    [SerializeField] private int maxOffersPerPickup = 3;
    [Tooltip("Time between new orders at EACH shop (random in range). Lower = busier.")]
    [SerializeField] private float offerSpawnMin = 6f;
    [SerializeField] private float offerSpawnMax = 16f;
    [Tooltip("How long an offer stays available before it vanishes (random).")]
    [SerializeField] private float offerLifeMin = 12f;
    [SerializeField] private float offerLifeMax = 30f;
    [Tooltip("How many offers to seed when the game starts.")]
    [SerializeField] private int startingOffers = 3;

    [Header("Timing & Rating")]
    [SerializeField] private float referenceSpeed = 5f;
    [SerializeField] private float routeFactor = 1.3f;
    [SerializeField] private float minIdealTime = 3f;
    [Tooltip("A carried order expires after idealTime x this.")]
    [SerializeField] private float expiryMultiplier = 2.2f;
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
    private readonly List<Offer> availableOffers = new List<Offer>();

    private float money;
    private int deliveriesCompleted;
    private int totalStars;
    private int complaints;
    private readonly Dictionary<DeliveryZone, float> pickupOfferTimers = new Dictionary<DeliveryZone, float>();

    private bool selecting;
    private DeliveryZone selectingPickup;
    private readonly List<Offer> menuOffers = new List<Offer>();
    private bool[] chosen;

    private int lastType; // 0 success, 1 fail, 2 info
    private string lastMsg = "";
    private int lastStars;
    private float lastPay;
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
            DeliveryZone p = NearestPickupWithOffers();
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

        // Each shop generates its own orders on its own timer.
        foreach (DeliveryZone p in pickups)
            pickupOfferTimers[p] = Random.Range(offerSpawnMin, offerSpawnMax);

        for (int i = 0; i < startingOffers; i++) SpawnOfferSomewhere();
    }

    private void Update()
    {
        // Carried orders expire.
        for (int i = orders.Count - 1; i >= 0; i--)
        {
            if (Time.time > orders[i].expireTime)
            {
                complaints++;
                Flash(1, $"{orders[i].dest.PlaceName} expired!", 0, 0);
                orders.RemoveAt(i);
            }
        }

        // Available offers vanish over time.
        for (int i = availableOffers.Count - 1; i >= 0; i--)
            if (Time.time > availableOffers[i].expireTime) availableOffers.RemoveAt(i);

        // Each shop spawns its own orders over time (frozen while paused).
        foreach (DeliveryZone p in pickups)
        {
            if (!pickupOfferTimers.ContainsKey(p))
                pickupOfferTimers[p] = Random.Range(offerSpawnMin, offerSpawnMax);

            pickupOfferTimers[p] -= Time.deltaTime;
            if (pickupOfferTimers[p] <= 0f)
            {
                SpawnOfferAt(p);
                pickupOfferTimers[p] = Random.Range(offerSpawnMin, offerSpawnMax);
            }
        }
    }

    // Seed: put an order at a random shop that has room.
    private void SpawnOfferSomewhere()
    {
        if (pickups.Count == 0) return;
        List<DeliveryZone> roomy = new List<DeliveryZone>();
        foreach (DeliveryZone p in pickups)
            if (OffersFor(p).Count < maxOffersPerPickup) roomy.Add(p);
        if (roomy.Count == 0) return;
        SpawnOfferAt(roomy[Random.Range(0, roomy.Count)]);
    }

    // Add one order at a specific shop (if it has room and a free destination).
    private void SpawnOfferAt(DeliveryZone pickup)
    {
        if (pickup == null || dropoffs.Count == 0) return;
        if (OffersFor(pickup).Count >= maxOffersPerPickup) return;

        // Destinations not already offered here and not already an active order.
        List<DeliveryZone> pool = new List<DeliveryZone>();
        foreach (DeliveryZone d in dropoffs)
            if (!OfferedAt(pickup, d) && !HasOrderTo(d)) pool.Add(d);
        if (pool.Count == 0) return;

        DeliveryZone dest = pool[Random.Range(0, pool.Count)];
        availableOffers.Add(new Offer
        {
            pickup = pickup,
            dest = dest,
            expireTime = Time.time + Random.Range(offerLifeMin, offerLifeMax)
        });
    }

    public void PlayerArrived(DeliveryZone zone)
    {
        if (selecting) return;

        if (zone.Type == DeliveryZone.ZoneType.Pickup)
        {
            if (orders.Count >= maxOrders) { Flash(2, "Your bag is full!", 0, 0); return; }
            List<Offer> here = OffersFor(zone);
            if (here.Count == 0) { Flash(2, "No orders here right now.", 0, 0); return; }
            OpenSelection(zone, here);
            return;
        }

        for (int i = 0; i < orders.Count; i++)
            if (orders[i].dest == zone) { CompleteOrder(orders[i]); return; }
    }

    private void OpenSelection(DeliveryZone pickup, List<Offer> here)
    {
        menuOffers.Clear();
        menuOffers.AddRange(here);
        chosen = new bool[menuOffers.Count];
        selectingPickup = pickup;
        selecting = true;
        Time.timeScale = 0f;
    }

    private void AcceptSelection()
    {
        int room = maxOrders - orders.Count;
        for (int i = 0; i < menuOffers.Count && room > 0; i++)
        {
            if (chosen[i])
            {
                AddOrder(menuOffers[i].pickup, menuOffers[i].dest);
                availableOffers.Remove(menuOffers[i]);
                room--;
            }
        }
        CloseSelection();
    }

    private void CloseSelection()
    {
        selecting = false;
        selectingPickup = null;
        menuOffers.Clear();
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

        Flash(0, $"Delivered to {o.dest.PlaceName}!", stars, pay);
        orders.Remove(o);
    }

    private void Flash(int type, string msg, int stars, float pay)
    {
        lastType = type; lastMsg = msg; lastStars = stars; lastPay = pay;
        lastResultTime = Time.time;
    }

    private List<Offer> OffersFor(DeliveryZone pickup)
    {
        List<Offer> list = new List<Offer>();
        foreach (Offer o in availableOffers) if (o.pickup == pickup) list.Add(o);
        return list;
    }

    private bool OfferedAt(DeliveryZone pickup, DeliveryZone dest)
    {
        foreach (Offer o in availableOffers) if (o.pickup == pickup && o.dest == dest) return true;
        return false;
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

    private DeliveryZone NearestPickupWithOffers()
    {
        DeliveryZone best = null; float bd = Mathf.Infinity;
        foreach (DeliveryZone p in pickups)
        {
            if (OffersFor(p).Count == 0) continue;
            float d = playerT != null ? FlatDistance(playerT.position, p.transform.position) : 0f;
            if (d < bd) { bd = d; best = p; }
        }
        if (best == null && pickups.Count > 0) best = pickups[0]; // fallback
        return best;
    }

    private float FlatDistance(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    private float ComputeIdealTime(float dist) => Mathf.Max(dist / Mathf.Max(0.1f, referenceSpeed) * routeFactor, minIdealTime);

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
        statStyle = MakeStyle(15, FontStyle.Normal);
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

        tagStyle.normal.textColor = new Color(0.6f, 0.65f, 0.72f);
        GUI.Label(new Rect(x, y, w, 20), $"ORDERS  {orders.Count}/{maxOrders}", tagStyle);
        y += 26;

        if (orders.Count == 0)
        {
            bodyStyle.normal.textColor = new Color(0.75f, 0.78f, 0.82f);
            GUI.Label(new Rect(x, y, w, 40), "No orders. Find a shop with jobs (green beam).", bodyStyle);
            y += 44;
        }
        else
        {
            Order next = NearestOrder();
            foreach (Order o in orders)
            {
                int stars = StarsForRatio((Time.time - o.startTime) / o.idealTime);
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
                GUI.Label(new Rect(x + 22, y, w - 22, 22), (o == next ? "> " : "") + o.dest.PlaceName, bodyStyle);
                y += 22;

                float life = o.expireTime - o.startTime;
                float leftFrac = Mathf.Clamp01((o.expireTime - Time.time) / life);
                GUI.color = new Color(1, 1, 1, 0.12f);
                GUI.DrawTexture(new Rect(x + 22, y, w - 22, 6), white1);
                GUI.color = tc;
                GUI.DrawTexture(new Rect(x + 22, y, (w - 22) * leftFrac, 6), white1);
                GUI.color = Color.white;
                y += 14;
            }
        }

        Divider(x, y, w); y += 14;

        if (Time.time - lastResultTime < resultDuration)
        {
            Color c = lastType == 0 ? new Color(0.4f, 0.9f, 0.5f)
                    : lastType == 1 ? new Color(0.95f, 0.4f, 0.4f)
                    : new Color(0.8f, 0.82f, 0.86f);
            bodyStyle.normal.textColor = c;
            GUI.Label(new Rect(x, y, w, 40), lastMsg, bodyStyle); y += 26;
            if (lastType == 0)
            {
                statStyle.normal.textColor = new Color(0.4f, 0.9f, 0.5f);
                GUI.Label(new Rect(x, y, w, 22), $"{lastStars}-star   +${lastPay:0.00}", statStyle);
                y += 24;
            }
        }

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
        float h = 150f + menuOffers.Count * rowH + 70f;
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

        for (int i = 0; i < menuOffers.Count; i++)
        {
            DeliveryZone d = menuOffers[i].dest;
            float fare = baseFare + FlatDistance(selectingPickup.transform.position, d.transform.position) * payPerDistance;

            bool canPick = chosen[i] || picked < room;
            GUI.enabled = canPick;
            bool nv = GUI.Toggle(new Rect(cx, cy + 8, 30, 26), chosen[i], "");
            if (nv != chosen[i]) { chosen[i] = nv; picked = CountChosen(); }
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