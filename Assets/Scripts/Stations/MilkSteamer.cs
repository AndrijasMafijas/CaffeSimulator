using UnityEngine;

public class MilkSteamer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Interactable interactable; // Hook to an Interactable
    [SerializeField] private StationProgressUI progressUI;

    [Header("Recipe")]
    [SerializeField] private ItemDefinition requiresMilk;
    [SerializeField] private ItemDefinition outputSteamedMilk;

    [Header("Settings")]
    [SerializeField, Min(0.5f)] private float steamDuration = 2.5f;
    [SerializeField] private string idleText = "Press E to steam (needs Milk)";
    [SerializeField] private string steamingText = "Steaming...";
    [SerializeField] private string readyText = "Press E to collect Steamed Milk";

    private enum State { Idle, Steaming, Ready }
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
        if (state == State.Steaming)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / steamDuration);
            int pct = Mathf.RoundToInt(t * 100f);
            if (interactable != null)
            {
                interactable.interactionText = string.Format("{0} {1}%", steamingText, pct);
            }
            UpdateProgressUI(t, string.Format("{0} {1}%", steamingText, pct));
            if (timer >= steamDuration)
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
        // Lazy find if user added UI later
        if (progressUI == null) progressUI = GetComponentInChildren<StationProgressUI>();
        if (progressUI == null) return;
        progressUI.SetProgress(t);
        progressUI.SetStatus(status);
        if (state == State.Steaming) progressUI.Show();
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
                TryStartSteam();
                break;
            case State.Steaming:
                break;
            case State.Ready:
                TryCollect();
                break;
        }
    }

    private void TryStartSteam()
    {
        var inv = GetInventory();
        if (inv == null)
        {
            Debug.LogWarning("MilkSteamer: No Inventory found on Player.");
            return;
        }
        if (requiresMilk != null && inv.Count(requiresMilk) <= 0)
        {
            Debug.Log("Need Milk to start steaming.");
            return;
        }

        if (requiresMilk != null) inv.Remove(requiresMilk, 1);

        timer = 0f;
        state = State.Steaming;
        UpdatePrompt();
        UpdateProgressUI(0f, string.Format("{0} {1}%", steamingText, 0));
    }

    private void TryCollect()
    {
        var inv = GetInventory();
        if (inv == null)
        {
            Debug.LogWarning("MilkSteamer: No Inventory found on Player.");
            return;
        }
        if (outputSteamedMilk == null)
        {
            Debug.LogWarning("MilkSteamer: Output item is not assigned.");
            return;
        }

        if (inv.Add(outputSteamedMilk, 1))
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
            case State.Steaming:
                interactable.interactionText = steamingText;
                break;
            case State.Ready:
                interactable.interactionText = readyText;
                break;
        }
    }
}
