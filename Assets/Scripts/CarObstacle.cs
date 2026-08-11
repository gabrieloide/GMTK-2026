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

    private Rigidbody rb;
    private RoadNetwork network;
    private RoadNode fromNode;
    private RoadNode toNode;
    private List<RoadNode> returnPath;
    private int returnPathIndex;
    private SpawnPoint exitPoint;
    private float lifeTimer;

    // firstTarget is picked by the caller (CarSpawner) rather than chosen internally so
    // the car can be instantiated already facing the same node it's about to drive to.
    public void Init(RoadNetwork roadNetwork, RoadNode startNode, RoadNode firstTarget, float moveSpeed)
    {
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
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        lifeTimer += Time.fixedDeltaTime;
        if (lifeTimer >= maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (toNode == null && exitPoint == null)
        {
            Destroy(gameObject);
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
            else Destroy(gameObject); // arrived at the exit spawn marker itself
        }
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
