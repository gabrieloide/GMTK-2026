using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the player car's smoke rig: one exhaust plume at the tailpipe and a tyre
/// system at each rear wheel, all sharing a material off the hand-drawn smoke sprite.
/// Re-running throws the old rig away and builds a fresh one, so it is safe to hit
/// again after moving the car model or retuning the numbers below.
/// </summary>
public static class CarSmokeBuilder
{
    private const string RootName = "Smoke";
    private const string TexturePath = "Assets/Materials/smoke.png";
    private const string MaterialPath = "Assets/Materials/CarSmoke.mat";

    // Player local space. The rear wheels sit at z -8.25 and x +-2.93, the rear bumper
    // at z -10.15, and the ground plane the wheels touch at y 0.11.
    private static readonly Vector3 ExhaustPosition = new Vector3(-1.7f, 0.9f, -10.3f);
    private static readonly Vector3 LeftTyrePosition = new Vector3(-2.93f, 0.35f, -8.25f);
    private static readonly Vector3 RightTyrePosition = new Vector3(2.93f, 0.35f, -8.25f);

    // Exhaust fires out of the back (+Z turned around) and tilted slightly up so the
    // plume clears the bumper instead of burying itself in the road.
    private static readonly Quaternion ExhaustRotation = Quaternion.Euler(-12f, 180f, 0f);

    [MenuItem("Tools/GMTK/Build Car Smoke")]
    public static void Build()
    {
        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("[CarSmokeBuilder] No PlayerController in the open scene.");
            return;
        }

        Material material = BuildMaterial();
        if (material == null) return;

        Transform old = player.transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Car Smoke");
        root.transform.SetParent(player.transform, false);

        ParticleSystem exhaust = CreateExhaust(root.transform, material);
        ParticleSystem left = CreateTyreSmoke(root.transform, "TyreSmokeLeft", LeftTyrePosition, material);
        ParticleSystem right = CreateTyreSmoke(root.transform, "TyreSmokeRight", RightTyrePosition, material);

        var smoke = player.GetComponent<CarSmoke>();
        if (smoke == null) smoke = Undo.AddComponent<CarSmoke>(player.gameObject);

        var serialized = new SerializedObject(smoke);
        serialized.FindProperty("exhaust").objectReferenceValue = exhaust;
        serialized.FindProperty("tyreSmokeLeft").objectReferenceValue = left;
        serialized.FindProperty("tyreSmokeRight").objectReferenceValue = right;
        serialized.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Selection.activeGameObject = root;
        Debug.Log("[CarSmokeBuilder] Built the car smoke rig under " + player.name + "/" + RootName + ".");
    }

    private static ParticleSystem CreateExhaust(Transform parent, Material material)
    {
        ParticleSystem system = CreateSystem(parent, "Exhaust", ExhaustPosition, ExhaustRotation, material);

        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        // Sized for the game's camera, which sits ~65 units back and draws the whole car
        // about 40px wide. A realistically small plume lands on single-digit pixels and
        // vanishes, so these are deliberately far larger than the tailpipe would suggest.
        main.startSize = new ParticleSystem.MinMaxCurve(3f, 5f);
        // Exhaust is dirtier than tyre smoke, but still opaque: see the gradient note in
        // CreateSystem. Fumes read as fumes through the colour, not through the alpha.
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.72f, 0.76f, 0.8f, 1f));
        main.gravityModifier = -0.05f;
        main.maxParticles = 120;

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 13f;
        shape.radius = 0.16f;

        SetGrowth(system, 0.6f, 1.7f);
        return system;
    }

    private static ParticleSystem CreateTyreSmoke(Transform parent, string name, Vector3 position, Material material)
    {
        ParticleSystem system = CreateSystem(parent, name, position, Quaternion.identity, material);

        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
        main.gravityModifier = -0.02f;
        main.maxParticles = 300;

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        SetGrowth(system, 0.5f, 2.0f);
        return system;
    }

    private static ParticleSystem CreateSystem(Transform parent, string name, Vector3 position,
                                               Quaternion rotation, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation;

        var system = go.AddComponent<ParticleSystem>();

        var main = system.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = true;
        // World space is what leaves the smoke hanging in the road behind the car instead
        // of dragging the whole cloud along with it. Hierarchy scaling keeps the sizes
        // below honest under the player's 0.75 scale.
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        // CarSmoke owns the rate; anything non-zero here would show up as a puff the
        // moment the scene loads.
        var emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        // Puffs shed their speed instead of flying off in a straight line.
        var limit = system.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = new ParticleSystem.MinMaxCurve(0.6f);
        limit.dampen = 0.35f;

        var rotationOverLifetime = system.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-35f * Mathf.Deg2Rad, 35f * Mathf.Deg2Rad);

        // The sprite is hand-drawn pixel art meant to be laid down opaque, and the scene
        // runs through a posterising dither, which collapses a half-transparent grey over
        // the road straight back into the road colour. So the puffs stay solid for most of
        // their life and only let go right at the end.
        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return system;
    }

    /// <summary>Puffs start small and bloom out as they die, which is most of what sells smoke.</summary>
    private static void SetGrowth(ParticleSystem system, float from, float to)
    {
        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
    }

    private static Material BuildMaterial()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null)
        {
            Debug.LogError("[CarSmokeBuilder] Missing smoke sprite at " + TexturePath + ".");
            return null;
        }

        // The sprite is drawn with hard alpha edges, so it needs alphaIsTransparency or
        // the transparent pixels bleed black into the fringe.
        if (AssetImporter.GetAtPath(TexturePath) is TextureImporter importer && !importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            Debug.LogError("[CarSmokeBuilder] URP particle shader not found.");
            return null;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1f);  // transparent
        material.SetFloat("_Blend", 0f);    // alpha blended
        material.SetFloat("_AlphaClip", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.renderQueue = (int)RenderQueue.Transparent;

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }
}
