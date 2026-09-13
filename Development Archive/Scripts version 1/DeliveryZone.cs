using UnityEngine;

public class DeliveryZone : MonoBehaviour
{
    public enum ZoneType { Pickup, Dropoff }

    [SerializeField] private ZoneType zoneType = ZoneType.Pickup;

    private void OnTriggerEnter(Collider other)
    {

        Debug.Log($"[{zoneType} zone] entered by: {other.name}");

        DeliveryManager manager = other.GetComponentInParent<DeliveryManager>();
        if (manager == null)
        {
            Debug.Log("[DeliveryZone] No DeliveryManager on that object.");
            return;
        }

        if (zoneType == ZoneType.Pickup)
        {
            manager.TryPickup();
        }
        else
        {
            manager.TryDropoff();
        }
    }

    // Force the collider to be a trigger 
    private void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    // Draw the zone in the Scene view so it's easy to place.
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = zoneType == ZoneType.Pickup
            ? new Color(0.2f, 1f, 0.4f, 0.25f)   // green = pickup
            : new Color(0.3f, 0.6f, 1f, 0.25f);  // blue  = dropoff

        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}