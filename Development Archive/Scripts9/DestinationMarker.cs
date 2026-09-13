using UnityEngine;
using System.Collections.Generic;

public class DestinationMarker : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The DeliveryManager. Auto-found if left empty.")]
    [SerializeField] private DeliveryManager player;

    [Header("Shape")]
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private float beamHeight = 6f;
    [SerializeField] private int segments = 40;
    [SerializeField] private float groundOffset = 0.02f;

    [Header("Pulse")]
    [SerializeField] private float pulseSpeed = 2.5f;
    [SerializeField] private float minAlpha = 0.55f;
    [SerializeField] private float maxAlpha = 1f;

    [Header("Colors")]
    [SerializeField] private Color pickupColor = new Color(0.2f, 1f, 0.4f);   // green = go collect
    [SerializeField] private Color dropoffColor = new Color(0.3f, 0.6f, 1f);  // blue = go deliver

    private Mesh beaconMesh;
    private readonly List<MeshRenderer> pool = new List<MeshRenderer>();
    private readonly List<Material> mats = new List<Material>();
    private readonly List<DeliveryManager.Stop> stops = new List<DeliveryManager.Stop>();

    private void Awake()
    {
        beaconMesh = BuildBeaconMesh();
        if (player == null) player = FindFirstObjectByType<DeliveryManager>();

        // This object is just the manager; the child beacons do the rendering.
        MeshRenderer rootMR = GetComponent<MeshRenderer>();
        if (rootMR != null) rootMR.enabled = false;
    }

    private void LateUpdate()
    {
        if (player == null) return;

        player.GetStops(stops);

        // No active orders? Fall back to the single suggested target (a shop with jobs).
        if (stops.Count == 0)
        {
            Transform t = player.CurrentTarget;
            if (t != null)
                stops.Add(new DeliveryManager.Stop { position = t.position, isPickup = !player.HasOrder });
        }

        float pulse = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);

        for (int i = 0; i < stops.Count; i++)
        {
            MeshRenderer mr = GetBeacon(i);
            mr.enabled = true;

            Vector3 p = stops[i].position; p.y = groundOffset;
            mr.transform.position = p;
            mr.transform.rotation = Quaternion.identity;

            Color c = stops[i].isPickup ? pickupColor : dropoffColor;
            c.a = pulse;
            mats[i].color = c;
        }
        for (int i = stops.Count; i < pool.Count; i++)
            pool[i].enabled = false;
    }

    private MeshRenderer GetBeacon(int i)
    {
        if (i < pool.Count) return pool[i];

        GameObject go = new GameObject("Beacon_" + i);
        go.transform.SetParent(transform, false);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = beaconMesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        Material m = new Material(Shader.Find("Sprites/Default"));
        mr.material = m;

        pool.Add(mr);
        mats.Add(m);
        return mr;
    }

    private Mesh BuildBeaconMesh()
    {
        int n = Mathf.Max(8, segments);
        Mesh mesh = new Mesh { name = "BeaconMesh" };

        var verts = new List<Vector3>();
        var cols = new List<Color>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        Color discCenter = new Color(1, 1, 1, 0.9f);
        Color discEdge = new Color(1, 1, 1, 0.45f);
        Color beamBottom = new Color(1, 1, 1, 0.6f);
        Color beamTop = new Color(1, 1, 1, 0f);

        int Add(Vector3 p, Color c) { verts.Add(p); cols.Add(c); uvs.Add(Vector2.zero); return verts.Count - 1; }
        Vector3 Ring(int i, float y) =>
            new Vector3(Mathf.Cos(2 * Mathf.PI * i / n) * radius, y, Mathf.Sin(2 * Mathf.PI * i / n) * radius);

        int center = Add(Vector3.zero, discCenter);
        int[] discRing = new int[n];
        for (int i = 0; i < n; i++) discRing[i] = Add(Ring(i, 0f), discEdge);
        for (int i = 0; i < n; i++) { int a = discRing[i], b = discRing[(i + 1) % n]; tris.Add(center); tris.Add(b); tris.Add(a); }

        int[] bot = new int[n], top = new int[n];
        for (int i = 0; i < n; i++) bot[i] = Add(Ring(i, 0f), beamBottom);
        for (int i = 0; i < n; i++) top[i] = Add(Ring(i, beamHeight), beamTop);
        for (int i = 0; i < n; i++)
        {
            int i2 = (i + 1) % n;
            tris.Add(bot[i]); tris.Add(bot[i2]); tris.Add(top[i2]);
            tris.Add(bot[i]); tris.Add(top[i2]); tris.Add(top[i]);
        }

        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}