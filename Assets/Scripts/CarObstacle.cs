using System.Collections.Generic;
using UnityEngine;

// Traffic car AI: wanders the RoadNetwork graph node by node, steering smoothly toward
// each next waypoint (offset into its own lane so opposing traffic doesn't share a
// centerline - see RoadNetwork.GetLanePoint) instead of snapping heading instantly.
// After a random number of hops it stops wandering, paths to the nearest spawn node via
// RoadNetwork.ShortestPathToNearestSpawn, and despawns on arrival - which may well be a
// different spawn than the one it entered from.
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
    private int hopsRemaining;
    private List<RoadNode> returnPath;
    private int returnPathIndex;
    private float lifeTimer;

    // firstTarget is picked by the caller (CarSpawner) rather than chosen internally so
    // the car can be instantiated already facing the same node it's about to drive to.
    public void Init(RoadNetwork roadNetwork, RoadNode startNode, RoadNode firstTarget, float moveSpeed)
    {
        network = roadNetwork;
        speed = moveSpeed;
        fromNode = startNode;
        toNode = firstTarget;
        hopsRemaining = Random.Range(minWanderHops, maxWanderHops + 1);
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

        if (toNode == null) return;

        var targetPoint = network.GetLanePoint(fromNode, toNode);
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
            ArriveAtNode();
        }
    }

    private void ArriveAtNode()
    {
        var arrivedAt = toNode;
        var cameFrom = fromNode;
        fromNode = arrivedAt;

        if (returnPath != null)
        {
            returnPathIndex++;
            if (returnPathIndex >= returnPath.Count)
            {
                Destroy(gameObject);
                return;
            }
            toNode = returnPath[returnPathIndex];
            return;
        }

        hopsRemaining--;
        if (hopsRemaining <= 0)
        {
            returnPath = network.ShortestPathToNearestSpawn(arrivedAt);
            if (returnPath != null && returnPath.Count > 1)
            {
                returnPathIndex = 1;
                toNode = returnPath[returnPathIndex];
                return;
            }

            // Already at (or no path to) a spawn node - nothing left to drive to.
            Destroy(gameObject);
            return;
        }

        toNode = network.GetRandomNeighbor(arrivedAt, cameFrom);
        if (toNode == null) Destroy(gameObject);
    }
}
