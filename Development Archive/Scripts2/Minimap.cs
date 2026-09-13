using UnityEngine;

public class Minimap : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Player's DeliveryManager. Auto-found if left empty.")]
    [SerializeField] private DeliveryManager player;

    [Header("Layout")]
    [Tooltip("Diameter of the minimap, in pixels.")]
    [SerializeField] private float mapDiameter = 200f;
    [Tooltip("Pixels between the map and the screen corner.")]
    [SerializeField] private float margin = 20f;
    [Tooltip("How many world metres the map RADIUS covers. Bigger = zoomed out.")]
    [SerializeField] private float worldRange = 70f;

    [Header("Blips")]
    [SerializeField] private float zoneDotSize = 10f;
    [SerializeField] private float targetDotSize = 16f;
    [SerializeField] private float playerDotSize = 12f;
    [SerializeField] private Color pickupColor = new Color(0.2f, 1f, 0.4f);
    [SerializeField] private Color dropoffColor = new Color(0.3f, 0.6f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.55f);

    private DeliveryZone[] zones;
    private Texture2D dotTex;

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<DeliveryManager>();
        zones = FindObjectsByType<DeliveryZone>(FindObjectsSortMode.None);
        dotTex = MakeCircleTexture(64);
    }

    private void OnGUI()
    {
        if (player == null || dotTex == null) return;

        float radius = mapDiameter * 0.5f;

        // Minimap centre on screen (top-right corner).
        Vector2 center = new Vector2(
            Screen.width - margin - radius,
            margin + radius
        );

        // Background circle.
        DrawDot(center, mapDiameter, backgroundColor);

        Vector3 playerPos = player.transform.position;
        float scale = radius / worldRange;
        Transform current = player.CurrentTarget;

        // Every zone as a faint dot, coloured by type.
        if (zones != null)
        {
            foreach (DeliveryZone z in zones)
            {
                if (z == null) continue;
                Color c = (z.Type == DeliveryZone.ZoneType.Pickup)
                    ? pickupColor : dropoffColor;
                c.a = 0.35f;
                DrawDot(WorldToMap(z.transform.position, playerPos, center, scale, radius),
                        zoneDotSize, c);
            }
        }

        // The active objective: bright, pulsing, clamped to the edge so it
        // always shows direction even when it's far off the map.
        if (current != null)
        {
            Color c = player.HasOrder ? dropoffColor : pickupColor;
            c.a = 1f;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 5f);
            DrawDot(WorldToMap(current.position, playerPos, center, scale, radius),
                    targetDotSize * pulse, c);
        }

        // Player, always dead-centre.
        DrawDot(center, playerDotSize, Color.white);
    }

    // Convert a world position into a point on the minimap, relative to the
    // player (who stays centred). +Z world = up, +X world = right.
    private Vector2 WorldToMap(Vector3 world, Vector3 playerPos,
                               Vector2 center, float scale, float radius)
    {
        Vector2 offset = new Vector2(
            (world.x - playerPos.x) * scale,
            -(world.z - playerPos.z) * scale
        );

        float maxR = radius - 8f; // keep blips inside the circle
        if (offset.magnitude > maxR)
        {
            offset = offset.normalized * maxR;
        }
        return center + offset;
    }

    private void DrawDot(Vector2 center, float size, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(
            new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size),
            dotTex);
        GUI.color = prev;
    }

    // A soft-edged white circle we can tint any colour.
    private Texture2D MakeCircleTexture(int res)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        float r = res * 0.5f;
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / r;
                float a = 1f - Mathf.InverseLerp(0.9f, 1f, dist); // soft edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
        }
        tex.Apply();
        return tex;
    }
}