using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    private enum Phase { Start, Playing, Shop, GameOver }
    private Phase phase = Phase.Start;

    [Header("Day")]
    [SerializeField] private float dayLength = 180f;

    [Header("Difficulty")]
    [Tooltip("Cars get this much faster each day. 0.15 = +15% per day.")]
    [SerializeField] private float speedIncreasePerDay = 0.15f;

    [Header("Scoring")]
    [Tooltip("Points removed per complaint at the end.")]
    [SerializeField] private float complaintPenalty = 100f;

    [Header("Upgrade amounts per level")]
    [SerializeField] private float healthPerLevel = 25f;
    [SerializeField] private float speedPerLevel = 0.5f;
    [SerializeField] private float tipPerLevel = 0.15f;
    [SerializeField] private int slotsPerLevel = 1;

    [Header("Upgrade base costs")]
    [SerializeField] private float healthBaseCost = 40f;
    [SerializeField] private float speedBaseCost = 60f;
    [SerializeField] private float tipBaseCost = 80f;
    [SerializeField] private float slotBaseCost = 100f;
    [SerializeField] private float costGrowth = 1.3f;
    [SerializeField] private int maxLevel = 8;
    [SerializeField] private int maxSlotLevel = 6;

    private PlayerHealth health;
    private PlayerMovement movement;
    private DeliveryManager delivery;

    private int day = 0;
    private float dayTimer;
    private float moneyAtDayStart;

    private readonly int[] levels = new int[4];

    // Scoring / high score state.
    private int finalScore;
    private bool enteringName;
    private string playerName = "";
    private bool viewingScores;   // title-screen board view

    private GUIStyle big, mid, small, button, timerStyle, field;
    private Texture2D veil, panel;

    private void Start()
    {
        health = FindFirstObjectByType<PlayerHealth>();
        movement = FindFirstObjectByType<PlayerMovement>();
        delivery = FindFirstObjectByType<DeliveryManager>();
        Time.timeScale = 0f;
    }

    private void Update()
    {
        if (phase != Phase.Playing) return;

        if (health != null && health.IsDead)
        {
            phase = Phase.GameOver;
            Time.timeScale = 0f;
            ComputeScore();
            return;
        }

        dayTimer -= Time.deltaTime;
        if (dayTimer <= 0f)
        {
            dayTimer = 0f;
            phase = Phase.Shop;
            Time.timeScale = 0f;
        }
    }

    private void ComputeScore()
    {
        float earned = delivery != null ? delivery.TotalEarned : 0f;
        float avg = delivery != null ? delivery.AverageRating : 0f;   // 0..5
        int complaints = delivery != null ? delivery.Complaints : 0;

        float mult = 1f + avg / 5f;   // 5-star avg = x2
        finalScore = Mathf.Max(0, Mathf.RoundToInt(earned * mult - complaints * complaintPenalty));

        enteringName = HighScoreTable.Qualifies(finalScore);
        playerName = "";
    }

    private void BeginDay()
    {
        day++;
        dayTimer = dayLength;
        moneyAtDayStart = delivery != null ? delivery.Money : 0f;
        if (health != null) health.FullHeal();
        VehicleSpawner.SpeedMultiplier = 1f + (day - 1) * speedIncreasePerDay;
        phase = Phase.Playing;
        Time.timeScale = 1f;
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private int CapFor(int i) => i == 3 ? maxSlotLevel : maxLevel;

    private float CostFor(int i)
    {
        float baseCost = i == 0 ? healthBaseCost : i == 1 ? speedBaseCost
                       : i == 2 ? tipBaseCost : slotBaseCost;
        return Mathf.Round(baseCost * Mathf.Pow(costGrowth, levels[i]));
    }

    private void Buy(int i)
    {
        if (levels[i] >= CapFor(i)) return;
        float cost = CostFor(i);
        if (delivery == null || !delivery.TrySpend(cost)) return;

        levels[i]++;
        if (i == 0 && health != null) health.AddMaxHealth(healthPerLevel);
        else if (i == 1 && movement != null) movement.AddMoveSpeed(speedPerLevel);
        else if (i == 2 && delivery != null) delivery.UpgradeTipBonus(tipPerLevel);
        else if (i == 3 && delivery != null) delivery.AddOrderSlot(slotsPerLevel);
    }

    // ---------------- UI ----------------
    private void OnGUI()
    {
        EnsureStyles();
        if (phase == Phase.Playing) DrawDayTimer();
        else if (phase == Phase.Start) DrawStart();
        else if (phase == Phase.Shop) DrawShop();
        else if (phase == Phase.GameOver) DrawGameOver();
    }

    private void DrawDayTimer()
    {
        int mins = Mathf.FloorToInt(dayTimer / 60f);
        int secs = Mathf.FloorToInt(dayTimer % 60f);
        timerStyle.normal.textColor = dayTimer < 30f ? new Color(1f, 0.4f, 0.4f) : Color.white;
        GUI.Label(new Rect(Screen.width * 0.5f - 120f, 12f, 240f, 40f),
            $"DAY {day}    {mins}:{secs:00}", timerStyle);
    }

    private void DrawStart()
    {
        Veil(0.75f);
        float cx = Screen.width * 0.5f;

        if (viewingScores)
        {
            float sy = Screen.height * 0.22f;
            sy = DrawBoard(sy);
            sy += 20;
            if (Button(cx - 120, sy, 240, 50, "BACK")) viewingScores = false;
            return;
        }

        float y = Screen.height * 0.18f;
        Centered(y, 600, "COURIER", big, new Color(0.3f, 0.75f, 1f)); y += 80;
        Centered(y, 900, "Work the day, bank the cash, buy upgrades. Don't crash.", mid, Color.white); y += 56;
        Centered(y, 900, "WASD / Arrows  \u2014  Drive", small, new Color(0.85f, 0.88f, 0.92f)); y += 28;
        Centered(y, 900, "Tab  \u2014  Job board     M  \u2014  Radio", small, new Color(0.85f, 0.88f, 0.92f)); y += 28;
        Centered(y, 900, "Green beam = collect     Blue beam = deliver", small, new Color(0.85f, 0.88f, 0.92f)); y += 28;
        Centered(y, 900, "Watch for traffic \u2014 getting hit hurts!", small, new Color(1f, 0.7f, 0.4f)); y += 46;

        if (Button(cx - 120, y, 240, 54, "START DAY 1")) BeginDay(); y += 62;
        if (Button(cx - 120, y, 240, 44, "HIGH SCORES")) viewingScores = true;
    }

    private void DrawGameOver()
    {
        Veil(0.85f);
        float cx = Screen.width * 0.5f;
        float y = Screen.height * 0.12f;

        Centered(y, 600, "YOU CRASHED OUT", big, new Color(0.95f, 0.3f, 0.3f)); y += 70;

        float earned = delivery != null ? delivery.TotalEarned : 0f;
        float avg = delivery != null ? delivery.AverageRating : 0f;
        int complaints = delivery != null ? delivery.Complaints : 0;
        float mult = 1f + avg / 5f;

        Centered(y, 700, $"Earned  ${earned:0}   x{mult:0.00}  ({avg:0.0}\u2605 avg)", small, Color.white); y += 28;
        Centered(y, 700, $"Complaints  {complaints}   (-{complaints * complaintPenalty:0})", small, new Color(0.95f, 0.5f, 0.4f)); y += 34;
        Centered(y, 700, $"SCORE  {finalScore}", mid, new Color(0.4f, 0.9f, 0.5f)); y += 56;

        if (enteringName)
        {
            Centered(y, 700, "New high score! Enter your name:", small, new Color(1f, 0.85f, 0.2f)); y += 34;
            playerName = GUI.TextField(new Rect(cx - 150, y, 300, 40), playerName, 12, field); y += 52;
            if (Button(cx - 120, y, 240, 50, "SUBMIT"))
            {
                HighScoreTable.Add(playerName, finalScore);
                enteringName = false;
            }
        }
        else
        {
            y = DrawBoard(y);
            y += 16;
            if (Button(cx - 120, y, 240, 52, "NEW RUN")) Restart();
        }
    }

    // Draws the high score board centred; returns the y below it.
    private float DrawBoard(float y)
    {
        float cx = Screen.width * 0.5f;
        Centered(y, 600, "HIGH SCORES", mid, new Color(0.3f, 0.75f, 1f)); y += 44;

        List<HighScoreTable.Entry> list = HighScoreTable.Load();
        if (list.Count == 0)
        {
            Centered(y, 600, "No scores yet \u2014 be the first!", small, new Color(0.7f, 0.72f, 0.76f)); y += 30;
            return y;
        }

        float w = 340f, x = cx - w * 0.5f;
        for (int i = 0; i < list.Count; i++)
        {
            small.alignment = TextAnchor.MiddleLeft; small.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, w * 0.7f, 26), $"{i + 1}.  {list[i].name}", small);
            small.alignment = TextAnchor.MiddleRight; small.normal.textColor = new Color(0.4f, 0.9f, 0.5f);
            GUI.Label(new Rect(x, y, w, 26), $"{list[i].score}", small);
            y += 28;
        }
        small.alignment = TextAnchor.UpperLeft;
        return y;
    }

    private void DrawShop()
    {
        Veil(0.82f);
        float w = 560f, h = 560f;
        float x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;
        GUI.color = new Color(0.08f, 0.09f, 0.12f, 0.97f);
        GUI.DrawTexture(new Rect(x, y, w, h), panel);
        GUI.color = Color.white;

        float pad = 28f, cx = x + pad, cw = w - pad * 2f, cy = y + pad;
        Centered(cy, cw, $"END OF DAY {day}", big, new Color(0.3f, 0.75f, 1f)); cy += 66;

        float money = delivery != null ? delivery.Money : 0f;
        float earned = money - moneyAtDayStart;
        small.alignment = TextAnchor.MiddleCenter; small.normal.textColor = new Color(0.85f, 0.87f, 0.9f);
        GUI.Label(new Rect(cx, cy, cw, 24), $"Earned today: ${earned:0.00}", small); cy += 26;
        small.normal.textColor = new Color(0.4f, 0.9f, 0.5f);
        GUI.Label(new Rect(cx, cy, cw, 28), $"Cash: ${money:0.00}", mid); cy += 44;
        small.alignment = TextAnchor.UpperLeft;

        cy = DrawUpgradeRow(cx, cy, cw, 0, "Max Health", $"+{healthPerLevel:0} HP");
        cy = DrawUpgradeRow(cx, cy, cw, 1, "Move Speed", $"+{speedPerLevel:0.0}");
        cy = DrawUpgradeRow(cx, cy, cw, 2, "Tip Bonus", $"+{tipPerLevel * 100f:0}% pay");
        cy = DrawUpgradeRow(cx, cy, cw, 3, "Carry Slots", $"+{slotsPerLevel} order(s)");

        cy += 10;
        if (Button(cx, cy, cw, 50, $"START DAY {day + 1}")) BeginDay();
    }

    private float DrawUpgradeRow(float x, float y, float w, int i, string name, string effect)
    {
        float rowH = 62f;
        GUI.color = new Color(1, 1, 1, 0.06f);
        GUI.DrawTexture(new Rect(x, y, w, rowH - 8f), panel);
        GUI.color = Color.white;

        small.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 12, y + 8, w * 0.55f, 24), $"{name}  (Lv {levels[i]})", mid);
        small.normal.textColor = new Color(0.7f, 0.73f, 0.78f);
        GUI.Label(new Rect(x + 12, y + 34, w * 0.55f, 20), effect + " per level", small);

        bool maxed = levels[i] >= CapFor(i);
        float cost = CostFor(i);
        bool canAfford = delivery != null && delivery.Money >= cost;

        string label = maxed ? "MAX" : $"${cost:0}";
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = maxed ? Color.gray : (canAfford ? new Color(0.25f, 0.85f, 0.4f) : new Color(0.6f, 0.3f, 0.3f));
        GUI.enabled = !maxed && canAfford;
        if (GUI.Button(new Rect(x + w - 130, y + 12, 118, 38), label, button)) Buy(i);
        GUI.enabled = true;
        GUI.backgroundColor = prevBg;

        return y + rowH;
    }

    private void Centered(float y, float w, string text, GUIStyle style, Color c)
    {
        style.normal.textColor = c; style.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(Screen.width * 0.5f - w * 0.5f, y, w, 60), text, style);
    }
    private bool Button(float x, float y, float w, float h, string label) => GUI.Button(new Rect(x, y, w, h), label, button);
    private void Veil(float a) { GUI.color = new Color(0.03f, 0.04f, 0.06f, a); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), veil); GUI.color = Color.white; }

    private void EnsureStyles()
    {
        if (big != null) return;
        big = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        mid = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        small = new GUIStyle(GUI.skin.label) { fontSize = 17 };
        button = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
        timerStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        field = new GUIStyle(GUI.skin.textField) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        veil = new Texture2D(1, 1); veil.SetPixel(0, 0, Color.white); veil.Apply();
        panel = new Texture2D(1, 1); panel.SetPixel(0, 0, Color.white); panel.Apply();
    }
}