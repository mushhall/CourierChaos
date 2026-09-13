using UnityEngine;

public class DeliveryZone : MonoBehaviour
{
    public enum ZoneType { Pickup, Dropoff }

    [SerializeField] private ZoneType zoneType = ZoneType.Pickup;
    public ZoneType Type => zoneType;

    [Header("Order (pickup zones only)")]
    [Tooltip("What this shop hands out, e.g. Pizza, Cake, Salad, Sandwich.")]
    [SerializeField] private string itemName = "Package";
    [Tooltip("A colour to represent this item in the UI.")]
    [SerializeField] private Color itemColor = new Color(1f, 0.8f, 0.2f);
    public string ItemName => itemName;
    public Color ItemColor => itemColor;

    [Tooltip("How close the player must get (in metres) to trigger this zone.")]
    [SerializeField] private float radius = 2.5f;

    private DeliveryManager player;
    private bool playerInside;

    private void Start()
    {
        player = FindFirstObjectByType<DeliveryManager>();
        if (player == null)
        {
            Debug.LogError("[DeliveryZone] No DeliveryManager found in the scene!");
        }
    }

    private void Update()
    {
        if (player == null) return;

        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = player.transform.position; b.y = 0f;

        bool inside = Vector3.Distance(a, b) <= radius;
        if (inside && !playerInside)
        {
            player.PlayerArrived(this);
        }
        playerInside = inside;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = zoneType == ZoneType.Pickup
            ? new Color(0.2f, 1f, 0.4f, 0.5f)   // green = pickup
            : new Color(0.3f, 0.6f, 1f, 0.5f);  // blue  = dropoff

        Gizmos.DrawWireSphere(transform.position, radius);
    }
}