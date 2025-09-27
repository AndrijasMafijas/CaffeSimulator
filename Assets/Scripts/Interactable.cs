using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    public string interactionText = "Press E to interact";

    // Event exposed in Inspector to hook up actions (pick up, open, etc.)
    public UnityEvent onInteract;

    // Called by PlayerInteractor when player presses the interact key while targeting this object
    public void Interact()
    {
        if (onInteract != null)
        {
            onInteract.Invoke();
        }
        else
        {
            Debug.Log($"Interacted with {gameObject.name}");
        }
    }
}