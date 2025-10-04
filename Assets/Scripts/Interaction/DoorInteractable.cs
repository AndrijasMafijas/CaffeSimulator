using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Simple door that opens/closes when player is nearby and presses E
[RequireComponent(typeof(Collider))]
public class DoorInteractable : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Pivot used for rotation (hinge). If null, uses this transform.")]
    public Transform pivot;

    [Tooltip("Local Y angle when closed (degrees)")]
    public float closedAngleY = 0f;

    [Tooltip("Local Y angle when open (degrees)")]
    public float openAngleY = 90f;

    [Header("Behavior")]
    [Tooltip("Degrees per second for opening/closing")]
    public float speed = 120f;

    [Tooltip("Optional: auto-close after seconds (<= 0 disables)")]
    public float autoCloseAfter = 0f;

    [Tooltip("Start in open state")] public bool startsOpen = false;

    [Header("Interaction")] 
    [Tooltip("Auto-open door when a NavMeshAgent enters trigger range")] 
    public bool autoOpenForAgents = true;

    [Tooltip("Force nearby agents to recalc path when door opens")] 
    public bool repathAgentsOnOpen = true;
    [Tooltip("Radius around the door to search for agents to repath")] 
    public float repathRadius = 5f;

    [Header("NavMesh Obstacle (optional)")]
    [Tooltip("Obstacles to toggle when door opens/closes. If empty, will search in children. Use Carve to update NavMesh at runtime.")]
    public NavMeshObstacle[] navMeshObstacles;

    [Header("OffMesh Link (optional)")]
    [Tooltip("Optional off-mesh link to bridge nav areas when door opens (helps if doorway wasn't baked as walkable). Enabled only when open.")]
    public OffMeshLink offMeshLink;
    [Tooltip("Start/end anchors for OffMeshLink. If OffMeshLink is missing, one will be created if both anchors are set.")]
    public Transform linkStart;
    public Transform linkEnd;

    [Header("Audio (optional)")]
    public AudioSource openSfx;
    public AudioSource closeSfx;

    private bool _isOpen;
    private bool _isMoving;
    private bool _playerInRange;
    private float _autoCloseTimer;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true; // ensure trigger so we can detect proximity
        if (pivot == null) pivot = transform; // default
    }

    private void Awake()
    {
        if (pivot == null) pivot = transform;
        if (navMeshObstacles == null || navMeshObstacles.Length == 0)
        {
            navMeshObstacles = GetComponentsInChildren<NavMeshObstacle>(true);
        }
        if (offMeshLink == null)
        {
            offMeshLink = GetComponentInChildren<OffMeshLink>(true);
            if (offMeshLink == null && linkStart != null && linkEnd != null)
            {
                var go = new GameObject("DoorOffMeshLink");
                go.transform.SetParent(transform, false);
                offMeshLink = go.AddComponent<OffMeshLink>();
                offMeshLink.startTransform = linkStart;
                offMeshLink.endTransform = linkEnd;
                offMeshLink.biDirectional = true;
                offMeshLink.activated = true;
                offMeshLink.autoUpdatePositions = true;
            }
        }
        // Initialize rotation
        _isOpen = startsOpen;
        float target = _isOpen ? openAngleY : closedAngleY;
        var e = pivot.localEulerAngles;
        e.y = target;
        pivot.localEulerAngles = e;

        ApplyObstacleState();
        ApplyLinkState();
    }

    private void Update()
    {
        if (_playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            Toggle();
        }

        if (_isOpen && autoCloseAfter > 0f && !_isMoving)
        {
            _autoCloseTimer += Time.deltaTime;
            if (_autoCloseTimer >= autoCloseAfter)
            {
                Close();
            }
        }
        else if (!_isOpen)
        {
            _autoCloseTimer = 0f;
        }
    }

    public void Toggle()
    {
        if (_isOpen) Close(); else Open();
    }

    public void Open()
    {
        if (_isMoving || _isOpen) return;
        _isOpen = true;
        _autoCloseTimer = 0f;
        if (openSfx) openSfx.Play();
        ApplyObstacleState();
        ApplyLinkState();
        if (repathAgentsOnOpen) RepathNearbyAgents();
        StopAllCoroutines();
        StartCoroutine(RotateTo(openAngleY));
    }

    public void Close()
    {
        if (_isMoving || !_isOpen) return;
        _isOpen = false;
        if (closeSfx) closeSfx.Play();
        ApplyObstacleState();
        ApplyLinkState();
        StopAllCoroutines();
        StartCoroutine(RotateTo(closedAngleY));
    }

    private void ApplyObstacleState()
    {
        if (navMeshObstacles == null) return;
        for (int i = 0; i < navMeshObstacles.Length; i++)
        {
            var ob = navMeshObstacles[i];
            if (ob == null) continue;
            ob.carving = true; // ensure carving enabled
#if UNITY_2020_1_OR_NEWER
            ob.carveOnlyStationary = false; // door moves/rotates
#endif
            ob.enabled = !_isOpen; // block when closed, free when open
        }
    }

    private void ApplyLinkState()
    {
        if (offMeshLink == null) return;
        offMeshLink.activated = _isOpen;
        offMeshLink.enabled = _isOpen;
    }

    private void RepathNearbyAgents()
    {
        // Try quick sphere around doorway to trigger replanning
        var hits = Physics.OverlapSphere(transform.position, repathRadius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            var agent = hits[i].GetComponentInParent<NavMeshAgent>();
            if (agent == null || !agent.enabled) continue;
            var dest = agent.destination;
            agent.ResetPath();
            agent.SetDestination(dest);
        }
    }

    private IEnumerator RotateTo(float targetY)
    {
        _isMoving = true;
        // Work with angles using Mathf.DeltaAngle
        while (true)
        {
            float currentY = pivot.localEulerAngles.y;
            float delta = Mathf.DeltaAngle(currentY, targetY);
            float step = Mathf.Sign(delta) * Mathf.Min(Mathf.Abs(delta), speed * Time.deltaTime);
            float nextY = currentY + step;

            var e = pivot.localEulerAngles;
            e.y = nextY;
            pivot.localEulerAngles = e;

            if (Mathf.Abs(Mathf.DeltaAngle(nextY, targetY)) <= 0.1f)
            {
                // snap to target
                e = pivot.localEulerAngles;
                e.y = targetY;
                pivot.localEulerAngles = e;
                break;
            }
            yield return null;
        }
        _isMoving = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            _playerInRange = true;
        }
        if (autoOpenForAgents && IsAgent(other))
        {
            Open();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            _playerInRange = false;
        }
    }

    private bool IsPlayer(Collider other)
    {
        // Common heuristics to detect the player
        if (other.CompareTag("Player")) return true;
        if (other.GetComponent<CharacterController>() != null) return true;
        if (other.GetComponentInParent<CharacterController>() != null) return true;
        // Fallback: Mini First Person Controller often has a Rigidbody on the player root
        var rb = other.attachedRigidbody;
        if (rb != null && rb.gameObject.CompareTag("Player")) return true;
        return false;
    }

    private bool IsAgent(Collider other)
    {
        if (other.GetComponent<NavMeshAgent>() != null) return true;
        if (other.GetComponentInParent<NavMeshAgent>() != null) return true;
        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (pivot == null) pivot = transform;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(pivot.position, pivot.position + pivot.right * 0.25f);
        // Draw arcs for angles (simple lines)
        Vector3 baseDir = pivot.forward;
        Quaternion qClosed = Quaternion.AngleAxis(closedAngleY, Vector3.up);
        Quaternion qOpen = Quaternion.AngleAxis(openAngleY, Vector3.up);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(pivot.position, qClosed * baseDir * 0.5f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(pivot.position, qOpen * baseDir * 0.5f);

        // Repath radius
        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, repathRadius);

        // Draw off-mesh link anchors
        if (linkStart != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(linkStart.position, 0.1f);
        }
        if (linkEnd != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(linkEnd.position, 0.1f);
        }
    }
#endif
}
