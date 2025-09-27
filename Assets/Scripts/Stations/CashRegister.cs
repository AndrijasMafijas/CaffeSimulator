using System.Collections.Generic;
using UnityEngine;

public class CashRegister : MonoBehaviour
{
    public enum RegisterMode { SellAll, SingleAccepted, SelectItem }

    [Header("References")]
    [SerializeField] private Interactable interactable;

    [Header("Mode")] 
    [SerializeField] private RegisterMode mode = RegisterMode.SelectItem;
    [SerializeField] private bool lockMovementDuringSelection = true;
    [SerializeField] private bool manageCursorDuringSelection = true;

    [Header("SingleAccepted (legacy)")]
    [SerializeField] private ItemDefinition acceptedItem;  // kept for backward compat
    [SerializeField] private int payout = 5;

    [Header("Texts")]
    [SerializeField] private string idleText = "Press E to sell";
    [SerializeField] private string nothingToSellText = "Nothing to sell";
    [SerializeField] private string soldText = "+{0}$ ({1})"; // {0}=amount, {1}=item names or summary

    private Inventory playerInventory;
    private Wallet playerWallet;
    private Coroutine resetPromptRoutine;

    // Selection state
    private bool selecting;
    private List<ItemDefinition> sellableItems = new List<ItemDefinition>();
    private int selectedIndex;
    private int selectedQty = 1;

    // Movement lock state
    private List<Behaviour> lockedBehaviours;
    private CursorLockMode prevCursorLock;
    private bool prevCursorVisible;

    private void Awake()
    {
        if (interactable == null) interactable = GetComponentInChildren<Interactable>();
        UpdatePrompt();
    }

