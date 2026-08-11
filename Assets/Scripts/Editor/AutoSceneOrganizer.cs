using UnityEditor;
using UnityEngine;
using System.IO;

public static class AutoSceneOrganizer
{
    [InitializeOnLoadMethod]
    public static void OrganizeNow()
    {
        if (SessionState.GetBool("SceneOrganized_V2", false)) return;
        SessionState.SetBool("SceneOrganized_V2", true);

        Undo.RegisterFullObjectHierarchyUndo(null, "Organizar Escena Completa");
        
        GameObject envFolder = GetOrCreateFolder("--- ENTORNO / CALLES ---");
        GameObject nodesFolder = GetOrCreateFolder("--- ROAD NETWORK ---");
        GameObject lightsFolder = GetOrCreateFolder("--- LUCES Y CÁMARAS ---");
        GameObject systemsFolder = GetOrCreateFolder("--- SISTEMAS / MANAGERS ---");
        GameObject othersFolder = GetOrCreateFolder("--- OTROS ---");

        var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        int movedCount = 0;

        foreach (var go in rootObjects)
        {
            if (go == envFolder || go == nodesFolder || go == lightsFolder || go == systemsFolder || go == othersFolder)
                continue;

            if (go.GetComponent<Light>() != null || go.GetComponent<Camera>() != null || go.GetComponent<ReflectionProbe>() != null) {
                Undo.SetTransformParent(go.transform, lightsFolder.transform, "Mover a Luces");
                movedCount++;
                continue;
            }

            if (go.GetComponent<RoadNetwork>() != null || go.name.Contains("RoadNetwork") || go.name.Contains("Node_") || go.name.Contains("Spawn_")) {
                Undo.SetTransformParent(go.transform, nodesFolder.transform, "Mover a Nodos");
                movedCount++;
                continue;
            }
            
            if (go.GetComponent<CarSpawner>() != null || go.name.Contains("Manager") || go.name.Contains("System")) {
                Undo.SetTransformParent(go.transform, systemsFolder.transform, "Mover a Sistemas");
                movedCount++;
                continue;
            }

            if (go.GetComponent<MeshRenderer>() != null || go.name.ToLower().Contains("street") || go.name.ToLower().Contains("building") || go.name.ToLower().Contains("car")) {
                Undo.SetTransformParent(go.transform, envFolder.transform, "Mover a Entorno");
                movedCount++;
                continue;
            }

            Undo.SetTransformParent(go.transform, othersFolder.transform, "Mover a Otros");
            movedCount++;
        }
        
        Debug.Log($"¡La escena ha sido organizada mágicamente! Se movieron {movedCount} objetos.");

        // Se auto-elimina para no dejar rastro
        EditorApplication.delayCall += () =>
        {
            string path = "Assets/Scripts/Editor/AutoSceneOrganizer.cs";
            if (File.Exists(path))
            {
                File.Delete(path);
                string metaPath = path + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
                AssetDatabase.Refresh();
            }
        };
    }

    private static GameObject GetOrCreateFolder(string folderName)
    {
        GameObject folder = GameObject.Find(folderName);
        if (folder == null)
        {
            folder = new GameObject(folderName);
            Undo.RegisterCreatedObjectUndo(folder, "Crear Carpeta");
            folder.transform.SetAsFirstSibling();
        }
        return folder;
    }
}
