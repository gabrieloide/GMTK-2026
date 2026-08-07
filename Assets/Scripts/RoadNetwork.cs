using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Owns the waypoint graph traffic cars drive on: a set of RoadNode children plus their
// connections. Draws the whole graph as gizmos (spawn nodes vs regular nodes, two lanes
// per street so both directions are visible) and provides the queries CarObstacle needs
// to wander the graph and, later, find its way back out to a spawn node.
// Building/editing the graph itself happens in RoadNetworkEditor (Scene view tool), not
// here - this component only stores and reads it.
public class RoadNetwork : MonoBehaviour
{
    [SerializeField] private float laneOffset = 1.2f;
    [SerializeField] private float nodeGizmoRadius = 0.6f;
    [SerializeField] private Color normalNodeColor = Color.cyan;
    [SerializeField] private Color spawnNodeColor = new Color(1f, 0.55f, 0f);

    public float LaneOffset => laneOffset;

    public List<RoadNode> Nodes => GetComponentsInChildren<RoadNode>(true).ToList();

    public RoadNode GetRandomSpawnNode()
    {
        var spawns = Nodes.Where(n => n.isSpawnPoint).ToList();
        return spawns.Count == 0 ? null : spawns[Random.Range(0, spawns.Count)];
    }

    // Picks a random neighbor of current, preferring not to double back to cameFrom
    // when another option exists (keeps cars from constantly U-turning).
    public RoadNode GetRandomNeighbor(RoadNode current, RoadNode cameFrom)
    {
        if (current == null || current.connections.Count == 0) return null;

        var options = current.connections.Where(n => n != cameFrom).ToList();
        if (options.Count == 0) options = current.connections;

        return options[Random.Range(0, options.Count)];
    }

    // Dijkstra over the graph (edge weight = euclidean distance) from `from` to the
    // nearest node flagged isSpawnPoint. Returns the path including `from` as the first
    // element and the chosen spawn node as the last, or null if none is reachable.
    public List<RoadNode> ShortestPathToNearestSpawn(RoadNode from)
    {
        if (from == null) return null;

        var dist = new Dictionary<RoadNode, float> { [from] = 0f };
        var prev = new Dictionary<RoadNode, RoadNode>();
        var unvisited = new HashSet<RoadNode>(Nodes);

        while (unvisited.Count > 0)
        {
            RoadNode current = null;
            var bestDist = float.PositiveInfinity;
            foreach (var node in unvisited)
            {
                if (dist.TryGetValue(node, out var d) && d < bestDist)
                {
                    bestDist = d;
                    current = node;
                }
            }

            if (current == null) break;
            unvisited.Remove(current);

            if (current.isSpawnPoint && current != from)
            {
                var path = new List<RoadNode> { current };
                while (prev.TryGetValue(path[0], out var p)) path.Insert(0, p);
                return path;
            }

            foreach (var neighbor in current.connections)
            {
                if (!unvisited.Contains(neighbor)) continue;
                var alt = dist[current] + Vector3.Distance(current.transform.position, neighbor.transform.position);
                if (!dist.TryGetValue(neighbor, out var existing) || alt < existing)
                {
                    dist[neighbor] = alt;
                    prev[neighbor] = current;
                }
            }
        }

        return null;
    }

    // Perpendicular offset for the lane a car travels in when moving from `from` toward
    // `to` - each direction of a street gets its own offset point so opposing traffic
    // doesn't share a centerline.
    public Vector3 GetLanePoint(RoadNode from, RoadNode to)
    {
        var a = from.transform.position;
        var b = to.transform.position;
        var dir = (b - a);
        if (dir.sqrMagnitude < 0.0001f) return b;
        dir.Normalize();
        var right = Vector3.Cross(Vector3.up, dir).normalized;
        return b + right * laneOffset;
    }

    private void OnDrawGizmos()
    {
        var nodes = Nodes;
        var drawnEdges = new HashSet<(RoadNode, RoadNode)>();

        foreach (var node in nodes)
        {
            if (node == null) continue;

            Gizmos.color = node.isSpawnPoint ? spawnNodeColor : normalNodeColor;
            Gizmos.DrawSphere(node.transform.position, nodeGizmoRadius);

            foreach (var other in node.connections)
            {
                if (other == null) continue;
                var key = node.GetInstanceID() < other.GetInstanceID() ? (node, other) : (other, node);
                if (!drawnEdges.Add(key)) continue;

                DrawLaneArrow(node, other);
                DrawLaneArrow(other, node);
            }
        }
    }

    private void DrawLaneArrow(RoadNode from, RoadNode to)
    {
        var start = from.transform.position;
        var end = GetLanePoint(from, to);

        // Offset the start point onto the same lane so the line doesn't run through the
        // node sphere at the centerline.
        var dir = (to.transform.position - from.transform.position).normalized;
        var right = Vector3.Cross(Vector3.up, dir).normalized;
        var laneStart = start + right * laneOffset;

        Gizmos.color = to.isSpawnPoint || from.isSpawnPoint ? spawnNodeColor : normalNodeColor;
        Gizmos.DrawLine(laneStart, end);

        var mid = Vector3.Lerp(laneStart, end, 0.5f);
        var back = -dir;
        var arrowRight = right;
        var arrowSize = 0.5f;
        Gizmos.DrawLine(mid, mid + (back + arrowRight) * arrowSize);
        Gizmos.DrawLine(mid, mid + (back - arrowRight) * arrowSize);
    }
}
