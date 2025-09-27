using UnityEngine;

// Attach to a world item object with a collider
[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    public ItemDefinition item;
    public int amount = 1;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    // Hook this to Interactable.onInteract from Inspector
    public void DoPickup()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        var inv = player.GetComponentInChildren<Inventory>();
        if (inv == null) inv = player.GetComponent<Inventory>();
        if (inv == null)
        {
            Debug.LogWarning("No Inventory found on Player.");
            return;
        }

        if (inv.Add(item, amount))
        {
            gameObject.SetActive(false); // hide after pickup
        }
        else
        {
            Debug.Log("Inventory full or cannot add item.");
        }
    }
}
