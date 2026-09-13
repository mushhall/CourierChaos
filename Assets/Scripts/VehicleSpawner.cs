using UnityEngine;

public class VehicleSpawner : MonoBehaviour
{
    // Shared across ALL spawners. GameManager raises this each day so traffic
    // speeds up over time. 1 = normal, 1.3 = 30% faster, etc.
    public static float SpeedMultiplier = 1f;

    [System.Serializable]
    public class VehicleType
    {
        public GameObject prefab;
        [Tooltip("Slowest this vehicle type will drive.")]
        public float minSpeed = 4f;
        [Tooltip("Fastest this vehicle type will drive.")]
        public float maxSpeed = 8f;
    }

    [Header("Vehicles (drag your car prefabs here)")]
    [SerializeField] private VehicleType[] vehicles;

    [Header("Spawning")]
    [Tooltip("Seconds between spawns, picked randomly in this range.")]
    [SerializeField] private float minInterval = 1.5f;
    [SerializeField] private float maxInterval = 4f;
    [Tooltip("Random sideways spread at spawn, so cars don't stack perfectly.")]
    [SerializeField] private float laneJitter = 0f;

    [Header("Despawn (shared by all cars from this spawner)")]
    [Tooltip("Centre of your town. Leave at 0,0,0 if that's roughly the middle.")]
    [SerializeField] private Vector3 townCenter = Vector3.zero;
    [Tooltip("Cars are removed once they drive further than this from the centre.")]
    [SerializeField] private float despawnRadius = 250f;

    private float timer;

    private void Start()
    {
        timer = Random.Range(minInterval, maxInterval);
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            Spawn();
            timer = Random.Range(minInterval, maxInterval);
        }
    }

    private void Spawn()
    {
        if (vehicles == null || vehicles.Length == 0) return;

        VehicleType v = vehicles[Random.Range(0, vehicles.Length)];
        if (v.prefab == null) return;

        Vector3 pos = transform.position;
        if (laneJitter > 0f)
            pos += transform.right * Random.Range(-laneJitter, laneJitter);

        GameObject car = Instantiate(v.prefab, pos, transform.rotation);

        VehicleMover mover = car.GetComponent<VehicleMover>();
        if (mover == null) mover = car.AddComponent<VehicleMover>();

        // Apply the day's speed multiplier to this car's random speed.
        float speed = Random.Range(v.minSpeed, v.maxSpeed) * SpeedMultiplier;
        mover.Init(transform.forward, speed, townCenter, despawnRadius);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 6f);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        DrawCircle(townCenter, despawnRadius);
    }

    private void DrawCircle(Vector3 c, float r)
    {
        Vector3 prev = c + new Vector3(r, 0, 0);
        for (int i = 1; i <= 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}