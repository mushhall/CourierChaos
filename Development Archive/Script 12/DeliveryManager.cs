using UnityEngine;
using System.Collections.Generic;

public class DeliveryManager : MonoBehaviour
{
    private class Order
    {
        public DeliveryZone pickup;   // where to collect
        public DeliveryZone dest;     // where to deliver
        public string item;
        public Color color;
        public bool collected;        // false = go collect, true = go deliver

        public float collectDeadline; // expire if not collected in time

        public float startTime;       // set at collection (rating clock)
        public float idealTime;
        public float distance;
        public float expireTime;      // delivery expiry (after collection)
    }

    private class Offer
    {
        public DeliveryZone pickup;
        public DeliveryZone dest;
        public float expireTime;
    }

    [Header("Orders")]
    [SerializeField] private int maxOrders = 4;

    [Header("Job Board")]
    [SerializeField] private KeyCode jobBoardKey = KeyCode.Tab;
    [Tooltip("Seconds to reach a shop and collect before an accepted job is lost.")]
    [SerializeField] private float collectTimeLimit = 45f;

    [Header("Order Offers (the live job board)")]
    [SerializeField] private int maxOffersPerPickup = 3;
    [Tooltip("Time between new orders at EACH shop (random in range). Lower = busier.")]
    [SerializeField] private float offerSpawnMin = 6f;
    [SerializeField] private float offerSpawnMax = 16f;
    [SerializeField] private float offerLifeMin = 15f;
    [SerializeField] private float offerLifeMax = 35f;
    [SerializeField] private int startingOffers = 4;

    [Header("Timing & Rating")]
    [SerializeField] private float referenceSpeed = 5f;
    [SerializeField] private float routeFactor = 1.3f;
    [SerializeField] private float minIdealTime = 3f;
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
    private readonly Dictionary<DeliveryZone, float> pickupOfferTimers = new Dictionary<DeliveryZone, float>();

    private float money;
    private float tipBonus = 1f;
    private int deliveriesCompleted;
    private int totalStars;
    private int complaints;

    // Board state.
    private bool boardOpen;
    private float prevTimeScale = 1f;
    private readonly List<Offer> boardOffers = new List<Offer>();
    private bool[] chosen;
    private Vector2 boardScroll;

    private int lastType; // 0 success, 1 fail, 2 info
    private string lastMsg = "";
    private int lastStars;
    private float lastPay;
    private float lastResultTime = -99f;
    private const float resultDuration = 3.5f;

    private Transform playerT;

    public bool HasOrder
    {
        get { Order o = NearestOrder(); return o != null && o.collected; }
    }
    public int DeliveriesCompleted => deliveriesCompleted;

    // All current stops for the beacons: pickups to collect (green) + deliveries (blue).
    public struct Stop { public Vector3 position; public bool isPickup; }
    public void GetStops(List<Stop> buffer)
    {
        buffer.Clear();
        foreach (Order o in orders)
            buffer.Add(new Stop
            {
                position = (o.collected ? o.dest : o.pickup).transform.position,
                isPickup = !o.collected
            });
    }

    public int Complaints => complaints;
    public float Money => money;

    // Spend money on an upgrade; returns false if you can't afford it.
    public bool TrySpend(float amount)
    {
        if (money < amount) return false;
        money -= amount;
        return true;
    }

    // Upgrade: permanently raise the tip multiplier on every delivery.
    public void UpgradeTipBonus(float amount) { tipBonus += amount; }
    public float TipBonus => tipBonus;

    // Upgrade: carry more orders at once.
    public void AddOrderSlot(int amount) { maxOrders += amount; }
    public int MaxOrders => maxOrders;
    public float AverageRating => deliveriesCompleted > 0 ? (float)totalStars / deliveriesCompleted : 0f;

