using UnityEditor;
using UnityEngine;

// Scene view tool for building the traffic road graph without touching the Inspector.
// Active whenever a RoadNetwork GameObject is selected:
//   Ctrl+Click on the ground   -> create a new node there.
//   Click a node, click again  -> connect/disconnect that street (click chains: A, B, C
//                                 connects A-B then B-C so a row of nodes goes fast).
//   Click the selected node    -> cancel the pending connection.
//   Shift+Click a node         -> toggle it as a spawn point (cyan <-> orange).
// Moving a node still uses Unity's normal move gizmo - select it in the Hierarchy and
// drag, same as any other GameObject.
[CustomEditor(typeof(RoadNetwork))]
public class RoadNetworkEditor : Editor
{
    private const float NodePickRadiusPixels = 20f;
    private const string NodeNamePrefix = "Node_";

    private RoadNode pendingNode;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Ctrl+Click en el suelo: crear nodo.\n" +
            "Click en un nodo y luego en otro: conectar/desconectar.\n" +
            "Shift+Click en un nodo: marcar/desmarcar como spawn.",
            MessageType.Info);

        if (pendingNode != null)
        {
            EditorGUILayout.LabelField("Nodo seleccionado:", pendingNode.name);
            if (GUILayout.Button("Eliminar nodo seleccionado"))
            {
                DeleteNode(pendingNode);
                pendingNode = null;
            }
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

        var clicked = PickNode(network, e.mousePosition);
        if (clicked == null) return;

        if (e.shift)
        {
            Undo.RecordObject(clicked, "Toggle Spawn Point");
            clicked.isSpawnPoint = !clicked.isSpawnPoint;
            EditorUtility.SetDirty(clicked);
            e.Use();
            return;
        }

        if (pendingNode == null)
        {
            pendingNode = clicked;
        }
        else if (pendingNode == clicked)
        {
            pendingNode = null;
        }
        else
        {
            Undo.RecordObject(pendingNode, "Toggle Road Connection");
            Undo.RecordObject(clicked, "Toggle Road Connection");
            if (pendingNode.IsConnectedTo(clicked)) pendingNode.Disconnect(clicked);
            else pendingNode.Connect(clicked);
            EditorUtility.SetDirty(pendingNode);
            EditorUtility.SetDirty(clicked);
            pendingNode = clicked;
        }

        e.Use();
        Repaint();
    }

    private static RoadNode PickNode(RoadNetwork network, Vector2 mousePosition)
    {
        RoadNode closest = null;
        var closestDist = NodePickRadiusPixels;

        foreach (var node in network.Nodes)
        {
            if (node == null) continue;
            var guiPoint = HandleUtility.WorldToGUIPoint(node.transform.position);
            var dist = Vector2.Distance(guiPoint, mousePosition);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = node;
            }
        }

        return closest;
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
        Selection.activeGameObject = network.gameObject;
    }

    private static void DeleteNode(RoadNode node)
    {
        Undo.DestroyObjectImmediate(node.gameObject);
    }
}
