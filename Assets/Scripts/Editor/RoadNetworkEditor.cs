using UnityEditor;
using UnityEngine;

// Scene view tool for building the traffic road graph without touching the Inspector.
// Active whenever a RoadNetwork GameObject is selected:
//   Ctrl+Click on the ground   -> create a new RoadNode there.
//   Alt+Click on the ground    -> create a new SpawnPoint there (starts as Entrada).
//   Click a node, click again  -> connect/disconnect that street (click chains: A, B, C
//                                 connects A-B then B-C so a row of nodes goes fast).
//   Click a spawn, click a node (or vice versa) -> attach that spawn point to the node
//                                 (a spawn point only ever has one connected node).
//   Click the selected element -> cancel the pending selection.
//   Shift+Click a spawn point  -> toggle its role (Entrada <-> Salida, green <-> red).
// Moving a node/spawn still uses Unity's normal move gizmo - select it in the Hierarchy
// and drag, same as any other GameObject.
[CustomEditor(typeof(RoadNetwork))]
public class RoadNetworkEditor : Editor
{
    private const float NodePickRadiusPixels = 20f;
    private const string NodeNamePrefix = "Node_";
    private const string SpawnNamePrefix = "Spawn_";

    private RoadNode pendingNode;
    private SpawnPoint pendingSpawn;