    public Transform CurrentTarget
    {
        get
        {
            Order o = NearestOrder();
            if (o != null) return (o.collected ? o.dest : o.pickup).transform;
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

        foreach (DeliveryZone p in pickups)
            pickupOfferTimers[p] = Random.Range(offerSpawnMin, offerSpawnMax);
        for (int i = 0; i < startingOffers; i++) SpawnOfferSomewhere();
    }

    private void Update()
    {
        // Toggle the job board (only when the game is actually running).
        if (Input.GetKeyDown(jobBoardKey))
        {
            if (boardOpen) CloseBoard();
            else if (Time.timeScale != 0f) OpenBoard();
        }

        // Accepted-but-not-collected orders expire.
        for (int i = orders.Count - 1; i >= 0; i--)
        {
            Order o = orders[i];
            if (!o.collected && Time.time > o.collectDeadline)
            {
                complaints++;
                Flash(1, $"Missed pickup: {o.item}", 0, 0);
                orders.RemoveAt(i);
            }
            else if (o.collected && Time.time > o.expireTime)
            {
                complaints++;
                Flash(1, $"{o.dest.PlaceName} expired!", 0, 0);
                orders.RemoveAt(i);
            }
        }

        // Offers vanish over time.
        for (int i = availableOffers.Count - 1; i >= 0; i--)
            if (Time.time > availableOffers[i].expireTime) availableOffers.RemoveAt(i);

        // Each shop spawns its own orders (frozen while paused).
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

    private void SpawnOfferSomewhere()
    {
        if (pickups.Count == 0) return;
        List<DeliveryZone> roomy = new List<DeliveryZone>();
        foreach (DeliveryZone p in pickups)
            if (OffersFor(p).Count < maxOffersPerPickup) roomy.Add(p);
        if (roomy.Count == 0) return;
        SpawnOfferAt(roomy[Random.Range(0, roomy.Count)]);
    }

    private void SpawnOfferAt(DeliveryZone pickup)
    {
        if (pickup == null || dropoffs.Count == 0) return;
        if (OffersFor(pickup).Count >= maxOffersPerPickup) return;

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

    // Arriving at a zone: collect at a shop, deliver at a destination.
    public void PlayerArrived(DeliveryZone zone)
    {
        if (boardOpen) return;

        if (zone.Type == DeliveryZone.ZoneType.Pickup)
        {
            int collectedNow = 0;
            foreach (Order o in orders)
            {
                if (!o.collected && o.pickup == zone)
                {
                    o.collected = true;
                    o.startTime = Time.time;
                    o.distance = FlatDistance(o.pickup.transform.position, o.dest.transform.position);
                    o.idealTime = ComputeIdealTime(o.distance);
                    o.expireTime = Time.time + o.idealTime * expiryMultiplier;
                    collectedNow++;
                }
            }
            if (collectedNow > 0) Flash(0, $"Collected {collectedNow} {zone.ItemName} order(s)!", 0, 0);
            return;
        }

        // Drop-off: deliver a collected order that belongs here.
        for (int i = 0; i < orders.Count; i++)
            if (orders[i].collected && orders[i].dest == zone) { CompleteOrder(orders[i]); return; }
    }

    private void OpenBoard()
    {
        boardOffers.Clear();
        boardOffers.AddRange(availableOffers);
        chosen = new bool[boardOffers.Count];
        boardScroll = Vector2.zero;
        boardOpen = true;
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    private void CloseBoard()
    {
        boardOpen = false;
        Time.timeScale = prevTimeScale;
    }

    private void AcceptBoard()
    {
        int room = maxOrders - orders.Count;
        for (int i = 0; i < boardOffers.Count && room > 0; i++)
        {
            if (chosen[i] && availableOffers.Contains(boardOffers[i]))
            {
                Offer of = boardOffers[i];
                orders.Add(new Order
                {
                    pickup = of.pickup,
                    dest = of.dest,
                    item = of.pickup.ItemName,
                    color = of.pickup.ItemColor,
                    collected = false,
                    collectDeadline = Time.time + collectTimeLimit
                });
                availableOffers.Remove(of);
                room--;
            }
        }
        CloseBoard();
    }

    private void CompleteOrder(Order o)
    {
        float ratio = (Time.time - o.startTime) / o.idealTime;
        int stars = StarsForRatio(ratio);
        float fare = baseFare + o.distance * payPerDistance;
        float pay = fare * tipMultipliers[Mathf.Clamp(stars, 0, tipMultipliers.Length - 1)] * tipBonus;

        money += pay;
        deliveriesCompleted++;
        totalStars += stars;
        if (stars == 0) complaints++;

        Flash(0, $"Delivered to {o.dest.PlaceName}!", stars, pay);
        orders.Remove(o);
    }

    private void Flash(int type, string msg, int stars, float pay)
    {
        lastType = type; lastMsg = msg; lastStars = stars; lastPay = pay; lastResultTime = Time.time;
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

    // Nearest active order by its NEXT stop (pickup if uncollected, else dest).
    private Order NearestOrder()
    {
        if (orders.Count == 0) return null;
        if (playerT == null) return orders[0];
        Order best = null; float bd = Mathf.Infinity;
        foreach (Order o in orders)
        {
            Vector3 stop = (o.collected ? o.dest : o.pickup).transform.position;
            float d = FlatDistance(playerT.position, stop);
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
        if (best == null && pickups.Count > 0) best = pickups[0];
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
    private GUIStyle titleStyle, tagStyle, bodyStyle, statStyle, moneyStyle, menuTitle, buttonStyle, hintStyle;
    private GUIStyle panelStyle;
    private Texture2D dot, white1;
    private PlayerHealth health;

    private void EnsureGui()
    {
        if (titleStyle != null) return;
        titleStyle = MakeStyle(28, FontStyle.Bold);
        tagStyle = MakeStyle(13, FontStyle.Bold);
        bodyStyle = MakeStyle(15, FontStyle.Normal); bodyStyle.wordWrap = true;
        statStyle = MakeStyle(14, FontStyle.Normal);
        moneyStyle = MakeStyle(24, FontStyle.Bold);
        menuTitle = MakeStyle(30, FontStyle.Bold); menuTitle.alignment = TextAnchor.MiddleCenter;
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
        hintStyle = MakeStyle(14, FontStyle.Bold); hintStyle.alignment = TextAnchor.MiddleCenter;

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
        if (boardOpen) DrawBoard();
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
        GUI.Label(new Rect(x, y, w, 20), $"{availableOffers.Count} jobs  [{jobBoardKey}]",
            new GUIStyle(tagStyle) { alignment = TextAnchor.UpperRight, normal = { textColor = accentColor } });
        y += 26;

        if (orders.Count == 0)
        {
            bodyStyle.normal.textColor = new Color(0.75f, 0.78f, 0.82f);
            GUI.Label(new Rect(x, y, w, 40), $"No orders. Press [{jobBoardKey}] for the job board.", bodyStyle);
            y += 44;
        }
        else
        {
            Order next = NearestOrder();
            foreach (Order o in orders)
            {
                Color rowCol;
                string label;
                float leftFrac;

                if (!o.collected)
                {
                    rowCol = new Color(0.4f, 0.9f, 0.5f); // green: go collect
                    label = $"Collect {o.item}  >  {o.dest.PlaceName}";
                    leftFrac = Mathf.Clamp01((o.collectDeadline - Time.time) / collectTimeLimit);
                }
                else
                {
                    int stars = StarsForRatio((Time.time - o.startTime) / o.idealTime);
                    rowCol = TierColor(stars);
                    if (stars == 0)
                    {
                        float fl = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
                        rowCol = Color.Lerp(new Color(0.35f, 0, 0), new Color(1, 0.15f, 0.15f), fl);
                    }
                    label = $"Deliver  >  {o.dest.PlaceName}";
                    float life = o.expireTime - o.startTime;
                    leftFrac = Mathf.Clamp01((o.expireTime - Time.time) / life);
                }

                GUI.color = o.color;
                GUI.DrawTexture(new Rect(x, y + 2, 14, 14), dot);
                GUI.color = Color.white;
                bodyStyle.normal.textColor = (o == next) ? accentColor : Color.white;
                GUI.Label(new Rect(x + 20, y, w - 20, 20), (o == next ? "> " : "") + label, bodyStyle);
                y += 20;

                GUI.color = new Color(1, 1, 1, 0.12f);
                GUI.DrawTexture(new Rect(x + 20, y, w - 20, 5), white1);
                GUI.color = rowCol;
                GUI.DrawTexture(new Rect(x + 20, y, (w - 20) * leftFrac, 5), white1);
                GUI.color = Color.white;
                y += 12;
            }
        }

        Divider(x, y, w); y += 14;

        if (Time.time - lastResultTime < resultDuration)
        {
            Color c = lastType == 0 ? new Color(0.4f, 0.9f, 0.5f)
                    : lastType == 1 ? new Color(0.95f, 0.4f, 0.4f)
                    : new Color(0.8f, 0.82f, 0.86f);
            bodyStyle.normal.textColor = c;
            GUI.Label(new Rect(x, y, w, 40), lastMsg, bodyStyle); y += 24;
            if (lastType == 0 && lastStars >= 0 && lastPay > 0f)
            {
                statStyle.normal.textColor = new Color(0.4f, 0.9f, 0.5f);
                GUI.Label(new Rect(x, y, w, 20), $"{lastStars}-star   +${lastPay:0.00}", statStyle);
                y += 22;
            }
        }

        float sy = py + ph - pad - 46;
        Divider(x, sy - 12, w);
        statStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, sy, w, 20), $"Deliveries: {deliveriesCompleted}", statStyle);
        GUI.Label(new Rect(x, sy + 22, w, 20), $"Avg: {AverageRating:0.0}   Complaints: {complaints}", statStyle);
    }

    private void DrawBoard()
    {
        GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.78f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white1);
        GUI.color = Color.white;

        float w = 520f, h = Mathf.Min(560f, Screen.height - 80f);
        float x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;

        GUI.color = panelColor;
        GUI.Box(new Rect(x, y, w, h), GUIContent.none, panelStyle);
        GUI.color = Color.white;

        float pad = 24f, cx = x + pad, cw = w - pad * 2f, cy = y + pad;

        menuTitle.normal.textColor = accentColor;
        GUI.Label(new Rect(cx, cy, cw, 40), "JOB BOARD", menuTitle);
        cy += 46;

        int room = maxOrders - orders.Count;
        int picked = CountChosen();
        statStyle.normal.textColor = new Color(0.75f, 0.78f, 0.82f);
        statStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(cx, cy, cw, 22), $"Accept up to {room}   (selected {picked})", statStyle);
        statStyle.alignment = TextAnchor.UpperLeft;
        cy += 30;

        float listH = h - (cy - y) - 80f;
        float rowH = 52f;
        Rect view = new Rect(cx, cy, cw, listH);
        Rect content = new Rect(0, 0, cw - 20f, boardOffers.Count * rowH + 4f);
        boardScroll = GUI.BeginScrollView(view, boardScroll, content);

        if (boardOffers.Count == 0)
        {
            bodyStyle.normal.textColor = new Color(0.7f, 0.72f, 0.76f);
            GUI.Label(new Rect(0, 6, content.width, 30), "No jobs right now. Sit tight, more will come.", bodyStyle);
        }

        for (int i = 0; i < boardOffers.Count; i++)
        {
            Offer of = boardOffers[i];
            float ry = i * rowH;
            float fare = baseFare + FlatDistance(of.pickup.transform.position, of.dest.transform.position) * payPerDistance;

            bool canPick = chosen[i] || picked < room;
            GUI.enabled = canPick;
            bool nv = GUI.Toggle(new Rect(0, ry + 12, 30, 26), chosen[i], "");
            if (nv != chosen[i]) { chosen[i] = nv; picked = CountChosen(); }
            GUI.enabled = true;

            GUI.color = of.pickup.ItemColor;
            GUI.DrawTexture(new Rect(34, ry + 14, 16, 16), dot);
            GUI.color = Color.white;

            bodyStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(56, ry + 6, content.width - 56, 22), $"{of.pickup.ItemName}  >  {of.dest.PlaceName}", bodyStyle);
            statStyle.normal.textColor = new Color(0.4f, 0.9f, 0.5f);
            GUI.Label(new Rect(56, ry + 28, content.width - 56, 20), $"up to ${fare:0.00}", statStyle);
        }

        GUI.EndScrollView();

        float by = y + h - pad - 44f;

        // CLOSE on the left.
        if (GUI.Button(new Rect(cx, by, cw * 0.48f, 44), "CLOSE", buttonStyle)) CloseBoard();

        // ACCEPT on the right, tinted green.
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.25f, 0.85f, 0.4f);
        if (GUI.Button(new Rect(cx + cw * 0.52f, by, cw * 0.48f, 44), "ACCEPT", buttonStyle)) AcceptBoard();
        GUI.backgroundColor = prevBg;
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