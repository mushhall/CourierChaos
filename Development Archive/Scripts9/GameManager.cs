using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private enum Phase { Start, Playing, GameOver }
    private Phase phase = Phase.Start;

    private PlayerHealth health;
    private DeliveryManager delivery;

    private float startTime;
    private float endTime;

    private GUIStyle bigStyle, midStyle, smallStyle, buttonStyle;
    private Texture2D veil;

    private void Start()
    {
        health = FindFirstObjectByType<PlayerHealth>();
        delivery = FindFirstObjectByType<DeliveryManager>();

        // Begin paused on the start screen.
        Time.timeScale = 0f;
    }

    private void Update()
    {
        if (phase == Phase.Playing && health != null && health.IsDead)
        {
            phase = Phase.GameOver;
            endTime = Time.unscaledTime;
            Time.timeScale = 0f;   // freeze the world
        }
    }

    private void BeginGame()
    {
        phase = Phase.Playing;
        startTime = Time.unscaledTime;
        Time.timeScale = 1f;
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (phase == Phase.Start) DrawStartScreen();
        else if (phase == Phase.GameOver) DrawEndScreen();
    }

    private void DrawStartScreen()
    {
        DrawVeil(0.75f);

        float cx = Screen.width * 0.5f;
        float y = Screen.height * 0.30f;

        Centered(y, 520, "COURIER", bigStyle, new Color(0.3f, 0.75f, 1f));
        y += 90;
        Centered(y, 700, "Deliver fast. Dodge the traffic. Get paid.", midStyle, Color.white);
        y += 60;
        Centered(y, 700, "WASD / Arrow keys to drive. Blue beam = pick up, Green = deliver.",
                 smallStyle, new Color(0.8f, 0.82f, 0.86f));
        y += 80;

        if (Button(cx - 110, y, 220, 56, "START SHIFT"))
            BeginGame();
    }

    private void DrawEndScreen()
    {
        DrawVeil(0.82f);

        float cx = Screen.width * 0.5f;
        float y = Screen.height * 0.16f;

        Centered(y, 600, "SHIFT OVER", bigStyle, new Color(0.95f, 0.3f, 0.3f));
        y += 80;
        Centered(y, 600, "You crashed out!", midStyle, new Color(0.9f, 0.9f, 0.9f));
        y += 70;

        // Summary rows.
        float shiftLen = Mathf.Max(0f, endTime - startTime);
        int deliveries = delivery != null ? delivery.DeliveriesCompleted : 0;
        float money = delivery != null ? delivery.Money : 0f;
        float avg = delivery != null ? delivery.AverageRating : 0f;
        int complaints = delivery != null ? delivery.Complaints : 0;

        float rowW = 420f;
        SummaryRow(cx, ref y, rowW, "Money earned", $"${money:0.00}", new Color(0.4f, 0.9f, 0.5f));
        SummaryRow(cx, ref y, rowW, "Deliveries", $"{deliveries}", Color.white);
        SummaryRow(cx, ref y, rowW, "Average rating", $"{avg:0.0} stars", new Color(1f, 0.85f, 0.2f));
        SummaryRow(cx, ref y, rowW, "Complaints", $"{complaints}", new Color(0.95f, 0.5f, 0.4f));
        SummaryRow(cx, ref y, rowW, "Time survived", $"{shiftLen:0}s", Color.white);

        y += 40;
        if (Button(cx - 110, y, 220, 56, "NEW SHIFT"))
            Restart();
    }

    // ---------- drawing helpers ----------
    private void SummaryRow(float cx, ref float y, float w, string label, string value, Color valueColor)
    {
        float x = cx - w * 0.5f;
        smallStyle.alignment = TextAnchor.MiddleLeft;
        smallStyle.normal.textColor = new Color(0.7f, 0.73f, 0.78f);
        GUI.Label(new Rect(x, y, w * 0.6f, 34), label, smallStyle);

        midStyle.alignment = TextAnchor.MiddleRight;
        midStyle.normal.textColor = valueColor;
        GUI.Label(new Rect(x + w * 0.4f, y, w * 0.6f, 34), value, midStyle);
        midStyle.alignment = TextAnchor.MiddleCenter;

        y += 42;
    }

    private void Centered(float y, float w, string text, GUIStyle style, Color c)
    {
        style.normal.textColor = c;
        style.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(Screen.width * 0.5f - w * 0.5f, y, w, 60), text, style);
    }

    private bool Button(float x, float y, float w, float h, string label)
    {
        return GUI.Button(new Rect(x, y, w, h), label, buttonStyle);
    }

    private void DrawVeil(float alpha)
    {
        GUI.color = new Color(0.03f, 0.04f, 0.06f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), veil);
        GUI.color = Color.white;
    }

    private void EnsureStyles()
    {
        if (bigStyle != null) return;

        bigStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        midStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        smallStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        buttonStyle = new GUIStyle(GUI.skin.button)
        { fontSize = 22, fontStyle = FontStyle.Bold };

        veil = new Texture2D(1, 1);
        veil.SetPixel(0, 0, Color.white);
        veil.Apply();
    }
}