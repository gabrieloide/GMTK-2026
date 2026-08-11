using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;

public class SceneOrganizerTool
{
    [MenuItem("Tools/Organize Scene")]
    public static void OrganizeNow()
    {
        GameObject envFolder = GetOrCreateFolder("--- ENVIRONMENT ---");
        GameObject nodesFolder = GetOrCreateFolder("--- ROAD NETWORK ---");
        GameObject lightsFolder = GetOrCreateFolder("--- LIGHTS & CAMERAS ---");
        GameObject systemsFolder = GetOrCreateFolder("--- SYSTEMS & MANAGERS ---");
        GameObject othersFolder = GetOrCreateFolder("--- OTHERS ---");

        var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        int movedCount = 0;

        foreach (var go in rootObjects)
        {
            // Ignore main folders
            if (go == envFolder || go == nodesFolder || go == lightsFolder || go == systemsFolder || go == othersFolder)
                continue;

            string lowerName = go.name.ToLower();

            // LIGHTS & CAMERAS
            if (go.GetComponent<Light>() != null || go.GetComponent<Camera>() != null || go.GetComponent<ReflectionProbe>() != null) {
                MoveToSubfolder(go, lightsFolder);
                movedCount++;
                continue;
            }

            // ROAD NETWORK
            if (go.GetComponent<RoadNetwork>() != null || lowerName.Contains("roadnetwork") || lowerName.Contains("node_") || lowerName.Contains("spawn_")) {
                MoveToSubfolder(go, nodesFolder);
                movedCount++;
                continue;
            }
            
            // SYSTEMS & MANAGERS
            if (go.GetComponent<CarSpawner>() != null || lowerName.Contains("manager") || lowerName.Contains("system")) {
                MoveToSubfolder(go, systemsFolder);
                movedCount++;
                continue;
            }

            // ENVIRONMENT (MeshRenderers, street, buildings, etc)
            if (go.GetComponent<MeshRenderer>() != null || lowerName.Contains("street") || lowerName.Contains("building") || lowerName.Contains("car") || lowerName.Contains("sidewalk") || lowerName.Contains("air") || lowerName.Contains("umbrella")) {
                // Apply specific mappings requested by user
                string customSubfolderName = null;
                if (lowerName.Contains("air")) customSubfolderName = "Airconditioners";
                else if (lowerName.Contains("umbrella")) customSubfolderName = "Umbrellas";
                else if (lowerName.Contains("street")) customSubfolderName = "Streets";
                else if (lowerName.Contains("building")) customSubfolderName = "Buildings";
                
                MoveToSubfolder(go, envFolder, customSubfolderName);
                movedCount++;
                continue;
            }

            // OTHERS
            MoveToSubfolder(go, othersFolder);
            movedCount++;
        }
        
        Debug.Log($"[Antigravity] Successfully organized {movedCount} objects into subgroups.");
    }

    private static void MoveToSubfolder(GameObject go, GameObject parentFolder, string customSubfolderName = null)
    {
        string subfolderName = customSubfolderName ?? GetCleanName(go.name);
        
        // Find or create subfolder under the main parent folder
        Transform subfolder = parentFolder.transform.Find(subfolderName);
        if (subfolder == null)
        {
            GameObject newSubfolder = new GameObject(subfolderName);
            newSubfolder.transform.SetParent(parentFolder.transform);
            Undo.RegisterCreatedObjectUndo(newSubfolder, "Create Subfolder");
            subfolder = newSubfolder.transform;
        }

        Undo.SetTransformParent(go.transform, subfolder, "Move to Subfolder");
    }

    private static string GetCleanName(string name)
    {
        // Remove " (1)", "(2)", "_1", etc.
        string cleanName = Regex.Replace(name, @"\s*\(\d+\)", "").Trim();
        cleanName = Regex.Replace(cleanName, @"_\d+", "").Trim();

        // Remove Variant suffixes if any
        cleanName = cleanName.Replace(" Variant", "").Trim();

        // Capitalize first letter and pluralize (basic rule)
        if (cleanName.Length > 0)
        {
            cleanName = char.ToUpper(cleanName[0]) + cleanName.Substring(1);
            if (!cleanName.EndsWith("s") && !cleanName.EndsWith("x"))
            {
                cleanName += "s";
            }
        }
        else
        {
            cleanName = "Miscellaneous";
        }

        return cleanName;
    }

    private static GameObject GetOrCreateFolder(string folderName)
    {
        GameObject folder = GameObject.Find(folderName);
        if (folder == null || folder.transform.parent != null)
        {
            // Only grab root level folder or create new
            folder = new GameObject(folderName);
            Undo.RegisterCreatedObjectUndo(folder, "Create Folder");
            folder.transform.SetAsFirstSibling();
        }
        return folder;
    }
}
