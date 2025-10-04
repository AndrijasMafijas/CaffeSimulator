using UnityEngine;
using System.Collections;
using UnityEngine.AI;

public class Customer : MonoBehaviour
{
    [Header("Setup")]
    public OrderDefinition order;
    public float moveSpeed = 2f;
    public Transform waitPoint; // legacy
    public bool usePhysics = true; // enable gravity and collisions (manual move mode)

    [Header("Navigation")]
    public bool useNavMesh = true; // use NavMeshAgent to navigate around obstacles
    [SerializeField, Min(0.05f)] private float navStoppingDistance = 0.25f; // close enough distance when using NavMesh
    [SerializeField, Min(0.1f)] private float navSampleRadius = 1.0f; // how far to search for nearest NavMesh position

    [Header("UI")]
    public CustomerOrderUI orderUI;

    [Header("Patience")]
    public float maxWaitSeconds = 30f; // fallback if order not set
    public AnimationCurve impatienceCurve = AnimationCurve.Linear(0, 0, 1, 1); // 0..1 -> 0..1
    [SerializeField, Min(0.01f)] private float arrivalRadius = 0.35f; // consider arrived if within this XZ distance (helps when blocked)

    [Header("State")]
    public bool isServed;
    public int queueIndex = -1;
    private float waitTimer;
    private bool isLeaving;
    private bool hasAnnouncedArrival; // first time reached a queue spot

    // Expose wait info to HUD
    public float WaitElapsed => waitTimer;
    public float WaitMax => maxWaitSeconds;

    private Inventory playerInventory;
    private Wallet playerWallet;

    // movement internals
    private Rigidbody rb;
    private Collider col;
    private Vector3 targetPos;
    private bool hasTarget;
    private NavMeshAgent agent;

    // Animator for visuals (acquired from children so swapped prefabs work)
    [Header("Animation")]
    [Tooltip("Name of the float parameter used to drive locomotion (e.g. 'Speed')")]
    public string animatorSpeedParameter = "Speed";
    [Tooltip("Threshold above which the animator is considered moving")]
    public float movingThreshold = 0.05f;
    private Animator animator;
    private Vector3 prevPosition;

    // Optional explicit run clip or Animator state names
    [Tooltip("Optional: AnimationClip to play for running (will be used as fallback if Animator doesn't have states)")]
    public AnimationClip runAnimation;
    [Tooltip("State name in the Animator to play for running (if present)")]
    public string runStateName = "RunForward";
    [Tooltip("State name in the Animator to play for idle (if present)")]
    public string idleStateName = "Idle";

    private Animation legacyAnimation; // legacy Animation component fallback
    private int runStateHash = 0;
    private int idleStateHash = 0;

    // queue
    private CustomerQueue queue;

    private void Reset()
    {
        EnsurePhysics();
        EnsureOrderUI();
    }

