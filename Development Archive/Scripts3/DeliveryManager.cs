using UnityEngine;
using System.Collections.Generic;

public class DeliveryManager : MonoBehaviour
{
    [Header("State (watch while playing)")]
    [SerializeField] private bool hasOrder = false;
    [SerializeField] private int deliveriesCompleted = 0;

    private readonly List<DeliveryZone> pickups = new List<DeliveryZone>();
    private readonly List<DeliveryZone> dropoffs = new List<DeliveryZone>();

    private DeliveryZone currentPickup;
    private DeliveryZone currentDropoff;

    public bool HasOrder => hasOrder;
    public int DeliveriesCompleted => deliveriesCompleted;

    // Where the player should head right now (read by the beacon).
    public Transform CurrentTarget
    {
        get
        {
            DeliveryZone z = hasOrder ? currentDropoff : currentPickup;
            return z != null ? z.transform : null;
        }
    }

    private void Start()
    {
        // Find every zone in the scene and sort it into pickups / dropoffs.
        DeliveryZone[] zones =
            FindObjectsByType<DeliveryZone>(FindObjectsSortMode.None);

        foreach (DeliveryZone z in zones)
        {
            if (z.Type == DeliveryZone.ZoneType.Pickup) pickups.Add(z);
            else dropoffs.Add(z);
        }

        if (pickups.Count == 0)
            Debug.LogWarning("[DeliveryManager] No pickup zones in the scene.");
        if (dropoffs.Count == 0)
            Debug.LogWarning("[DeliveryManager] No dropoff zones in the scene.");

        // Assign the first pickup target.
        currentPickup = PickRandom(pickups, null);
    }

    // Called by a zone when the player walks into it.
    public void PlayerArrived(DeliveryZone zone)
    {
        if (!hasOrder && zone == currentPickup)
        {
            hasOrder = true;
            currentDropoff = PickRandom(dropoffs, null);
            Debug.Log("Order picked up! Follow the blue beam to deliver.");
        }
        else if (hasOrder && zone == currentDropoff)
        {
            hasOrder = false;
            deliveriesCompleted++;
            currentDropoff = null;
            currentPickup = PickRandom(pickups, currentPickup);
            Debug.Log($"Delivered! Total: {deliveriesCompleted}. New pickup marked.");
        }
        // Walking into any other (non-assigned) zone does nothing.
    }

    // Random zone from a list, avoiding 'avoid' when there's a choice.
    private DeliveryZone PickRandom(List<DeliveryZone> list, DeliveryZone avoid)
    {
        if (list.Count == 0) return null;
        if (list.Count == 1) return list[0];

        DeliveryZone choice;
        do { choice = list[Random.Range(0, list.Count)]; }
        while (choice == avoid);
        return choice;
    }

    private GUIStyle labelStyle;
    private void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;
        }

        GUI.Label(new Rect(20, 20, 700, 40),
            hasOrder ? "Carrying an order — follow the blue beam!"
                     : "Head to the green beam to pick up an order.",
            labelStyle);

        GUI.Label(new Rect(20, 55, 700, 40),
            $"Deliveries completed: {deliveriesCompleted}", labelStyle);
    }
}