    private bool showNodes = false;
    private bool showSpawns = false;
    private Vector2 scrollPosNodes;
    private Vector2 scrollPosSpawns;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var network = (RoadNetwork)target;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Ctrl+Click en el suelo: crear nodo de camino.\n" +
            "Alt+Click en el suelo: crear punto de spawn (entrada/salida).\n" +
            "Click en un elemento y luego en otro: conectar/desconectar (nodo-nodo) o " +
            "enganchar (spawn-nodo).\n" +
            "Shift+Click en un punto de spawn: alternar Entrada/Salida.",
            MessageType.Info);

        if (pendingNode != null)
        {
            EditorGUILayout.LabelField("Nodo seleccionado:", pendingNode.name);
            if (GUILayout.Button("Eliminar nodo seleccionado"))
            {
                DeleteObject(pendingNode.gameObject);
                pendingNode = null;
            }
        }

        if (pendingSpawn != null)
        {
            EditorGUILayout.LabelField("Spawn seleccionado:", $"{pendingSpawn.name} ({pendingSpawn.role})");
            if (GUILayout.Button("Eliminar spawn seleccionado"))
            {
                DeleteObject(pendingSpawn.gameObject);
                pendingSpawn = null;
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Auto-Conectar Nodos Cercanos (Prefabs)", GUILayout.Height(30)))
        {
            AutoConnectNodes(network);
        }
        EditorGUILayout.HelpBox("Usa el botón de arriba si armaste la ciudad con prefabs. Conectará automáticamente todos los nodos que estén pegados o súper cerca (a menos de 0.5 unidades de distancia).", MessageType.Info);

        EditorGUILayout.Space();
        showNodes = EditorGUILayout.Foldout(showNodes, $"Nodos ({network.Nodes.Count})", true);
        if (showNodes)
        {
            scrollPosNodes = EditorGUILayout.BeginScrollView(scrollPosNodes, GUILayout.MaxHeight(200));
            foreach (var n in network.Nodes)
            {
                if (n == null) continue;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(n, typeof(RoadNode), true);
                if (GUILayout.Button("Ver", GUILayout.Width(50)))
                {
                    Selection.activeGameObject = n.gameObject;
                    SceneView.FrameLastActiveSceneView();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space();
        showSpawns = EditorGUILayout.Foldout(showSpawns, $"Spawn Points ({network.SpawnPoints.Count})", true);
        if (showSpawns)
        {
            scrollPosSpawns = EditorGUILayout.BeginScrollView(scrollPosSpawns, GUILayout.MaxHeight(200));
            foreach (var s in network.SpawnPoints)
            {
                if (s == null) continue;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(s, typeof(SpawnPoint), true);
                if (GUILayout.Button("Ver", GUILayout.Width(50)))
                {
                    Selection.activeGameObject = s.gameObject;
                    SceneView.FrameLastActiveSceneView();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void OnSceneGUI()
    {
        var network = (RoadNetwork)target;
        var e = Event.current;

        if (pendingNode != null)
        {
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(pendingNode.transform.position, Vector3.up, 0.85f);
        }
        if (pendingSpawn != null)
        {
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(pendingSpawn.transform.position, Vector3.up, 0.85f);
        }

        if (e.type != EventType.MouseDown || e.button != 0) return;

        if (e.control)
        {
            if (TryRaycastGround(network, e.mousePosition, out var point))
            {
                CreateNode(network, point);
                e.Use();
            }
            return;
        }

        if (e.alt)
        {
            if (TryRaycastGround(network, e.mousePosition, out var point))
            {
                CreateSpawnPoint(network, point);
                e.Use();
            }
            return;
        }

        if (!TryPickElement(network, e.mousePosition, out var clickedNode, out var clickedSpawn)) return;

        if (e.shift)
        {
            if (clickedSpawn != null)
            {
                Undo.RecordObject(clickedSpawn, "Toggle Spawn Role");
                clickedSpawn.role = clickedSpawn.role == SpawnPoint.Role.Entrada ? SpawnPoint.Role.Salida : SpawnPoint.Role.Entrada;
                EditorUtility.SetDirty(clickedSpawn);
            }
            e.Use();
            return;
        }

        if (clickedSpawn != null)
        {
            if (pendingNode != null)
            {
                Undo.RecordObject(clickedSpawn, "Connect Spawn Point");
                clickedSpawn.connectedNode = pendingNode;
                EditorUtility.SetDirty(clickedSpawn);
                pendingNode = null;
                pendingSpawn = null;
            }
            else if (pendingSpawn == clickedSpawn)
            {
                pendingSpawn = null;
            }
            else
            {
                pendingSpawn = clickedSpawn;
                pendingNode = null;
            }

            e.Use();
            Repaint();
            return;
        }

        // clickedNode != null from here on.
        if (pendingSpawn != null)
        {
            Undo.RecordObject(pendingSpawn, "Connect Spawn Point");
            pendingSpawn.connectedNode = clickedNode;
            EditorUtility.SetDirty(pendingSpawn);
            pendingSpawn = null;
            pendingNode = null;
            e.Use();
            Repaint();
            return;
        }

        if (pendingNode == null)
        {
            pendingNode = clickedNode;
        }
        else if (pendingNode == clickedNode)
        {
            pendingNode = null;
        }
        else
        {
            Undo.RecordObject(pendingNode, "Toggle Road Connection");
            Undo.RecordObject(clickedNode, "Toggle Road Connection");
            if (pendingNode.IsConnectedTo(clickedNode)) pendingNode.Disconnect(clickedNode);
            else pendingNode.Connect(clickedNode);
            EditorUtility.SetDirty(pendingNode);
            EditorUtility.SetDirty(clickedNode);
            pendingNode = clickedNode;
        }

        e.Use();
        Repaint();
    }

    // Picks whichever of a RoadNode or SpawnPoint is closest to the mouse, within pick
    // radius. Only one of the two out params is ever non-null.
    private static bool TryPickElement(RoadNetwork network, Vector2 mousePosition, out RoadNode node, out SpawnPoint spawn)
    {
        node = null;
        spawn = null;
        var bestDist = NodePickRadiusPixels;

        foreach (var n in network.Nodes)
        {
            if (n == null) continue;
            var dist = Vector2.Distance(HandleUtility.WorldToGUIPoint(n.transform.position), mousePosition);
            if (dist < bestDist)
            {
                bestDist = dist;
                node = n;
                spawn = null;
            }
        }

        foreach (var s in network.SpawnPoints)
        {
            if (s == null) continue;
            var dist = Vector2.Distance(HandleUtility.WorldToGUIPoint(s.transform.position), mousePosition);
            if (dist < bestDist)
            {
                bestDist = dist;
                spawn = s;
                node = null;
            }
        }

        return node != null || spawn != null;
    }

    private static bool TryRaycastGround(RoadNetwork network, Vector2 mousePosition, out Vector3 point)
    {
        var ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        if (Physics.Raycast(ray, out var hit))
        {
            point = hit.point;
            return true;
        }

        var plane = new Plane(Vector3.up, network.transform.position);
        if (plane.Raycast(ray, out var enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }

        point = default;
        return false;
    }

    private void CreateNode(RoadNetwork network, Vector3 position)
    {
        var go = new GameObject(NodeNamePrefix + network.Nodes.Count);
        Undo.RegisterCreatedObjectUndo(go, "Create Road Node");
        go.transform.SetParent(network.transform);
        go.transform.position = position;
        var node = go.AddComponent<RoadNode>();

        if (pendingNode != null)
        {
            Undo.RecordObject(pendingNode, "Connect Road Node");
            pendingNode.Connect(node);
            EditorUtility.SetDirty(pendingNode);
        }

        pendingNode = node;
        pendingSpawn = null;
        Selection.activeGameObject = network.gameObject;
    }

    private void CreateSpawnPoint(RoadNetwork network, Vector3 position)
    {
        var go = new GameObject(SpawnNamePrefix + network.SpawnPoints.Count);
        Undo.RegisterCreatedObjectUndo(go, "Create Spawn Point");
        go.transform.SetParent(network.transform);
        go.transform.position = position;
        var spawn = go.AddComponent<SpawnPoint>();

        if (pendingNode != null)
        {
            Undo.RecordObject(spawn, "Connect Spawn Point");
            spawn.connectedNode = pendingNode;
        }

        pendingSpawn = spawn;
        pendingNode = null;
        Selection.activeGameObject = network.gameObject;
    }

    private void AutoConnectNodes(RoadNetwork network)
    {
        var nodes = network.Nodes;
        int connectionsMade = 0;
        
        Undo.RecordObjects(nodes.ToArray(), "Auto Connect Nodes");

        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                var n1 = nodes[i];
                var n2 = nodes[j];
                
                if (n1 == null || n2 == null) continue;
                
                if (Vector3.Distance(n1.transform.position, n2.transform.position) < 0.5f)
                {
                    if (!n1.IsConnectedTo(n2))
                    {
                        n1.Connect(n2);
                        EditorUtility.SetDirty(n1);
                        EditorUtility.SetDirty(n2);
                        connectionsMade++;
                    }
                }
            }
        }
        
        var spawns = network.SpawnPoints;
        int spawnConnections = 0;
        foreach (var spawn in spawns)
        {
            if (spawn == null || spawn.connectedNode != null) continue;
            
            RoadNode closest = null;
            float closestDist = float.MaxValue;
            foreach (var node in nodes)
            {
                float d = Vector3.Distance(spawn.transform.position, node.transform.position);
                if (d < 2.0f && d < closestDist)
                {
                    closestDist = d;
                    closest = node;
                }
            }
            
            if (closest != null)
            {
                Undo.RecordObject(spawn, "Auto Connect Spawn");
                spawn.connectedNode = closest;
                EditorUtility.SetDirty(spawn);
                spawnConnections++;
            }
        }
        
        Debug.Log($"[RoadNetwork] Auto-conexión terminada. {connectionsMade} conexiones entre nodos y {spawnConnections} Spawns conectados.");
    }

    private static void DeleteObject(GameObject go)
    {
        Undo.DestroyObjectImmediate(go);
    }
}
