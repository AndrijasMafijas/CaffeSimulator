using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Inventory : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public ItemDefinition item;
        public int count;
    }

    [SerializeField] private int capacity = 12;
    [SerializeField] private List<Slot> slots = new List<Slot>();

    // Initialized to avoid null event at runtime
    public UnityEvent onChanged = new UnityEvent();

    public IList<Slot> Slots => slots; // expose read-only-ish access for UI

    private void Awake()
    {
        // Init fixed-size list
        while (slots.Count < capacity) slots.Add(new Slot());
    }

    public bool Add(ItemDefinition item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        bool changed = false;

        // Fill existing stacks
        for (int i = 0; i < slots.Count && amount > 0; i++)
        {
            var s = slots[i];
            if (s.item == item && s.count < item.maxStack)
            {
                int canAdd = Mathf.Min(item.maxStack - s.count, amount);
                s.count += canAdd;
                amount -= canAdd;
                changed = changed || canAdd > 0;
            }
        }

        // Use empty slots
        for (int i = 0; i < slots.Count && amount > 0; i++)
        {
            var s = slots[i];
            if (s.item == null || s.count == 0)
            {
                s.item = item;
                int canAdd = Mathf.Min(item.maxStack, amount);
                s.count = canAdd;
                amount -= canAdd;
                changed = changed || canAdd > 0;
            }
        }

        if (changed)
        {
            onChanged.Invoke();
        }

        return amount == 0;
    }

    public bool Remove(ItemDefinition item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        int have = Count(item);
        if (have < amount) return false;
        bool changed = false;

        for (int i = 0; i < slots.Count && amount > 0; i++)
        {
            var s = slots[i];
            if (s.item == item && s.count > 0)
            {
                int take = Mathf.Min(s.count, amount);
                s.count -= take;
                amount -= take;
                if (take > 0) changed = true;
                if (s.count == 0) s.item = null;
            }
        }

        if (changed)
        {
            onChanged.Invoke();
        }

        return true;
    }

    public int Count(ItemDefinition item)
    {
        int c = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s.item == item) c += s.count;
        }
        return c;
    }
}
