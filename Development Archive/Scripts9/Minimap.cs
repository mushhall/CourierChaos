using UnityEngine;
using System.Collections.Generic;

public class Minimap : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The DeliveryManager. Auto-found if left empty.")]
    [SerializeField] private DeliveryManager player;

    [Header("Layout")]
    [SerializeField] private float mapDiameter = 200f;
    [SerializeField] private float margin = 20f;
    [Tooltip("How many world metres the map RADIUS covers. Bigger = zoomed out.")]
    [SerializeField] private float worldRange = 70f;

    [Header("Blips")]
    [SerializeField] private float stopDotSize = 14f;
    [SerializeField] private float playerDotSize = 12f;
    [SerializeField] private Color pickupColor = new Color(0.2f, 1f, 0.4f);   // green = collect
    [SerializeField] private Color dropoffColor = new Color(0.3f, 0.6f, 1f);  // blue = deliver
    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.55f);

    private Texture2D dotTex;
    private readonly List<DeliveryManager.Stop> stops = new List<DeliveryManager.Stop>();

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<DeliveryManager>();
        dotTex = MakeCircleTexture(64);
    }

    private void OnGUI()
    {
        if (player == null || dotTex == null) return;

        player.GetStops(stops);

        // Fall back to the single suggested target if we have no orders.
        if (stops.Count == 0)
        {
            Transform t = player.CurrentTarget;
            if (t == null) return; // nothing to show, hide the map
            stops.Add(new DeliveryManager.Stop { position = t.position, isPickup = !player.HasOrder });
        }

        float radius = mapDiameter * 0.5f;
        Vector2 center = new Vector2(Screen.width - margin - radius, margin + radius);

        DrawDot(center, mapDiameter, backgroundColor);

        Vector3 playerPos = player.transform.position;
        float scale = radius / worldRange;

        // Pulse so the objectives catch the eye.
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 5f);

        foreach (DeliveryManager.Stop s in stops)
        {
            Color c = s.isPickup ? pickupColor : dropoffColor;
            c.a = 1f;
            DrawDot(WorldToMap(s.position, playerPos, center, scale, radius), stopDotSize * pulse, c);
        }

        // Player at the centre.
        DrawDot(center, playerDotSize, Color.white);
    }

    private Vector2 WorldToMap(Vector3 world, Vector3 playerPos, Vector2 center, float scale, float radius)
    {
        Vector2 offset = new Vector2((world.x - playerPos.x) * scale, -(world.z - playerPos.z) * scale);
        float maxR = radius - 8f;
        if (offset.magnitude > maxR) offset = offset.normalized * maxR;
        return center + offset;
    }

    private void DrawDot(Vector2 center, float size, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), dotTex);
        GUI.color = prev;
    }

    private Texture2D MakeCircleTexture(int res)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = res * 0.5f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / r;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.InverseLerp(0.9f, 1f, dist))));
            }
        tex.Apply();
        return tex;
    }
}