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
/// The shaders alone are not enough to gate visibility: the outline hull is pushed outward
/// from the mesh centre, which drags the underside past the car's own wheels and into the
/// road right next to it, so its depth test reads "something is in front" even in open
/// street. Update() adds the missing gate - a line-of-sight check from the camera to the
/// car against every building's bounds - and only the proxies survive that check, so the
/// shader-level trick just has to shape the ring, not decide whether it should exist.
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

    // Matches the naming convention SceneBootstrapper already relies on for the building
    // FBXs (building1..building8), so both scripts agree on what counts as a building
    // without either one having to tag or list them.
    private const string BuildingNamePrefix = "building";

    private const string MaskProxyName = "XRayMaskProxy";
    private const string OutlineProxyName = "XRayOutlineProxy";

    private readonly List<Renderer> proxies = new List<Renderer>();

    // Buildings do not move and have no colliders (see SceneBootstrapper), so a physics
    // raycast is not an option - the bounds are collected once and reused every frame.
    private readonly List<Bounds> buildingBounds = new List<Bounds>();
    private Vector3 sightTargetLocalOffset;
    private Camera sightCamera;
    private bool proxiesVisible;

    /// <summary>Turns the whole effect off and on, e.g. while the car is exploding.</summary>
    public bool Visible
    {
        get => enabled;
        set => enabled = value;
    }

    private void Awake()
    {
        Build();
        CacheBuildingBounds();
    }

    private void OnEnable()
    {
        // Start hidden rather than flashing the full ring: Update runs this same frame and
        // will turn the proxies back on if the car genuinely is behind something.
        proxiesVisible = false;
        SetProxiesEnabled(false);
    }

    private void OnDisable()
    {
        SetProxiesEnabled(false);
    }

    private void Update()
    {
        bool occluded = IsOccludedByBuilding();
        if (occluded == proxiesVisible) return;

        proxiesVisible = occluded;
        SetProxiesEnabled(occluded);
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

        bool hasCombinedWorldBounds = false;
        var combinedWorldBounds = new Bounds();

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

            if (!hasCombinedWorldBounds)
            {
                combinedWorldBounds = source.bounds;
                hasCombinedWorldBounds = true;
            }
            else
            {
                combinedWorldBounds.Encapsulate(source.bounds);
            }
        }

        // Where the line-of-sight check aims: the middle of the car's own visible bulk
        // rather than its transform origin, which for this rig sits down at wheel height.
        sightTargetLocalOffset = hasCombinedWorldBounds
            ? transform.InverseTransformPoint(combinedWorldBounds.center)
            : Vector3.zero;

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

    private void CacheBuildingBounds()
    {
        buildingBounds.Clear();

        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!candidate.name.ToLowerInvariant().StartsWith(BuildingNamePrefix)) continue;

            MeshRenderer[] renderers = candidate.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) continue;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            buildingBounds.Add(bounds);
        }
    }

    private bool IsOccludedByBuilding()
    {
        if (buildingBounds.Count == 0) return false;

        if (sightCamera == null)
        {
            sightCamera = Camera.main;
            if (sightCamera == null) return false;
        }

        Vector3 origin = sightCamera.transform.position;
        Vector3 target = transform.TransformPoint(sightTargetLocalOffset);

        foreach (Bounds bounds in buildingBounds)
        {
            if (SegmentIntersectsBounds(origin, target, bounds)) return true;
        }

        return false;
    }

    // Buildings have no colliders (see SceneBootstrapper), so this stands in for the
    // Physics.Linecast that would otherwise answer "is anything between the camera and the
    // car" - a plain ray-vs-AABB test against the bounds cached in CacheBuildingBounds.
    private static bool SegmentIntersectsBounds(Vector3 from, Vector3 to, Bounds bounds)
    {
        Vector3 offset = to - from;
        float length = offset.magnitude;
        if (length <= 0f) return bounds.Contains(from);

        var ray = new Ray(from, offset / length);
        return bounds.IntersectRay(ray, out float hitDistance) && hitDistance <= length;
    }
}
