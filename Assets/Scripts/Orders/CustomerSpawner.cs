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

    [Header("Business Hours")]
    [Tooltip("If true, spawns only happen during open hours")] public bool useBusinessHours = true;
    [Tooltip("Opening hour (inclusive) in 24h format")] [Range(0f,24f)] public float openHour = 8f;
    [Tooltip("Closing hour (exclusive) in 24h format")] [Range(0f,24f)] public float closeHour = 24f;
    [Tooltip("When closing time hits, existing customers are forced to leave")]
    public bool kickCustomersOnClose = true;

    private float timer;
    private bool wasOpen;

    private void Start()
    {
        if (spawnOnStart && IsOpenNow())
        {
            SpawnOne();
        }
        wasOpen = IsOpenNow();
        ValidateSetup(logWarnings: true);
    }

    private void Update()
    {
        if (!ValidateSetup()) return;

        // respect business hours
        if (useBusinessHours)
        {
            bool openNow = IsOpenNow();
            if (openNow)
            {
                // Handle open state
                int count = FindObjectsOfType<Customer>().Length;
                if (count < maxCustomers)
                {
                    timer += Time.deltaTime;
                    if (timer >= spawnInterval)
                    {
                        timer = 0f;
                        SpawnOne();
                    }
                }
            }
            else
            {
                timer = 0f; // reset timer while closed
            }

            // detect transition from open -> closed
            if (wasOpen && !openNow)
            {
                if (kickCustomersOnClose)
                {
                    var customers = FindObjectsOfType<Customer>();
                    foreach (var c in customers)
                    {
                        c.ForceLeave();
                    }
                }
            }
            wasOpen = openNow;
            return;
        }

        // No business hours: default spawn behavior
        int defaultCount = FindObjectsOfType<Customer>().Length;
        if (defaultCount >= maxCustomers) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnOne();
        }
    }

    private bool IsOpenNow()
    {
        var tod = TimeOfDayManager.Instance;
        if (tod == null) return true; // if no time system, always open
        float h = tod.Hours;
        // Handle ranges that might wrap past midnight
        if (Mathf.Approximately(openHour, closeHour)) return true; // 24/7
        if (openHour < closeHour)
            return h >= openHour && h < closeHour; // normal daytime window
        else
            return h >= openHour || h < closeHour; // overnight window
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
        if (useBusinessHours && !IsOpenNow()) return;

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
