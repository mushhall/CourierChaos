using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DestinationMarker : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Player's DeliveryManager. Auto-found if left empty.")]
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
    [SerializeField] private Color pickupColor = new Color(0.2f, 1f, 0.4f);   // green
    [SerializeField] private Color dropoffColor = new Color(0.3f, 0.6f, 1f);  // blue

    private MeshRenderer meshRenderer;
    private Material mat;

    private void Awake()
    {
        BuildBeaconMesh();

        meshRenderer = GetComponent<MeshRenderer>();
        mat = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.material = mat;

        if (player == null) player = FindFirstObjectByType<DeliveryManager>();
    }

    private void LateUpdate()
    {
        if (player == null) return;

        // The manager decides where we should be.
        Transform target = player.CurrentTarget;
        Color color = player.HasOrder ? dropoffColor : pickupColor;

        if (target == null)
        {
            meshRenderer.enabled = false;
            return;
        }
        meshRenderer.enabled = true;

        Vector3 pos = target.position;
        pos.y = groundOffset;
        transform.position = pos;
        transform.rotation = Quaternion.identity;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        color.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        mat.color = color;
    }

    private void BuildBeaconMesh()
    {
        int n = Mathf.Max(8, segments);
        Mesh mesh = new Mesh { name = "BeaconMesh" };

        var verts = new System.Collections.Generic.List<Vector3>();
        var cols = new System.Collections.Generic.List<Color>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();

        Color discCenter = new Color(1, 1, 1, 0.9f);
        Color discEdge = new Color(1, 1, 1, 0.45f);
        Color beamBottom = new Color(1, 1, 1, 0.6f);
        Color beamTop = new Color(1, 1, 1, 0f);

        int Add(Vector3 p, Color c)
        {
            verts.Add(p); cols.Add(c); uvs.Add(Vector2.zero);
            return verts.Count - 1;
        }

        Vector3 Ring(int i, float y) =>
            new Vector3(Mathf.Cos(2 * Mathf.PI * i / n) * radius, y,
                        Mathf.Sin(2 * Mathf.PI * i / n) * radius);

        int center = Add(Vector3.zero, discCenter);
        int[] discRing = new int[n];
        for (int i = 0; i < n; i++) discRing[i] = Add(Ring(i, 0f), discEdge);
        for (int i = 0; i < n; i++)
        {
            int a = discRing[i], b = discRing[(i + 1) % n];
            tris.Add(center); tris.Add(b); tris.Add(a);
        }

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

        GetComponent<MeshFilter>().mesh = mesh;
    }
}