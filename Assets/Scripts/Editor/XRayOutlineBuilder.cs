using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Sets the player car up so it is never lost behind a building: builds the two x-ray
/// materials and hangs an <see cref="XRayOutline"/> on the car with them wired in.
/// Re-running only refreshes the wiring, so it is safe to hit again after swapping the
/// car model. All the look tuning lives on the materials themselves.
/// </summary>
public static class XRayOutlineBuilder
{
    private const string MaskMaterialPath = "Assets/Materials/XRayMask.mat";
    private const string OutlineMaterialPath = "Assets/Materials/XRayOutline.mat";

    private const string MaskShaderName = "GMTK/X-Ray Mask";
    private const string OutlineShaderName = "GMTK/X-Ray Outline";

    // The arrow is a separate hint that already floats clear of the roof, so ringing it
    // as well only adds noise around the car.
    private static readonly string[] IgnoredChildren = { "ArrowModel" };

    // Warm yellow: it separates from the buildings and from the road in both the lit and
    // the shaded half of the city, and survives the dither pass.
    private static readonly Color OutlineColor = new Color(1f, 0.85f, 0.25f, 1f);

    // The car is only about 40px wide from the game camera, so a physically sensible ring
    // lands on a single pixel and the dither pass eats half of it.
    private const float OutlineWidth = 0.7f;
    private const float RadialBlend = 0.65f;

    // A little colour inside the ring so the car still reads as a car, not as a lost hoop.
    private const float FillAlpha = 0.22f;

    [MenuItem("Tools/GMTK/Build Car X-Ray Outline")]
    public static void Build()
    {
        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("[XRayOutlineBuilder] No PlayerController in the open scene.");
            return;
        }

        Material mask = BuildMaskMaterial();
        Material outline = BuildOutlineMaterial();
        if (mask == null || outline == null) return;

        var effect = player.GetComponent<XRayOutline>();
        if (effect == null) effect = Undo.AddComponent<XRayOutline>(player.gameObject);

        var serialized = new SerializedObject(effect);
        serialized.FindProperty("maskMaterial").objectReferenceValue = mask;
        serialized.FindProperty("outlineMaterial").objectReferenceValue = outline;

        SerializedProperty ignored = serialized.FindProperty("ignored");
        List<Transform> skip = FindIgnored(player.transform);
        ignored.arraySize = skip.Count;
        for (int i = 0; i < skip.Count; i++)
        {
            ignored.GetArrayElementAtIndex(i).objectReferenceValue = skip[i];
        }

        serialized.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Selection.activeGameObject = player.gameObject;
        Debug.Log("[XRayOutlineBuilder] " + player.name + " will now show its outline through buildings. " +
                  "Tune the colour and width on " + OutlineMaterialPath + ".");
    }

    private static List<Transform> FindIgnored(Transform player)
    {
        var found = new List<Transform>();
        foreach (string childName in IgnoredChildren)
        {
            Transform child = player.Find(childName);
            if (child != null) found.Add(child);
        }

        return found;
    }

    private static Material BuildMaskMaterial()
    {
        Material material = LoadOrCreate(MaskMaterialPath, MaskShaderName);
        if (material == null) return null;

        material.SetFloat("_StencilBit", 128f);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Material BuildOutlineMaterial()
    {
        Material material = LoadOrCreate(OutlineMaterialPath, OutlineShaderName);
        if (material == null) return null;

        material.SetColor("_OutlineColor", OutlineColor);
        material.SetFloat("_OutlineWidth", OutlineWidth);
        material.SetFloat("_RadialBlend", RadialBlend);
        material.SetColor("_FillColor", new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, FillAlpha));
        material.SetFloat("_StencilBit", 128f);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Material LoadOrCreate(string path, string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError("[XRayOutlineBuilder] Shader '" + shaderName + "' not found. " +
                           "It should have imported from Assets/Shaders.");
            return null;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        return material;
    }
}
