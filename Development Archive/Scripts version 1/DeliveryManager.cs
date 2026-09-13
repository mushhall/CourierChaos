using UnityEngine;

public class DeliveryManager : MonoBehaviour
{
    [Header("State (watch these change while playing)")]
    [SerializeField] private bool hasOrder = false;
    [SerializeField] private int deliveriesCompleted = 0;

    // Other systems (UI, sounds, etc.) can read this without editing state.
    public bool HasOrder => hasOrder;
    public int DeliveriesCompleted => deliveriesCompleted;

    // Called by a Pickup zone when the player walks into it.
    public void TryPickup()
    {
        if (hasOrder)
        {
            Debug.Log("You're already carrying an order — go deliver it first!");
            return;
        }

        hasOrder = true;
        Debug.Log("Order picked up! Head to the delivery point.");
    }

    // Called by a Dropoff zone when the player walks into it.
    public void TryDropoff()
    {
        if (!hasOrder)
        {
            Debug.Log("Nothing to deliver — go pick up an order first.");
            return;
        }

        hasOrder = false;
        deliveriesCompleted++;
        Debug.Log($"Delivered! Total deliveries: {deliveriesCompleted}");
    }

    // Quick placeholder readout so you can SEE the state on the Game screen
    // without building a UI yet. Replace this with real UI (TextMeshPro) later.
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

        GUI.Label(
            new Rect(20, 20, 600, 40),
            hasOrder ? "Carrying an order — go deliver it!"
                     : "No order — head to the pickup point.",
            labelStyle
        );

        GUI.Label(
            new Rect(20, 55, 600, 40),
            $"Deliveries completed: {deliveriesCompleted}",
            labelStyle
        );
    }
}