using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps the car readable when a building comes between it and the camera. The camera is
/// fixed, so the player cannot simply swing around an obstruction - instead the car's
/// contour is drawn on top of whatever is hiding it.
///
/// The work is done by two shaders (GMTK/X-Ray Mask and GMTK/X-Ray Outline), and this
/// component just gets them onto the car: for every mesh under here it spawns two proxy
/// renderers sharing the same mesh and transform, one per material. They are separate
/// objects rather than extra entries in the car's own material list because a renderer
/// only draws as many materials as the mesh has submeshes.
///
/// Nothing is authored in the scene - the proxies are built at Awake, so re-exporting or
/// swapping the car model needs no rebuild here. XRayOutlineBuilder wires the materials up.
/// </summary>
[DisallowMultipleComponent]
public class XRayOutline : MonoBehaviour
{
    [Header("Materials")]
    [Tooltip("GMTK/X-Ray Mask - stamps the silhouette into the stencil buffer.")]
    [SerializeField] private Material maskMaterial;

    [Tooltip("GMTK/X-Ray Outline - the ring that shows through buildings.")]
    [SerializeField] private Material outlineMaterial;

    [Header("Scope")]
    [Tooltip("Sub-hierarchies to leave alone, such as the delivery arrow, which already " +
             "floats above the roof and does not need its own contour.")]
    [SerializeField] private Transform[] ignored = new Transform[0];

    private const string MaskProxyName = "XRayMaskProxy";
    private const string OutlineProxyName = "XRayOutlineProxy";

    private readonly List<Renderer> proxies = new List<Renderer>();

    /// <summary>Turns the whole effect off and on, e.g. while the car is exploding.</summary>
    public bool Visible
    {
        get => enabled;
        set => enabled = value;
    }

    private void Awake()
    {
        Build();
    }

    private void OnEnable()
    {
        SetProxiesEnabled(true);
    }

    private void OnDisable()
    {
        SetProxiesEnabled(false);
    }

    private void Build()
    {
        if (maskMaterial == null || outlineMaterial == null)
        {
            Debug.LogWarning("[XRayOutline] No materials assigned on " + name +
                             ", so the car will disappear behind buildings. Run Tools/GMTK/Build Car X-Ray Outline.", this);
            enabled = false;
            return;
        }

        // Snapshot first: the proxies added below are children too, and re-processing them
        // would build outlines of outlines.
        MeshRenderer[] sources = GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer source in sources)
        {
            if (IsIgnored(source.transform)) continue;

            var filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;

            Mesh mesh = filter.sharedMesh;
            CreateProxy(source, mesh, MaskProxyName, maskMaterial);

            Renderer outline = CreateProxy(source, mesh, OutlineProxyName, outlineMaterial);

            // The hull is pushed away from the mesh's own centre, which is per-mesh data the
            // shader cannot look up on its own.
            var block = new MaterialPropertyBlock();
            block.SetVector("_CenterOS", mesh.bounds.center);
            outline.SetPropertyBlock(block);
        }

        if (proxies.Count == 0)
        {
            Debug.LogWarning("[XRayOutline] Found no meshes under " + name + " to outline.", this);
        }
    }

    private Renderer CreateProxy(MeshRenderer source, Mesh mesh, string proxyName, Material material)
    {
        var go = new GameObject(proxyName);
        go.layer = source.gameObject.layer;
        go.transform.SetParent(source.transform, false);

        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var renderer = go.AddComponent<MeshRenderer>();

        // One entry per submesh: a shorter list would leave part of the car unmasked, and
        // the mask has to cover exactly what the outline surrounds.
        var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        renderer.sharedMaterials = materials;

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;

        proxies.Add(renderer);
        return renderer;
    }

    private bool IsIgnored(Transform candidate)
    {
        foreach (Transform root in ignored)
        {
            if (root == null) continue;
            if (candidate == root || candidate.IsChildOf(root)) return true;
        }

        return false;
    }

    private void SetProxiesEnabled(bool value)
    {
        foreach (Renderer proxy in proxies)
        {
            if (proxy != null) proxy.enabled = value;
        }
    }
}
