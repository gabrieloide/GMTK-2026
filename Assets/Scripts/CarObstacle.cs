using System.Collections.Generic;
using UnityEngine;

// Traffic car AI: wanders the RoadNetwork graph node by node, steering smoothly toward
// each next waypoint (offset into its own lane so opposing traffic doesn't share a
// centerline - see RoadNetwork.GetLanePoint) instead of snapping heading instantly.
// After a random number of hops it stops wandering, paths to the RoadNode nearest a
// Salida SpawnPoint via RoadNetwork.ShortestPathToNearestExit, drives the final stretch
// off the node graph to that spawn marker's own position, and despawns there - which may
// well be a different spawn than the one it entered from.
[RequireComponent(typeof(Rigidbody))]
public class CarObstacle : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private float turnSpeed = 120f;
    [SerializeField] private float nodeArriveRadius = 1.5f;
    [SerializeField] private int minWanderHops = 4;
    [SerializeField] private int maxWanderHops = 10;
    // Safety net only - normal despawn happens on arrival at the return spawn node. This
    // just guards against a misconfigured/disconnected graph leaving a car stuck forever.
    [SerializeField] private float maxLifetime = 120f;

    [Header("Reactions")]
    [Tooltip("Distance at which the NPC car gets angry if the player cuts them off.")]
    [SerializeField] private float nearMissDistance = 8f;
    private bool hasReactedToPlayer = false;
    private Transform playerTransform;

    private Rigidbody rb;
    private RoadNetwork network;
    private RoadNode fromNode;
    private RoadNode toNode;
    private List<RoadNode> returnPath;
    private int returnPathIndex;
    private SpawnPoint exitPoint;
    private float lifeTimer;

    private System.Action<CarObstacle> onDespawn;

    // firstTarget is picked by the caller (CarSpawner) rather than chosen internally so
    // the car can be instantiated already facing the same node it's about to drive to.
    public void Init(RoadNetwork roadNetwork, RoadNode startNode, RoadNode firstTarget, float moveSpeed, System.Action<CarObstacle> onDespawn = null)
    {
        this.onDespawn = onDespawn;
        network = roadNetwork;
        speed = moveSpeed;
        fromNode = startNode;
        toNode = firstTarget;
        
        // Pick a random exit immediately upon spawning, ensuring it's at least 50 units away (so they travel across town).
        exitPoint = network.GetRandomSalidaSpawnPoint(startNode.transform.position, 50f);
        if (exitPoint != null && exitPoint.connectedNode != null)
        {
            returnPath = network.ShortestPathToTarget(firstTarget, exitPoint.connectedNode);
            if (returnPath != null)
            {
                returnPathIndex = 0; // firstTarget is at index 0 of this path
            }
        }
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        if (network == null || fromNode == null || toNode == null)
        {
            Despawn();
        }

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;
    }

    private void OnEnable()
    {
        lifeTimer = 0f;
    }

    private void Despawn()
    {
        if (onDespawn != null) onDespawn(this);
        else Destroy(gameObject);
    }

    private float stunTimer = 0f;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            stunTimer = 1.5f;
        }
    }

    private void FixedUpdate()
    {
        lifeTimer += Time.fixedDeltaTime;
        if (lifeTimer >= maxLifetime)
        {
            Despawn();
            return;
        }

        if (stunTimer > 0f)
        {
            stunTimer -= Time.fixedDeltaTime;
            return;
        }

        if (toNode == null && exitPoint == null)
        {
            Despawn();
            return;
        }

        var targetPoint = toNode != null ? network.GetLanePoint(fromNode, toNode) : exitPoint.transform.position;
        var toTarget = targetPoint - rb.position;
        toTarget.y = 0f;

        var forward = transform.forward;
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            var desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            var newRotation = Quaternion.RotateTowards(rb.rotation, desiredRotation, turnSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
            forward = newRotation * Vector3.forward;
        }

        rb.MovePosition(rb.position + forward * speed * Time.fixedDeltaTime);

        if (Vector3.Distance(rb.position, targetPoint) <= nodeArriveRadius)
        {
            if (toNode != null) ArriveAtNode();
            else Despawn(); // arrived at the exit spawn marker itself
        }

        CheckPlayerNearMiss();
    }

    private void CheckPlayerNearMiss()
    {
        if (hasReactedToPlayer || playerTransform == null) return;

        if (Vector3.Distance(transform.position, playerTransform.position) < nearMissDistance)
        {
            // Check if player is somewhat in front of the NPC car
            Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, dirToPlayer) > 0.4f)
            {
                hasReactedToPlayer = true;
                StartCoroutine(ReactionRoutine());
            }
        }
    }

    private System.Collections.IEnumerator ReactionRoutine()
    {
        // Play honk sound (Make sure "car_horn" exists in AudioManager)
        if (Code.Scripts.Audio.AudioManager.Instance != null)
        {
            Code.Scripts.Audio.AudioManager.Instance.PlaySFX("car_horn");
        }

        // Visual wiggle of anger
        Transform visual = transform.Find("CAR-BASE");
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);

        if (visual != null)
        {
            Vector3 originalScale = visual.localScale;
            float timer = 0f;
            while (timer < 0.6f)
            {
                timer += Time.deltaTime;
                // Rapid bouncing / squashing to look "angry"
                float scaleY = 1f + Mathf.Sin(timer * 50f) * 0.15f;
                visual.localScale = new Vector3(originalScale.x, originalScale.y * scaleY, originalScale.z);
                yield return null;
            }
            visual.localScale = originalScale;
        }

        // Cooldown before they can get angry again
        yield return new WaitForSeconds(3f);
        hasReactedToPlayer = false;
    }

    private void ArriveAtNode()
    {
        AdvanceTarget();

        // Skip microscopic bridge segments caused by prefab snapping to prevent 
        // erratic lane offset calculations that cause 360-degree spins.
        while (toNode != null && Vector3.Distance(fromNode.transform.position, toNode.transform.position) < 2.0f)
        {
            AdvanceTarget();
        }
    }

    private void AdvanceTarget()
    {
        var arrivedAt = toNode;
        var cameFrom = fromNode;
        fromNode = arrivedAt;

        if (returnPath != null)
        {
            returnPathIndex++;
            toNode = returnPathIndex < returnPath.Count ? returnPath[returnPathIndex] : null;
            return;
        }

        // Fallback: If no exit was found at spawn, wander aimlessly but avoid U-turns if possible.
        var options = arrivedAt.connections.FindAll(n => n != cameFrom);
        if (options.Count == 0) options = arrivedAt.connections;
        
        toNode = options.Count > 0 ? options[Random.Range(0, options.Count)] : null;
    }
}