    private void Awake()
    {
        EnsurePhysics();
        EnsureOrderUI();
        EnsureNav();
        EnsureAnimator();
        prevPosition = CurrentPosition();
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerInventory = player.GetComponentInChildren<Inventory>() ?? player.GetComponent<Inventory>();
            playerWallet = player.GetComponentInChildren<Wallet>() ?? player.GetComponent<Wallet>();
        }
    }

    private void Start()
    {
        // Hide UI and do not register on HUD until we reach our first spot
        orderUI?.Hide();
        waitTimer = 0f;
        hasAnnouncedArrival = false;

        if (order != null)
        {
            maxWaitSeconds = order.maxWaitSeconds;
            // Defer UpdateOrderUI until arrival
        }

        if (queue == null)
        {
            var q = FindObjectOfType<CustomerQueue>();
            if (q != null) q.JoinQueue(this);
            else if (waitPoint != null) SetTarget(waitPoint.position);
        }
    }

    private void FixedUpdate()
    {
        if (useNavMesh && agent != null && agent.enabled)
        {
            agent.speed = Mathf.Max(0.01f, moveSpeed);
            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= agent.stoppingDistance)
                {
                    if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.0001f)
                    {
                        hasTarget = false;
                    }
                }
                else
                {
                    hasTarget = true;
                }
            }

            TryAnnounceArrival();
            UpdateAnimatorByAgent();
            prevPosition = CurrentPosition();
            return;
        }

        if (hasTarget)
        {
            Vector3 current = rb != null ? rb.position : transform.position;
            Vector3 dest = new Vector3(targetPos.x, current.y, targetPos.z);
            Vector3 next = Vector3.MoveTowards(current, dest, moveSpeed * Time.fixedDeltaTime);
            if (rb != null && usePhysics && !rb.isKinematic)
            {
                rb.MovePosition(next);
            }
            else
            {
                transform.position = next;
            }

            float dist = HorizontalDistance(current, targetPos);
            if (dist <= arrivalRadius)
            {
                hasTarget = false; // reached (or close enough if blocked)
            }
        }

        TryAnnounceArrival();

        // Update animator based on movement speed measured from position delta
        UpdateAnimatorByPosition(prevPosition, CurrentPosition());
        prevPosition = CurrentPosition();
    }

    private void TryAnnounceArrival()
    {
        if (hasAnnouncedArrival || isLeaving) return;

        bool atSpot = false;
        if (useNavMesh && agent != null && agent.enabled)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.0001f) atSpot = true;
            }
        }
        else
        {
            atSpot = HorizontalDistance(CurrentPosition(), targetPos) <= arrivalRadius + 0.01f;
        }

        if (atSpot)
        {
            hasAnnouncedArrival = true;
            waitTimer = 0f; // start patience now
            // Show UI and register HUD when the customer reaches the queue spot
            UpdateOrderUI();
            orderUI?.Show();
            OrderTicketsHUD.Instance?.Register(this);
        }
    }

    private void Update()
    {
        if (order == null || isLeaving) return;

        // Only count patience once we reached the first queue spot
        if (!isServed && hasAnnouncedArrival)
        {
            waitTimer += Time.deltaTime;
            float t = Mathf.Clamp01(waitTimer / maxWaitSeconds);
            float impatience = Mathf.Clamp01(impatienceCurve.Evaluate(t));
            orderUI?.SetProgress(impatience);
            if (waitTimer >= maxWaitSeconds)
            {
                StartLeave();
            }
        }
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private Vector3 CurrentPosition()
    {
        return rb != null ? rb.position : transform.position;
    }

    public void SetQueue(CustomerQueue q)
    {
        queue = q;
    }

    public void SetQueueTarget(Vector3 pos, int index)
    {
        queueIndex = index;
        SetTarget(pos);
    }

    private void SetTarget(Vector3 pos)
    {
        targetPos = pos;
        if (useNavMesh && agent != null && agent.enabled)
        {
            Vector3 dest = pos;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(pos, out hit, navSampleRadius, NavMesh.AllAreas))
            {
                dest = hit.position;
            }
            agent.stoppingDistance = Mathf.Max(0.01f, navStoppingDistance);
            agent.SetDestination(dest);
            hasTarget = true;
        }
        else
        {
            hasTarget = true;
        }
    }

    private void EnsureOrderUI()
    {
        if (orderUI == null)
        {
            orderUI = GetComponentInChildren<CustomerOrderUI>();
            if (orderUI == null)
            {
                var go = new GameObject("OrderUI");
                go.transform.SetParent(transform, false);
                orderUI = go.AddComponent<CustomerOrderUI>();
                orderUI.GetComponent<Transform>().localPosition = Vector3.zero;
            }
        }
    }

    private void UpdateOrderUI()
    {
        if (orderUI == null || order == null) return;
        var icon = order.requiredItem != null ? order.requiredItem.icon : null;
        var label = order.requiredItem != null ? $"{order.requiredItem.displayName} x{order.quantity}" : "(no order)";
        orderUI.Set(icon, label);
    }

    public bool TryServe(ItemDefinition item, int count)
    {
        if (order == null || isServed) return false;
        if (item != order.requiredItem || count < order.quantity) return false;

        if (playerInventory != null && playerInventory.Count(item) >= order.quantity)
        {
            playerInventory.Remove(item, order.quantity);
            if (playerWallet != null) playerWallet.Add(order.reward);
            isServed = true;
            StartLeave();
            return true;
        }
        return false;
    }

    // Force customer to leave immediately (e.g., closing time)
    public void ForceLeave()
    {
        StartLeave();
    }

    private void StartLeave()
    {
        if (isLeaving) return;
        isLeaving = true;
        orderUI?.Hide();
        OrderTicketsHUD.Instance?.Unregister(this);
        if (queue != null) queue.LeaveQueue(this);

        if (useNavMesh && agent != null)
        {
            if (rb != null) rb.isKinematic = true;
            if (col != null) col.enabled = false;
        }
        else
        {
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
            if (col != null) col.enabled = false;
        }

        StartCoroutine(LeaveRoutine());
    }

    private IEnumerator LeaveRoutine()
    {
        Vector3 fallback = transform.position + new Vector3(0, 0, -3f);
        Vector3 dest = queue != null ? queue.GetExitPosition(fallback) : fallback;

        if (useNavMesh && agent != null && agent.enabled)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(dest, out hit, navSampleRadius, NavMesh.AllAreas)) dest = hit.position;
            agent.stoppingDistance = 0.05f;
            agent.SetDestination(dest);

            // Wait until the agent reaches the destination (no timeout)
            while (true)
            {
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.0001f) break;
                }
                yield return null;
            }
            Destroy(gameObject);
            yield break;
        }
        else
        {
            float speed = moveSpeed * 1.2f;
            while (Vector3.Distance(transform.position, dest) > 0.05f)
            {
                transform.position = Vector3.MoveTowards(transform.position, dest, speed * Time.deltaTime);
                yield return null;
            }
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        OrderTicketsHUD.Instance?.Unregister(this);
        if (queue != null) queue.LeaveQueue(this);
    }

    private void EnsurePhysics()
    {
        col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<CapsuleCollider>();
            var cap = col as CapsuleCollider;
            if (cap != null)
            {
                cap.height = 2f;
                cap.radius = 0.4f;
                cap.center = new Vector3(0, 1f, 0);
            }
        }
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.useGravity = usePhysics;
        rb.isKinematic = !usePhysics; // will be true if using navmesh
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
    }

    private void EnsureNav()
    {
        if (!useNavMesh) return;
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
        }
        agent.speed = Mathf.Max(0.01f, moveSpeed);
        agent.angularSpeed = 720f;
        agent.acceleration = 16f;
        agent.stoppingDistance = Mathf.Max(0.01f, navStoppingDistance);
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = Random.Range(30, 70);
        agent.updateRotation = true;
        agent.updatePosition = true;

        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    // Acquire animator from children (useful when visuals are swapped at runtime)
    private void EnsureAnimator()
    {
        // Prefer an Animator on the same GameObject first
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // Prepare legacy Animation fallback if a run clip was assigned
        if (runAnimation != null)
        {
            legacyAnimation = GetComponent<Animation>();
            if (legacyAnimation == null)
            {
                legacyAnimation = gameObject.AddComponent<Animation>();
            }
            if (legacyAnimation.GetClip(runAnimation.name) == null)
            {
                legacyAnimation.AddClip(runAnimation, runAnimation.name);
            }
        }

        // Cache state hashes if animator has controller
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            runStateHash = Animator.StringToHash(runStateName);
            idleStateHash = Animator.StringToHash(idleStateName);
        }
    }

    // Unity callback when children change (ArtSwap may add visual children at runtime)
    private void OnTransformChildrenChanged()
    {
        EnsureAnimator();
    }

    private void UpdateAnimatorByAgent()
    {
        if (animator == null)
        {
            // fallback to legacy Animation if assigned
            float spdFallback = agent != null ? agent.velocity.magnitude : 0f;
            HandleLegacyAnimation(spdFallback);
            return;
        }
        if (agent == null) return;
        float spd = agent.velocity.magnitude;

        // Prefer driving parameters if animator supports them
        animator.SetFloat(animatorSpeedParameter, spd);
        animator.SetBool("IsMoving", spd > movingThreshold);

        // If animator contains a run state, crossfade to it when moving
        if (animator.runtimeAnimatorController != null && runStateHash != 0)
        {
            if (spd > movingThreshold && animator.HasState(0, runStateHash))
            {
                animator.CrossFade(runStateHash, 0.1f);
            }
            else if (spd <= movingThreshold && idleStateHash != 0 && animator.HasState(0, idleStateHash))
            {
                animator.CrossFade(idleStateHash, 0.1f);
            }
        }
    }

    private void UpdateAnimatorByPosition(Vector3 prev, Vector3 now)
    {
        // Calculate approximate horizontal speed
        float dist = Vector3.Distance(new Vector3(prev.x, 0f, prev.z), new Vector3(now.x, 0f, now.z));
        float spd = dist / Mathf.Max(0.0001f, Time.fixedDeltaTime);

        if (animator == null)
        {
            HandleLegacyAnimation(spd);
            return;
        }

        animator.SetFloat(animatorSpeedParameter, spd);
        animator.SetBool("IsMoving", spd > movingThreshold);

        if (animator.runtimeAnimatorController != null && runStateHash != 0)
        {
            if (spd > movingThreshold && animator.HasState(0, runStateHash))
            {
                animator.CrossFade(runStateHash, 0.1f);
            }
            else if (spd <= movingThreshold && idleStateHash != 0 && animator.HasState(0, idleStateHash))
            {
                animator.CrossFade(idleStateHash, 0.1f);
            }
        }
    }

    // Play/stop legacy Animation clip when Animator isn't available
    private void HandleLegacyAnimation(float speed)
    {
        if (legacyAnimation == null || runAnimation == null) return;
        string cname = runAnimation.name;
        if (speed > movingThreshold)
        {
            if (!legacyAnimation.IsPlaying(cname))
            {
                legacyAnimation.Play(cname);
            }
        }
        else
        {
            if (legacyAnimation.IsPlaying(cname))
            {
                legacyAnimation.Stop(cname);
            }
        }
    }
}