    private void Update()
    {
        if (!selecting || mode != RegisterMode.SelectItem) return;
        var inv = GetInventory();
        if (inv == null) { CancelSelection(); return; }

        // Rebuild if inventory changed (simple: rebuild each frame)
        BuildSellableList(inv);
        if (sellableItems.Count == 0) { ShowPrompt(nothingToSellText); CancelSelection(); return; }
        selectedIndex = Mathf.Clamp(selectedIndex, 0, sellableItems.Count - 1);

        // Input (WASD preferred, keep arrows as secondary)
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) { selectedIndex = (selectedIndex - 1 + sellableItems.Count) % sellableItems.Count; selectedQty = 1; }
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) { selectedIndex = (selectedIndex + 1) % sellableItems.Count; selectedQty = 1; }

        var cur = sellableItems[selectedIndex];
        int have = inv.Count(cur);
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) { selectedQty = Mathf.Clamp(selectedQty + 1, 1, have); }
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) { selectedQty = Mathf.Clamp(selectedQty - 1, 1, have); }
        if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Escape)) { CancelSelection(); return; }

        UpdateSelectionPrompt(cur, have);
        // Note: confirming sale is handled by OnInteract (E key) to keep one-button confirm
    }

    private string GetSelectHelpRich()
    {
        // Smaller multi-line help using WASD
        return "<size=75%>A/D: change item\nW/S: change quantity\nE: confirm, R: cancel</size>";
    }

    private void UpdateSelectionPrompt(ItemDefinition cur, int have)
    {
        if (interactable == null) return;
        int price = cur != null ? cur.sellPrice : 0;
        int total = price * selectedQty;
        interactable.interactionText = string.Format("Sell {0} x{1} = ${2}\n{3}", cur.displayName, selectedQty, total, GetSelectHelpRich());
    }

    private void BuildSellableList(Inventory inv)
    {
        sellableItems.Clear();
        var set = new HashSet<ItemDefinition>();
        foreach (var slot in inv.Slots)
        {
            if (slot.item == null || slot.count <= 0) continue;
            if (slot.item.sellPrice <= 0) continue;
            if (set.Add(slot.item)) sellableItems.Add(slot.item);
        }
        if (selectedIndex >= sellableItems.Count) selectedIndex = Mathf.Max(0, sellableItems.Count - 1);
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

    private Wallet GetWallet()
    {
        if (playerWallet != null) return playerWallet;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;
        playerWallet = player.GetComponentInChildren<Wallet>();
        if (playerWallet == null) playerWallet = player.GetComponent<Wallet>();
        if (playerWallet == null) playerWallet = player.AddComponent<Wallet>();
        return playerWallet;
    }

    public void OnInteract()
    {
        switch (mode)
        {
            case RegisterMode.SellAll:
                SellAll();
                return;
            case RegisterMode.SingleAccepted:
                SellSingleItem();
                return;
            case RegisterMode.SelectItem:
                if (!selecting)
                {
                    StartSelection();
                }
                else
                {
                    ConfirmSelection();
                }
                return;
        }
    }

    private void StartSelection()
    {
        var inv = GetInventory();
        if (inv == null) { ShowPrompt(nothingToSellText); return; }
        BuildSellableList(inv);
        if (sellableItems.Count == 0) { ShowPrompt(nothingToSellText); return; }
        selecting = true;
        selectedIndex = 0;
        selectedQty = 1;
        if (lockMovementDuringSelection) SetPlayerMovementLocked(true);
        if (manageCursorDuringSelection) SetCursorManaged(true);
        UpdateSelectionPrompt(sellableItems[selectedIndex], inv.Count(sellableItems[selectedIndex]));
    }

    private void ConfirmSelection()
    {
        var inv = GetInventory();
        var wallet = GetWallet();
        if (inv == null || wallet == null) { CancelSelection(); return; }
        if (sellableItems.Count == 0) { CancelSelection(); return; }
        var cur = sellableItems[selectedIndex];
        int have = inv.Count(cur);
        selectedQty = Mathf.Clamp(selectedQty, 1, have);
        if (selectedQty <= 0) { CancelSelection(); return; }
        inv.Remove(cur, selectedQty);
        int earned = cur.sellPrice * selectedQty;
        if (earned > 0) wallet.Add(earned);
        ShowPrompt(string.Format(soldText, earned, cur.displayName + " x" + selectedQty));
        selecting = false;
        if (lockMovementDuringSelection) SetPlayerMovementLocked(false);
        if (manageCursorDuringSelection) SetCursorManaged(false);
    }

    private void CancelSelection()
    {
        selecting = false;
        if (lockMovementDuringSelection) SetPlayerMovementLocked(false);
        if (manageCursorDuringSelection) SetCursorManaged(false);
        UpdatePrompt();
    }

    private void SetCursorManaged(bool selectingNow)
    {
        if (selectingNow)
        {
            prevCursorLock = Cursor.lockState;
            prevCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = prevCursorLock;
            Cursor.visible = prevCursorVisible;
        }
    }

    private void SetPlayerMovementLocked(bool locked)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        if (locked)
        {
            if (lockedBehaviours == null) lockedBehaviours = new List<Behaviour>();
            lockedBehaviours.Clear();
            // Disable common movement-related behaviours if present
            TryDisable<MonoBehaviour>(player, "FirstPersonMovement");
            TryDisable<MonoBehaviour>(player, "Jump");
            TryDisable<MonoBehaviour>(player, "Zoom");
            TryDisable<MonoBehaviour>(player, "FirstPersonLook");
            TryDisable<MonoBehaviour>(player, "MouseLook");
        }
        else
        {
            if (lockedBehaviours != null)
            {
                foreach (var b in lockedBehaviours)
                {
                    if (b != null) b.enabled = true;
                }
                lockedBehaviours.Clear();
            }
        }

        void TryDisable<T>(GameObject root, string typeName) where T : Behaviour
        {
            var components = root.GetComponentsInChildren<T>(true);
            foreach (var c in components)
            {
                if (c != null && c.GetType().Name == typeName && c.enabled)
                {
                    c.enabled = false;
                    lockedBehaviours.Add(c);
                }
            }
        }
    }

    private void SellAll()
    {
        var inv = GetInventory();
        var wallet = GetWallet();
        if (inv == null || wallet == null)
        {
            Debug.LogWarning("CashRegister: Missing Inventory or Wallet on Player.");
            return;
        }

        int earned = 0;
        List<string> soldLines = new List<string>();

        var totals = new Dictionary<ItemDefinition, int>();
        foreach (var slot in inv.Slots)
        {
            if (slot.item == null || slot.count <= 0) continue;
            if (slot.item.sellPrice <= 0) continue;
            if (!totals.ContainsKey(slot.item)) totals[slot.item] = 0;
            totals[slot.item] += slot.count;
        }

        if (totals.Count == 0)
        {
            ShowPrompt(nothingToSellText);
            return;
        }

        foreach (var kv in totals)
        {
            var def = kv.Key;
            int cnt = kv.Value;
            if (cnt <= 0) continue;
            inv.Remove(def, cnt);
            int amount = def.sellPrice * cnt;
            earned += amount;
            soldLines.Add(string.Format("{0} x{1}", def.displayName, cnt));
        }

        if (earned > 0)
        {
            wallet.Add(earned);
            ShowPrompt(string.Format(soldText, earned, string.Join(", ", soldLines.ToArray())));
        }
        else
        {
            ShowPrompt(nothingToSellText);
        }
    }

    private void SellSingleItem()
    {
        var inv = GetInventory();
        var wallet = GetWallet();
        if (inv == null || wallet == null)
        {
            Debug.LogWarning("CashRegister: Missing Inventory or Wallet on Player.");
            return;
        }

        if (acceptedItem == null)
        {
            Debug.LogWarning("CashRegister: acceptedItem not set.");
            return;
        }

        if (inv.Count(acceptedItem) <= 0)
        {
            ShowPrompt("You need a " + acceptedItem.displayName);
            return;
        }

        inv.Remove(acceptedItem, 1);
        wallet.Add(payout);
        ShowPrompt(string.Format(soldText, payout, acceptedItem.displayName));
    }

    private void ShowPrompt(string text)
    {
        if (interactable != null)
        {
            interactable.interactionText = text;
            ScheduleResetPrompt();
        }
    }

    private void ScheduleResetPrompt()
    {
        if (resetPromptRoutine != null) StopCoroutine(resetPromptRoutine);
        resetPromptRoutine = StartCoroutine(ResetPromptAfter(1.5f));
    }

    private System.Collections.IEnumerator ResetPromptAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        UpdatePrompt();
        resetPromptRoutine = null;
    }

    private void UpdatePrompt()
    {
        if (interactable == null) return;
        interactable.interactionText = idleText + "\n<size=75%>Press E</size>";
    }
}
