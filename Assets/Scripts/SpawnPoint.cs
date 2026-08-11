using UnityEngine;

// Traffic entry/exit marker, separate from the RoadNode path graph. Lives whereever a
// car should physically appear or disappear (usually the edge of the drivable area) and
// connects to exactly one RoadNode - the point where it feeds into (Entrada) or drains
// out of (Salida) the network. CarSpawner only spawns from Entrada points; CarObstacle
// only heads toward Salida points to despawn. Unlike RoadNode, a SpawnPoint is never a
// waypoint cars pass through mid-route.
public class SpawnPoint : MonoBehaviour
{
    public enum Role { Entrada, Salida }

    public Role role = Role.Entrada;
    public RoadNode connectedNode;
}
