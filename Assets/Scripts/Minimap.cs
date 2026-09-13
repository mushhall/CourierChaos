using UnityEngine;
using System.Collections.Generic;

public class Minimap : MonoBehaviour
{
    //References for minimap
    [Header("References")]
    [Tooltip("The DeliveryManager. Auto-found if left empty.")]

    //information about pickup/drop-off locations
    [SerializeField] private DeliveryManager player;
    [Tooltip("A Camera that is a CHILD of the Player, looking straight down, Orthographic.")]

    //See the world from above
    [SerializeField] private Camera minimapCamera;

    [Header("Render")]
    // Resolution of the minimap camera texture
    [SerializeField] private int renderResolution = 512;

    [Header("Layout")]

    // Size and position of the minimap on the screen
    [SerializeField] private float squareSize = 210f;
    [SerializeField] private float margin = 20f;
    [SerializeField] private float borderThickness = 3f;

    // Colour around the minimap
    [SerializeField] private Color borderColor = new Color(0.1f, 0.12f, 0.16f, 1f);

    [Header("Blips")]
    // Size of destination and player markers
    [SerializeField] private float stopDotSize = 14f;
    [SerializeField] private float playerDotSize = 12f;

    // Different colours for pickups/drop-offs
    [SerializeField] private Color pickupColor = new Color(0.2f, 1f, 0.4f);
    [SerializeField] private Color dropoffColor = new Color(0.3f, 0.6f, 1f);
    [SerializeField] private Color playerColor = new Color(1f, 1f, 1f);

    //Get player Position and direction
    private Transform playerT;
    //Render Minimap
    private RenderTexture rt;
    private Texture2D dotTex, white1;
    //Stores Current piclup/dropoff points
    private readonly List<DeliveryManager.Stop> stops = new List<DeliveryManager.Stop>();

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<DeliveryManager>();
        // Get the player's transform from PlayerMovement
        PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
        if (pm != null) playerT = pm.transform;

        // Create a texture for the minimap camera to render into
        if (minimapCamera != null)
        {
            rt = new RenderTexture(renderResolution, renderResolution, 16);
            minimapCamera.targetTexture = rt;
        }

        //Create Textures for GUI drawing
        dotTex = MakeCircle(64);
        // A 1x1 white texture can be resized to draw the border/lines
        white1 = new Texture2D(1, 1); white1.SetPixel(0, 0, Color.white); white1.Apply();
    }

    private void OnGUI()
    {
        // Don't try to draw anything if one of the important references is missing
        if (minimapCamera == null || rt == null || playerT == null || player == null) return;

        // Minimap GUI location
        float s = squareSize;
        float x = Screen.width - margin - s;   // top-right corner
        float y = margin;

        // Border frame.
        GUI.color = borderColor;

        GUI.DrawTexture(
            new Rect(
                x - borderThickness, 
                y - borderThickness,
                s + borderThickness * 2, 
                s + borderThickness * 2
            ), 
            white1
        );

        // Reset GUI colour after drawing
        GUI.color = Color.white;

        // The live rendered map.
        GUI.DrawTexture(new Rect(x, y, s, s), rt, ScaleMode.StretchToFill, false);

        // Find the Middle of the minimap
        Vector2 center = new Vector2(x + s * 0.5f, y + s * 0.5f);

        // Converts world distance into distance on the minimap
        float scale = (s * 0.5f) / Mathf.Max(1f, minimapCamera.orthographicSize);

        // Ask DeliveryManager for the current stops
        player.GetStops(stops);

        // Player's own axes: up on the map = player forward (map rotates with player).
        Vector3 f = playerT.forward; f.y = 0f; f.Normalize();
        Vector3 r = playerT.right; r.y = 0f; r.Normalize();

        // Makes the destination marker pulse slightly
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 5f);

        foreach (DeliveryManager.Stop st in stops)
        {

            // Direction from the player to the stop
            Vector3 d = st.position - playerT.position; 
            // Ignore height because this is a 2D minimap
            d.y = 0f;

            // Find how far the destination is to the player's left/right
            float ix = Vector3.Dot(d, r);

            // Find how far the destination is in front of/behind the player
            float iy = Vector3.Dot(d, f);

            // Convert the world position into a position  on the minimap
            Vector2 p = center + new Vector2(ix * scale, -iy * scale);

            // Keep the marker inside the minimap even if the destination is far away
            p.x = Mathf.Clamp(p.x, x + 8, x + s - 8);
            p.y = Mathf.Clamp(p.y, y + 8, y + s - 8);

            // Choose colour based on pickup/drop-off
            Color c = st.isPickup ? pickupColor : dropoffColor; 
            c.a = 1f;

            // Draw the destination marker
            DrawDot(p, stopDotSize * pulse, c);
        }

        // Player marker: dot + a short "nose" line pointing up. forward direction
        GUI.color = playerColor;
        GUI.DrawTexture(new Rect(center.x - 1.5f, center.y - playerDotSize, 3f, playerDotSize), white1);
        GUI.color = Color.white;
        // Put the player's dot in the centre
        DrawDot(center, playerDotSize, playerColor);
    }

    private void DrawDot(Vector2 c, float size, Color color)
    {
        // Remember the current GUI colour
        Color prev = GUI.color; 
        // Change colour for this marker
        GUI.color = color;
        // Draw the circle centred on position c
        GUI.DrawTexture(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size), dotTex);
        // Restore the old colour
        GUI.color = prev;
    }

    // Creating Dor Image
    private Texture2D MakeCircle(int res)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        // Radius of the circle
        float r = res * 0.5f;

        // Check every pixel in the texture
        for (int yy = 0; yy < res; yy++)
            for (int xx = 0; xx < res; xx++)
            {

                // Distance of this pixel from the centre
                float dx = xx - r + 0.5f, dy = yy - r + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / r;

                // Pixels near the edge become transparent, which creates a smooth circular dot
                tex.SetPixel(xx, yy, new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.InverseLerp(0.9f, 1f, dist))));
            }
        tex.Apply();
        return tex;
    }
}