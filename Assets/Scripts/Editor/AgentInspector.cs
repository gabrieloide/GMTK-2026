using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System.Linq;

public static class AgentInspector
{
    [MenuItem("Tools/Agent Inspector")]
    public static void InspectNetwork()
    {
        var network = Object.FindAnyObjectByType<RoadNetwork>();
        if (network == null)
        {
            File.WriteAllText("E:/Unity/Projects/GMTK 2026/road_diag.txt", "No RoadNetwork found.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        var nodes = network.Nodes;
        var spawns = network.SpawnPoints;

        sb.AppendLine($"Total Nodes: {nodes.Count}");
        sb.AppendLine($"Total Spawns: {spawns.Count}");
        
        int deadEnds = 0;
        foreach (var node in nodes)
        {
            if (node == null) continue;
            if (node.connections == null) continue;
            
            node.connections.RemoveAll(x => x == null);
            if (node.connections.Count == 0)
            {
                sb.AppendLine($"DEAD END (0 conexiones): Nodo '{node.name}' en la posicion {node.transform.position}");
                deadEnds++;
            }
            else if (node.connections.Count == 1)
            {
                sb.AppendLine($"CALLEJON (1 conexion): Nodo '{node.name}' solo va hacia '{node.connections[0].name}'");
            }
        }
        
        foreach (var spawn in spawns)
        {
            if (spawn.connectedNode == null)
                sb.AppendLine($"SPAWN DESCONECTADO: '{spawn.name}' ({spawn.role}) en {spawn.transform.position}");
            else
                sb.AppendLine($"SPAWN OK: '{spawn.name}' ({spawn.role}) conectado a '{spawn.connectedNode.name}'");
        }
        
        File.WriteAllText("E:/Unity/Projects/GMTK 2026/road_diag.txt", sb.ToString());
        Debug.Log("Diagnostics saved to road_diag.txt");
    }
}
