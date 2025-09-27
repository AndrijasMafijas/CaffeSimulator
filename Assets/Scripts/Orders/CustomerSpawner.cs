using UnityEngine;
using System.Collections.Generic;

public class CustomerSpawner : MonoBehaviour
{
    [Header("Setup")]
    public Transform spawnPoint; // legacy single spawn
    public List<Transform> spawnPoints = new List<Transform>(); // new multi-spawn
    public GameObject customerPrefab; // simple capsule with Customer script
    public List<OrderDefinition> possibleOrders = new List<OrderDefinition>();
    public CustomerQueue queue;

    [Header("Spawning")] 
    [Min(0.5f)] public float spawnInterval = 15f;
    [Min(0)] public int maxCustomers = 3;
    public bool spawnOnStart = true; // spawn one immediately for testing
    public bool randomizeSpawn = true; // pick random point from list

    private float timer;

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnOne();
        }
        ValidateSetup(logWarnings: true);
    }

    private void Update()
    {
        if (!ValidateSetup()) return;

        int count = FindObjectsOfType<Customer>().Length;
        if (count >= maxCustomers) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnOne();
        }
    }

    private bool ValidateSetup(bool logWarnings = false)
    {
        if (customerPrefab == null)
        {
            if (logWarnings) Debug.LogWarning("CustomerSpawner: Assign customerPrefab.", this);
            return false;
        }
        if ((spawnPoints == null || spawnPoints.Count == 0) && spawnPoint == null)
        {
            if (logWarnings) Debug.LogWarning("CustomerSpawner: Assign at least one spawn point.", this);
            return false;
        }
        if (possibleOrders == null || possibleOrders.Count == 0)
        {
            if (logWarnings) Debug.LogWarning("CustomerSpawner: Add at least one OrderDefinition to possibleOrders.", this);
            return false;
        }
        if (queue == null)
        {
            queue = FindObjectOfType<CustomerQueue>();
        }
        return true;
    }

    private Transform PickSpawnPoint()
    {
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            if (randomizeSpawn)
                return spawnPoints[Random.Range(0, spawnPoints.Count)];
            else
                return spawnPoints[0];
        }
        return spawnPoint;
    }

    [ContextMenu("Spawn Now")] // right-click component header -> Spawn Now
    public void SpawnOne()
    {
        if (customerPrefab == null) return;
        var sp = PickSpawnPoint();
        if (sp == null) return;

        var go = Instantiate(customerPrefab, sp.position, sp.rotation);
        var cust = go.GetComponent<Customer>();
        if (cust == null)
        {
            cust = go.AddComponent<Customer>();
        }
        if (queue == null) queue = FindObjectOfType<CustomerQueue>();
        if (queue != null) queue.JoinQueue(cust);

        if (possibleOrders != null && possibleOrders.Count > 0)
        {
            cust.order = possibleOrders[Random.Range(0, possibleOrders.Count)];
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Legacy single point
        if (spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.25f);
        }
        // New list points
        if (spawnPoints != null)
        {
            foreach (var sp in spawnPoints)
            {
                if (sp == null) continue;
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(sp.position, new Vector3(0.2f, 0.2f, 0.2f));
            }
        }
    }
#endif
}
