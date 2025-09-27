using System.Collections.Generic;
using UnityEngine;

public class CustomerQueue : MonoBehaviour
{
    [Header("Queue Spots (0 = front)")]
    public List<Transform> spots = new List<Transform>();

    [Header("Exit Spots")]
    public List<Transform> exitSpots = new List<Transform>();
    [SerializeField] private bool randomizeExit = true;

    private readonly List<Customer> customers = new List<Customer>();

    public int Count => customers.Count;

    public void JoinQueue(Customer c)
    {
        if (c == null) return;
        if (customers.Contains(c)) return;
        customers.Add(c);
        c.SetQueue(this);
        UpdateAssignments();
    }

    public void LeaveQueue(Customer c)
    {
        if (c == null) return;
        if (!customers.Remove(c)) return;
        UpdateAssignments();
    }

    public bool IsFront(Customer c)
    {
        return customers.Count > 0 && customers[0] == c;
    }

    public Vector3 GetExitPosition(Vector3 fallback)
    {
        if (exitSpots != null && exitSpots.Count > 0)
        {
            if (randomizeExit)
            {
                var t = exitSpots[Random.Range(0, exitSpots.Count)];
                if (t != null) return t.position;
            }
            else
            {
                foreach (var t in exitSpots)
                {
                    if (t != null) return t.position;
                }
            }
        }
        return fallback;
    }

    private void UpdateAssignments()
    {
        if (spots == null || spots.Count == 0)
            return;

        int lastIndex = spots.Count - 1;
        for (int i = 0; i < customers.Count; i++)
        {
            var cust = customers[i];
            if (cust == null) continue;
            int spotIndex = Mathf.Min(i, lastIndex);
            var spot = spots[spotIndex];
            if (spot != null)
            {
                cust.SetQueueTarget(spot.position, i);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        if (spots != null)
        {
            for (int i = 0; i < spots.Count; i++)
            {
                var t = spots[i];
                if (t == null) continue;
                Gizmos.DrawWireSphere(t.position, 0.15f);
                UnityEditor.Handles.Label(t.position + Vector3.up * 0.1f, $"Q {i}");
            }
        }
        Gizmos.color = Color.cyan;
        if (exitSpots != null)
        {
            for (int i = 0; i < exitSpots.Count; i++)
            {
                var t = exitSpots[i];
                if (t == null) continue;
                Gizmos.DrawWireCube(t.position, new Vector3(0.2f, 0.2f, 0.2f));
                UnityEditor.Handles.Label(t.position + Vector3.up * 0.1f, $"Exit {i}");
            }
        }
    }
#endif
}
