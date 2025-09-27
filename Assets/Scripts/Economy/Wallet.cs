using UnityEngine;
using UnityEngine.Events;

public class Wallet : MonoBehaviour
{
    [SerializeField] private int balance = 0;

    [System.Serializable]
    public class IntEvent : UnityEvent<int> { }

    public IntEvent onChanged = new IntEvent();

    public int Balance => balance;

    public void Add(int amount)
    {
        if (amount <= 0) return;
        balance += amount;
        onChanged.Invoke(balance);
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (balance < amount) return false;
        balance -= amount;
        onChanged.Invoke(balance);
        return true;
    }
}
