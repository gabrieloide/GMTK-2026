using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Owns the waypoint graph traffic cars drive on: a set of RoadNode children plus their
// connections, and a set of SpawnPoint children marking where cars enter (Entrada) or
// exit (Salida) the network. Draws the whole graph as gizmos - two lanes per street so
// both directions are visible, plus a colored arrow per spawn point showing which way
// traffic flows through it - and provides the queries CarSpawner/CarObstacle need to
// spawn cars, wander the graph, and find their way back out to an exit.
// Building/editing the graph itself happens in RoadNetworkEditor (Scene view tool), not
// here - this component only stores and reads it.
public class RoadNetwork : MonoBehaviour
{
    [SerializeField] private float laneOffset = 3f;
    [SerializeField] private float nodeGizmoRadius = 0.6f;
    [SerializeField] private float spawnGizmoSize = 0.8f;
    [SerializeField] private Color normalNodeColor = Color.cyan;
    [SerializeField] private Color entradaColor = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color salidaColor = new Color(0.9f, 0.2f, 0.2f);

    public float LaneOffset => laneOffset;

    public List<RoadNode> Nodes => FindObjectsByType<RoadNode>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
    public List<SpawnPoint> SpawnPoints => FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();

    public SpawnPoint GetRandomEntradaSpawnPoint()
    {
        var options = SpawnPoints.Where(s => s.role == SpawnPoint.Role.Entrada && s.connectedNode != null).ToList();
        return options.Count == 0 ? null : options[Random.Range(0, options.Count)];
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
    // nearest RoadNode that has a Salida SpawnPoint attached. Returns the path including
    // `from` as the first element and that node as the last, plus the SpawnPoint found -
    // or (null, null) if no exit is reachable. The caller still has to drive the final
    // stretch from that last node to exitPoint.transform.position itself.
    public SpawnPoint GetRandomSalidaSpawnPoint(Vector3 fromPos = default, float minDistance = 0f)
    {
        var validSpawns = SpawnPoints.Where(s => s.role == SpawnPoint.Role.Salida && s.connectedNode != null).ToList();
        if (validSpawns.Count == 0) return null;

        var farSpawns = validSpawns.Where(s => Vector3.Distance(fromPos, s.transform.position) >= minDistance).ToList();
        
        // Fallback: If all exits are closer than minDistance, try to just avoid the exact same entrance (distance > 2.0f)
        if (farSpawns.Count == 0)
        {
            farSpawns = validSpawns.Where(s => Vector3.Distance(fromPos, s.transform.position) > 2.0f).ToList();
            if (farSpawns.Count == 0) farSpawns = validSpawns; // Extreme fallback
        }

        return farSpawns[Random.Range(0, farSpawns.Count)];
    }

    public List<RoadNode> ShortestPathToTarget(RoadNode from, RoadNode target)
    {
        if (from == null || target == null) return null;

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
            if (current == target)
            {
                var path = new List<RoadNode> { current };
                while (prev.TryGetValue(path[0], out var p)) path.Insert(0, p);
                return path;
            }
            unvisited.Remove(current);

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

            Gizmos.color = normalNodeColor;
            Gizmos.DrawSphere(node.transform.position, nodeGizmoRadius);

            foreach (var other in node.connections)
            {
                if (other == null) continue;
                if (drawnEdges.Contains((node, other)) || drawnEdges.Contains((other, node))) continue;
                drawnEdges.Add((node, other));

                DrawLaneArrow(node, other);
                DrawLaneArrow(other, node);
            }
        }

        foreach (var spawn in SpawnPoints)
        {
            if (spawn != null) DrawSpawnPoint(spawn);
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

        Gizmos.color = normalNodeColor;
        Gizmos.DrawLine(laneStart, end);

        var mid = Vector3.Lerp(laneStart, end, 0.5f);
        var back = -dir;
        var arrowRight = right;
        var arrowSize = 0.5f;
        Gizmos.DrawLine(mid, mid + (back + arrowRight) * arrowSize);
        Gizmos.DrawLine(mid, mid + (back - arrowRight) * arrowSize);
    }

    // Draws the spawn marker as a colored sphere (green = Entrada, red = Salida) plus a
    // line to its connected node with an arrowhead in the direction traffic actually
    // flows: into the network for Entrada, out of it for Salida.
    private void DrawSpawnPoint(SpawnPoint spawn)
    {
        var isEntrada = spawn.role == SpawnPoint.Role.Entrada;
        Gizmos.color = isEntrada ? entradaColor : salidaColor;
        Gizmos.DrawSphere(spawn.transform.position, spawnGizmoSize * 0.5f);

        if (spawn.connectedNode == null) return;

        var spawnPos = spawn.transform.position;
        var nodePos = spawn.connectedNode.transform.position;
        Gizmos.DrawLine(spawnPos, nodePos);

        var from = isEntrada ? spawnPos : nodePos;
        var to = isEntrada ? nodePos : spawnPos;
        var dir = (to - from);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();
        var right = Vector3.Cross(Vector3.up, dir).normalized;

        var tip = Vector3.Lerp(from, to, 0.5f);
        var back = -dir;
        Gizmos.DrawLine(tip, tip + (back + right) * spawnGizmoSize);
        Gizmos.DrawLine(tip, tip + (back - right) * spawnGizmoSize);
    }
}
