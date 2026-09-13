using UnityEngine;

public class DeliveryZone : MonoBehaviour
{
    //Collection or drop off
    public enum ZoneType { Pickup, Dropoff }

    // Choose the zone type in the Inspector
    [SerializeField] private ZoneType zoneType = ZoneType.Pickup;
    // Allows other scripts to read the zone type
    public ZoneType Type => zoneType;

    [Header("Pickup shops only")]
    [Tooltip("What this shop hands out, e.g. Pizza, Cake, Salad.")]
    // Name of the item the player picks up here
    [SerializeField] private string itemName = "Package";

    // Colour used to represent this item
    [SerializeField] private Color itemColor = new Color(1f, 0.8f, 0.2f);
    public string ItemName => itemName;
    public Color ItemColor => itemColor;

    [Header("Drop-off places only")]
    [Tooltip("Name shown in the order menu, e.g. 'Blue Apartments 01'.")]

    // Name of the delivery destination
    [SerializeField] private string placeName = "Destination";
    public string PlaceName => placeName;

    [Tooltip("How close the player must get (in metres) to trigger this zone.")]
    [SerializeField] private float radius = 2.5f;

    // Reference to the DeliveryManager
    private DeliveryManager player;

    // Remembers whether the player was already inside so arrival only triggers once
    private bool playerInside;

    private void Start()
    {
        // Find the DeliveryManager in the scene
        player = FindFirstObjectByType<DeliveryManager>();
        if (player == null)
            Debug.LogError("[DeliveryZone] No DeliveryManager found in the scene!");
    }

    private void Update()
    {
        // Can't check the player's position without DeliveryManagers
        if (player == null) return;

        // Position of this delivery zone, Ignore height because only horizontal distance matters
        Vector3 a = transform.position; 
        a.y = 0f;

        // Position of the player
        Vector3 b = player.transform.position; 
        b.y = 0f;

        // Check if the player is inside the zone radius
        bool inside = Vector3.Distance(a, b) <= radius;

        // Only trigger when the player first enters the zone
        if (inside && !playerInside) player.PlayerArrived(this);

        // Remember the player's current state for next frame
        playerInside = inside;
    }

    private void OnDrawGizmos()
    {

        // Green for pickup zones, blue for drop-off zones
        Gizmos.color = zoneType == ZoneType.Pickup
            ? new Color(0.2f, 1f, 0.4f, 0.5f)
            : new Color(0.3f, 0.6f, 1f, 0.5f);

        // Shows the trigger radius in the Scene view
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}