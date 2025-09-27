using UnityEngine;

// Drop-off point to serve customers at the front of the queue
[RequireComponent(typeof(Collider))]
public class OrderDropOff : MonoBehaviour
{
    public Interactable interactable;
    public ItemDefinition defaultItemToHand; // optional
    public CustomerQueue queue; // target queue

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        if (interactable == null) interactable = GetComponent<Interactable>();
        if (interactable == null) interactable = gameObject.AddComponent<Interactable>();
        interactable.interactionText = "Press E to serve";
    }

    private void Awake()
    {
        if (queue == null) queue = FindObjectOfType<CustomerQueue>();
    }

    private ItemDefinition PickItemForFront(Customer front)
    {
        if (front == null || front.order == null) return null;
        return front.order.requiredItem;
    }

    public void OnInteract()
    {
        if (queue == null)
        {
            if (interactable != null) interactable.interactionText = "No queue";
            return;
        }

        // Get customer at front spot (index 0)
        Customer front = null;
        var customers = FindObjectsOfType<Customer>();
        foreach (var c in customers)
        {
            if (c != null && queue.IsFront(c))
            {
                front = c;
                break;
            }
        }
        if (front == null)
        {
            if (interactable != null) interactable.interactionText = "No customer at front";
            return;
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        var inv = player.GetComponentInChildren<Inventory>() ?? player.GetComponent<Inventory>();
        if (inv == null) return;

        var item = PickItemForFront(front);
        if (item == null)
        {
            if (interactable != null) interactable.interactionText = "No item requested";
            return;
        }

        if (inv.Count(item) <= 0)
        {
            if (interactable != null) interactable.interactionText = $"Need {item.displayName}";
            return;
        }

        bool served = front.TryServe(item, front.order.quantity);
        if (!served)
        {
            if (interactable != null) interactable.interactionText = "Not enough items";
        }
        else
        {
            if (interactable != null) interactable.interactionText = $"Served {item.displayName}!";
        }
    }
}
