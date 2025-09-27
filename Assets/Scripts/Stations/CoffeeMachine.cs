using UnityEngine;

public class CoffeeMachine : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Interactable interactable; // Hook this machine to an Interactable (same object or child)
    [SerializeField] private StationProgressUI progressUI;

    [Header("Recipe")]
    [SerializeField] private ItemDefinition requiresCup;
    [SerializeField] private ItemDefinition requiresBeans;
    [SerializeField] private ItemDefinition outputCoffee;

    [Header("Settings")]
    [SerializeField, Min(0.5f)] private float brewDuration = 3f;
    [SerializeField] private string idleText = "Press E to brew (needs Cup + Beans)";
    [SerializeField] private string brewingText = "Brewing...";
    [SerializeField] private string readyText = "Press E to collect Coffee";

    private enum State { Idle, Brewing, Ready }
    private State state = State.Idle;
    private float timer;
    private Inventory playerInventory;

    private void Awake()
    {
        if (interactable == null) interactable = GetComponentInChildren<Interactable>();
        if (progressUI == null) progressUI = GetComponentInChildren<StationProgressUI>();
        UpdatePrompt();
        UpdateProgressUI(0f, "");
        if (progressUI != null) progressUI.Hide();
    }

    private void Update()
    {
        if (state == State.Brewing)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / brewDuration);
            int pct = Mathf.RoundToInt(t * 100f);
            if (interactable != null)
            {
                interactable.interactionText = string.Format("{0} {1}%", brewingText, pct);
            }
            UpdateProgressUI(t, string.Format("{0} {1}%", brewingText, pct));
            if (timer >= brewDuration)
            {
                state = State.Ready;
                UpdatePrompt();
                UpdateProgressUI(1f, readyText);
                if (progressUI != null) progressUI.Hide();
            }
        }
    }

    private void UpdateProgressUI(float t, string status)
    {
        if (progressUI == null) return;
        progressUI.SetProgress(t);
        progressUI.SetStatus(status);
        if (state == State.Brewing) progressUI.Show();
    }

    private Inventory GetInventory()
    {
        if (playerInventory != null) return playerInventory;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;
        playerInventory = player.GetComponentInChildren<Inventory>();
        if (playerInventory == null) playerInventory = player.GetComponent<Inventory>();
        return playerInventory;
    }

    public void OnInteract()
    {
        switch (state)
        {
            case State.Idle:
                TryStartBrew();
                break;
            case State.Brewing:
                // do nothing while brewing
                break;
            case State.Ready:
                TryCollect();
                break;
        }
    }

    private void TryStartBrew()
    {
        var inv = GetInventory();
        if (inv == null)
        {
            Debug.LogWarning("CoffeeMachine: No Inventory found on Player.");
            return;
        }
        if (requiresCup != null && inv.Count(requiresCup) <= 0)
        {
            Debug.Log("Need a Cup to start brewing.");
            return;
        }
        if (requiresBeans != null && inv.Count(requiresBeans) <= 0)
        {
            Debug.Log("Need Beans to start brewing.");
            return;
        }

        if (requiresCup != null) inv.Remove(requiresCup, 1);
        if (requiresBeans != null) inv.Remove(requiresBeans, 1);

        timer = 0f;
        state = State.Brewing;
        UpdatePrompt();
        UpdateProgressUI(0f, string.Format("{0} {1}%", brewingText, 0));
    }

    private void TryCollect()
    {
        var inv = GetInventory();
        if (inv == null)
        {
            Debug.LogWarning("CoffeeMachine: No Inventory found on Player.");
            return;
        }
        if (outputCoffee == null)
        {
            Debug.LogWarning("CoffeeMachine: Output item is not assigned.");
            return;
        }

        if (inv.Add(outputCoffee, 1))
        {
            state = State.Idle;
            UpdatePrompt();
            UpdateProgressUI(0f, "");
        }
        else
        {
            if (interactable != null)
            {
                interactable.interactionText = "Inventory full - free a slot to collect";
            }
        }
    }

    private void UpdatePrompt()
    {
        if (interactable == null) return;
        switch (state)
        {
            case State.Idle:
                interactable.interactionText = idleText;
                break;
            case State.Brewing:
                interactable.interactionText = brewingText;
                break;
            case State.Ready:
                interactable.interactionText = readyText;
                break;
        }
    }
}
