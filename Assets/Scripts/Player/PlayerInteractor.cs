using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PlayerInteractor : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField, Range(0.3f, 3f)] private float maxDistance = 1.5f;
    [SerializeField] private LayerMask interactMask = ~0; // default: everything

    [Header("Proximity")] 
    [SerializeField] private bool enableProximity = true;
    [SerializeField, Range(0.1f, 1.5f)] private float proximityRadius = 0.5f; // collide-radius around player
    [SerializeField] private Transform playerRoot; // if null, falls back to camera

    [Header("Refs")] 
    [SerializeField] private InteractionPromptUI promptUI;

    private Camera cam;
    private Interactable current;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (playerRoot == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerRoot = player.transform;
        }
    }

    private void Update()
    {
        UpdateTarget();
        HandleInput();
    }

    private void UpdateTarget()
    {
        Interactable best = null;

        // 1) Proximity candidate (preferred when overlapping)
        if (enableProximity)
        {
            var prox = FindProximityCandidate();
            if (prox != null)
            {
                best = prox;
            }
        }

        // 2) Fallback to center-screen raycast
        if (best == null)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactMask, QueryTriggerInteraction.Collide))
            {
                best = hit.collider.GetComponentInParent<Interactable>();
            }
        }

        current = best;
        if (promptUI != null)
        {
            if (current != null) promptUI.Show(current.interactionText);
            else promptUI.Hide();
        }
    }

    private Interactable FindProximityCandidate()
    {
        Vector3 origin = playerRoot != null ? playerRoot.position : cam.transform.position;
        Collider[] cols = Physics.OverlapSphere(origin, proximityRadius, interactMask, QueryTriggerInteraction.Collide);
        float bestDist = float.MaxValue;
        Interactable best = null;
        foreach (var c in cols)
        {
            if (c == null) continue;
            var it = c.GetComponentInParent<Interactable>();
            if (it == null) continue;
            float d = Vector3.Distance(origin, c.ClosestPoint(origin));
            if (d < bestDist)
            {
                bestDist = d;
                best = it;
            }
        }
        return best;
    }

    private void HandleInput()
    {
        if (current == null) return;
        if (Input.GetKeyDown(KeyCode.E))
        {
            current.Interact();
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!enableProximity) return;
        Transform tr = GetComponent<Camera>() != null ? GetComponent<Camera>().transform : transform;
        Vector3 origin = (playerRoot != null ? playerRoot.position : tr.position);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, proximityRadius);
    }
#endif
}
