using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DestinationMarker : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Player. Auto-found if left empty.")]
    [SerializeField] private DeliveryManager player;
    [Tooltip("Drag your PickupZone here.")]
    [SerializeField] private Transform pickupTarget;
    [Tooltip("Drag your DropoffZone here.")]
    [SerializeField] private Transform dropoffTarget;

    [Header("Shape")]
    [Tooltip("Radius of the circular base / beam.")]
    [SerializeField] private float radius = 2.5f;
    [Tooltip("How tall the light beam rises.")]
    [SerializeField] private float beamHeight = 6f;
    [Tooltip("Sides of the circle. Higher = smoother.")]
    [SerializeField] private int segments = 40;
    [Tooltip("Height above ground, to avoid flicker with the road.")]
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
        // Sprites/Default: unlit, transparent, renders both sides (Cull Off),
        // and multiplies by the mesh's vertex colors -> gives us the top fade.
        mat = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.material = mat;

        if (player == null) player = FindFirstObjectByType<DeliveryManager>();
    }

    private void LateUpdate()
    {
        if (player == null) return;

        bool goingToDropoff = player.HasOrder;
        Transform target = goingToDropoff ? dropoffTarget : pickupTarget;
        Color color = goingToDropoff ? dropoffColor : pickupColor;

        if (target == null)
        {
            meshRenderer.enabled = false;
            return;
        }
        meshRenderer.enabled = true;

        // Sit on the ground at the target.
        Vector3 pos = target.position;
        pos.y = groundOffset;
        transform.position = pos;
        transform.rotation = Quaternion.identity;

        // Breathing pulse on overall opacity. This multiplies the baked
        // vertex-alpha gradient, so the top of the beam still fades to nothing.
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        color.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        mat.color = color;
    }

    // Builds a filled circular disc plus a hollow beam (tube) rising from it.
    // Vertex alpha fades to 0 at the top of the beam for the light-shaft look.
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
            verts.Add(p);
            cols.Add(c);
            uvs.Add(Vector2.zero);
            return verts.Count - 1;
        }

        Vector3 Ring(int i, float y) =>
            new Vector3(Mathf.Cos(2 * Mathf.PI * i / n) * radius, y,
                        Mathf.Sin(2 * Mathf.PI * i / n) * radius);

        // ---- Disc (triangle fan) ----
        int center = Add(Vector3.zero, discCenter);
        int[] discRing = new int[n];
        for (int i = 0; i < n; i++) discRing[i] = Add(Ring(i, 0f), discEdge);
        for (int i = 0; i < n; i++)
        {
            int a = discRing[i];
            int b = discRing[(i + 1) % n];
            tris.Add(center); tris.Add(b); tris.Add(a);
        }

        // ---- Beam (tube wall) ----
        int[] bot = new int[n];
        int[] top = new int[n];
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