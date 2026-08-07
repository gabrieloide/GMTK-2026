using System.Collections.Generic;
using UnityEngine;

// A single waypoint in the RoadNetwork graph. Lives as a child GameObject under a
// RoadNetwork so it can be dragged around with Unity's normal move gizmo; connections
// are edited via RoadNetworkEditor (click a node, then click another) rather than by
// hand in the Inspector.
public class RoadNode : MonoBehaviour
{
    public bool isSpawnPoint;
    public List<RoadNode> connections = new List<RoadNode>();

    public void Connect(RoadNode other)
    {
        if (other == null || other == this) return;
        if (!connections.Contains(other)) connections.Add(other);
        if (!other.connections.Contains(this)) other.connections.Add(this);
    }

    public void Disconnect(RoadNode other)
    {
        if (other == null) return;
        connections.Remove(other);
        other.connections.Remove(this);
    }

    public bool IsConnectedTo(RoadNode other)
    {
        return other != null && connections.Contains(other);
    }

    private void OnDestroy()
    {
        // Copy: Disconnect mutates the other node's list, which would otherwise
        // invalidate the enumerator of connections while we're iterating it.
        foreach (var other in connections.ToArray())
        {
            if (other != null) other.connections.Remove(this);
        }
    }
}
