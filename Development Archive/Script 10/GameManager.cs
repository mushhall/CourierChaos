using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // The game can be in one of these three states
    private enum Phase { Start, Playing, GameOver }

    // Game begins on the start screen
    private Phase phase = Phase.Start;

    // References used to check player health
    // and get delivery results at the end
    private PlayerHealth health;
    private DeliveryManager delivery;

    // Used to work out how long the player survived
    private float startTime;
    private float endTime;

    // Styles used for the start/game-over UI
    private GUIStyle bigStyle, midStyle, smallStyle, buttonStyle;

    // Small texture stretched over the screen to make the background darker
    private Texture2D veil;

    private void Start()
    {
        // Find the health and delivery scripts in the scene
        health = FindFirstObjectByType<PlayerHealth>();
        delivery = FindFirstObjectByType<DeliveryManager>();

        // Begin paused on the start screen.
        Time.timeScale = 0f;
    }

    private void Update()
    {

        // Only check for death while the game is being played
        if (phase == Phase.Playing && health != null && health.IsDead)
        {

            // Switch to the game-over state
            phase = Phase.GameOver;

            // Save the time when the game ended
            endTime = Time.unscaledTime;

            Time.timeScale = 0f;   // freeze the world
        }
    }

    private void BeginGame()
    {

        // Change from the start screen to gameplay
        phase = Phase.Playing;

        // Remember when the shift started
        startTime = Time.unscaledTime;

        // Unpause the game
        Time.timeScale = 1f;
    }

    private void Restart()
    {

        // Make sure time is running again before reloading
        Time.timeScale = 1f;

        // Reload the current Unity scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnGUI()
    {
        // Create the GUI styles if they haven't been made yet
        EnsureStyles();

        // Draw different screens depending on game state
        if (phase == Phase.Start) DrawStartScreen();
        else if (phase == Phase.GameOver) DrawEndScreen();
    }

    private void DrawStartScreen()
    {
        // Darken the game behind the menu
        DrawVeil(0.75f);

        // Find the centre of the screen
        float cx = Screen.width * 0.5f;

        // Starting vertical position for the text
        float y = Screen.height * 0.30f;

        // Game title
        Centered(y, 520, "COURIER", bigStyle, new Color(0.3f, 0.75f, 1f));
        y += 90;

        //Description
        Centered(y, 700, "Deliver fast. Dodge the traffic. Get paid.", midStyle, Color.white);
        y += 60;
        //Controls Instructions
        Centered(y, 700, "WASD / Arrow keys to drive. Blue beam = pick up, Green = deliver.",
                 smallStyle, new Color(0.8f, 0.82f, 0.86f));
        y += 80;


        // Start the game when the button is clicked
        if (Button(cx - 110, y, 220, 56, "START SHIFT"))
            BeginGame();
    }

    private void DrawEndScreen()
    {

        // Darker overlay for the game-over screen
        DrawVeil(0.82f);

        float cx = Screen.width * 0.5f;
        float y = Screen.height * 0.16f;

        // Game-over heading
        Centered(y, 600, "SHIFT OVER", bigStyle, new Color(0.95f, 0.3f, 0.3f));
        y += 80;
        Centered(y, 600, "You crashed out!", midStyle, new Color(0.9f, 0.9f, 0.9f));
        y += 70;

        // Summary rows.
        float shiftLen = Mathf.Max(0f, endTime - startTime);

        // Get the player's results from DeliveryManager.
        // If DeliveryManager is missing, use 0 instead.
        int deliveries = delivery != null ? delivery.DeliveriesCompleted : 0;
        float money = delivery != null ? delivery.Money : 0f;
        float avg = delivery != null ? delivery.AverageRating : 0f;
        int complaints = delivery != null ? delivery.Complaints : 0;

        float rowW = 420f;

        // Display the player's final results
        SummaryRow(cx, ref y, rowW, "Money earned", $"${money:0.00}", new Color(0.4f, 0.9f, 0.5f));
        SummaryRow(cx, ref y, rowW, "Deliveries", $"{deliveries}", Color.white);
        SummaryRow(cx, ref y, rowW, "Average rating", $"{avg:0.0} stars", new Color(1f, 0.85f, 0.2f));
        SummaryRow(cx, ref y, rowW, "Complaints", $"{complaints}", new Color(0.95f, 0.5f, 0.4f));
        SummaryRow(cx, ref y, rowW, "Time survived", $"{shiftLen:0}s", Color.white);


//Reload, New Game
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

    // Helper method for drawing text in the centre
    private void Centered(float y, float w, string text, GUIStyle style, Color c)
    {
        style.normal.textColor = c;
        style.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(Screen.width * 0.5f - w * 0.5f, y, w, 60), text, style);
    }

    // Helper method for creating GUI buttons
    private bool Button(float x, float y, float w, float h, string label)
    {
        return GUI.Button(new Rect(x, y, w, h), label, buttonStyle);
    }

    // Draw a transparent dark layer over the entire screen
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