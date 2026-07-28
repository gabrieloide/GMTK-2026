using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-shot scene setup for the delivery game. Every step is idempotent, so any of
/// them can be re-run on its own while tuning without duplicating what it built.
/// </summary>
public static class SceneBootstrapper
{
    private const string MarkersRootName = "OrderPoints";
    private const string CarSpawnRootName = "CarSpawnPoints";
    private const string ArrowRigName = "DeliveryArrowRig";
    private const string ArrowLayerName = "ArrowUI";
    private const string CanvasName = "GameCanvas";

    private const string HolderMarkerPath = "Assets/Prefabs/OrderHolderMarker.prefab";
    private const string DestinationMarkerPath = "Assets/Prefabs/OrderDestinationMarker.prefab";
    private const string CarObstaclePath = "Assets/Prefabs/CarObstacle.prefab";
    private const string PickupMaterialPath = "Assets/Materials/OrderPickupMarker.mat";
    private const string DropoffMaterialPath = "Assets/Materials/OrderDropoffMarker.mat";
    private const string ArrowMaterialPath = "Assets/Materials/DeliveryArrow.mat";
    private const string ArrowTexturePath = "Assets/Textures/ArrowRenderTexture.renderTexture";
    private const string CarMeshPath = "Assets/3D/CAR-BASE.fbx";

    // The city is ~585 units across and the car is ~9 units long, so markers have to
    // be big to read at all, and sit high enough to be seen over the sidewalks.
    private const float MarkerHeight = 3f;
    private const float MarkerScale = 6f;
    private const float StreetMargin = 14f;
    private const float MinClearance = 8f;
    private const float MinMarkerSeparation = 45f;
    private const int WantedOrderPoints = 40;

    private const float CarSpawnHeight = 1.25f;
    private const int WantedCarSpawnPoints = 10;
    private const float MinCarSpawnSeparation = 80f;

    private static readonly Vector3[] Cardinals =
    {
        Vector3.forward, Vector3.back, Vector3.left, Vector3.right
    };

    [MenuItem("Tools/GMTK/1 Dedupe Order Manager", priority = 1)]
    public static void DedupeOrderManager()
    {
        var managers = Object.FindObjectsByType<OrderManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (managers.Length <= 1)
        {
            Debug.Log($"[Bootstrap] OrderManager count is {managers.Length} - nothing to dedupe.");
            return;
        }

        for (int i = 1; i < managers.Length; i++)
        {
            Debug.Log($"[Bootstrap] Removing duplicate OrderManager on '{managers[i].gameObject.name}'.");
            Undo.DestroyObjectImmediate(managers[i].gameObject);
        }

        Debug.Log($"[Bootstrap] Kept 1 OrderManager, removed {managers.Length - 1}.");
        MarkDirty();
    }

    [MenuItem("Tools/GMTK/2 Create Game And Timer Managers", priority = 2)]
    public static void CreateManagers()
    {
        EnsureComponentObject<GameManager>("GameManager");
        EnsureComponentObject<TimerManager>("TimerManager");
        MarkDirty();
    }

    [MenuItem("Tools/GMTK/3 Strip Stray Order Components", priority = 3)]
    public static void StripStrayOrderComponents()
    {
        var markersRoot = GameObject.Find(MarkersRootName);
        int removed = 0;

        foreach (var holder in Object.FindObjectsByType<OrderHolder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (IsUnderRoot(holder.transform, markersRoot)) continue;
            Undo.DestroyObjectImmediate(holder);
            removed++;
        }

        foreach (var destination in Object.FindObjectsByType<OrderDestination>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (IsUnderRoot(destination.transform, markersRoot)) continue;
            Undo.DestroyObjectImmediate(destination);
            removed++;
        }

        Debug.Log($"[Bootstrap] Stripped {removed} stray order component(s) left over from earlier scene edits.");
        if (removed > 0) MarkDirty();
    }

    [MenuItem("Tools/GMTK/4 Create Order Marker Prefabs", priority = 4)]
    public static void CreateOrderMarkerPrefabs()
    {
        CreateMarkerPrefab<OrderHolder>(HolderMarkerPath, PickupMaterialPath, new Color(0.15f, 0.9f, 0.3f));
        CreateMarkerPrefab<OrderDestination>(DestinationMarkerPath, DropoffMaterialPath, new Color(0.95f, 0.25f, 0.15f));
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/GMTK/5 Place Order Markers", priority = 5)]
    public static void PlaceOrderMarkers()
    {
        if (GameObject.Find(MarkersRootName) != null)
        {
            Debug.LogWarning($"[Bootstrap] '{MarkersRootName}' already exists - delete it first if you want to regenerate the markers.");
            return;
        }

        var holderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HolderMarkerPath);
        var destinationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DestinationMarkerPath);
        if (holderPrefab == null || destinationPrefab == null)
        {
            Debug.LogError("[Bootstrap] Marker prefabs are missing - run step 4 first.");
            return;
        }

        CollectSceneBounds(out var buildings, out var obstacles);
        if (buildings.Count == 0)
        {
            Debug.LogError("[Bootstrap] No building renderers found - cannot work out where the streets are.");
            return;
        }

        var positions = FindOpenPositions(buildings, obstacles, WantedOrderPoints, MarkerHeight, MinMarkerSeparation);
        if (positions.Count < 4)
        {
            Debug.LogError($"[Bootstrap] Only found {positions.Count} usable spots - try lowering MinMarkerSeparation.");
            return;
        }

        // GetPickupPoint() adds the marker's forward vector plus this offset, and the
        // marker prefab's visual sits exactly one unit forward, so keep the offset zero
        // or the sphere stops lining up with the spot the player has to drive into.
        var orderManager = Object.FindFirstObjectByType<OrderManager>(FindObjectsInactive.Include);
        if (orderManager != null) SetPrivateValue(orderManager, "offset", Vector3.zero);

        var root = new GameObject(MarkersRootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Order Points");

        int holders = 0, destinations = 0;
        for (int i = 0; i < positions.Count; i++)
        {
            bool isHolder = i % 2 == 0;
            var prefab = isHolder ? holderPrefab : destinationPrefab;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            instance.transform.position = positions[i];
            instance.name = isHolder ? $"Pickup ({holders++})" : $"Dropoff ({destinations++})";
        }

        Debug.Log($"[Bootstrap] Placed {holders} pickup and {destinations} dropoff markers under '{MarkersRootName}'. Eyeball them in the Scene view - placement is bounds-based, so a few may need nudging.");
        MarkDirty();
    }

    [MenuItem("Tools/GMTK/6 Create Car Obstacle Prefab", priority = 6)]
    public static void CreateCarObstaclePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CarObstaclePath) != null)
        {
            Debug.Log("[Bootstrap] CarObstacle prefab already exists - skipping.");
            return;
        }

        var carMesh = AssetDatabase.LoadAssetAtPath<GameObject>(CarMeshPath);
        if (carMesh == null)
        {
            Debug.LogError($"[Bootstrap] Could not load '{CarMeshPath}'.");
            return;
        }

        EnsureFolder("Assets/Prefabs");

        var root = new GameObject("CarObstacle");
        var body = (GameObject)PrefabUtility.InstantiatePrefab(carMesh);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);

        var bounds = CalculateRendererBounds(root);
        var rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        // Stays a solid collider on purpose: PlayerCollision.OnCollisionEnter is what
        // produces the knockback, and that bump is the whole point of the obstacle.
        var collider = root.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = bounds.size;

        root.AddComponent<CarObstacle>();

        PrefabUtility.SaveAsPrefabAsset(root, CarObstaclePath);
        Object.DestroyImmediate(root);

        Debug.Log($"[Bootstrap] Created '{CarObstaclePath}' (collider size {bounds.size}).");
    }

    [MenuItem("Tools/GMTK/7 Configure Car Spawner", priority = 7)]
    public static void ConfigureCarSpawner()
    {
        var spawner = Object.FindFirstObjectByType<CarSpawner>(FindObjectsInactive.Include);
        if (spawner == null)
        {
            spawner = EnsureComponentObject<CarSpawner>("CarSpawner");
        }

        var serialized = new SerializedObject(spawner);

        var prefabProperty = serialized.FindProperty("carPrefab");
        if (prefabProperty.objectReferenceValue == null)
        {
            prefabProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CarObstaclePath);
            if (prefabProperty.objectReferenceValue == null)
            {
                Debug.LogError("[Bootstrap] CarObstacle prefab missing - run step 6 first.");
                return;
            }
        }

        var materialsProperty = serialized.FindProperty("carMaterials");
        if (materialsProperty.arraySize == 0)
        {
            var materials = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith("Car"))
                .Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Where(material => material != null)
                .Cast<Object>()
                .ToList();
            SetArray(materialsProperty, materials);
        }

        var spawnPointsProperty = serialized.FindProperty("spawnPoints");
        if (spawnPointsProperty.arraySize == 0)
        {
            CollectSceneBounds(out var buildings, out var obstacles);
            var positions = FindOpenPositions(buildings, obstacles, WantedCarSpawnPoints, CarSpawnHeight, MinCarSpawnSeparation);

            var existingRoot = GameObject.Find(CarSpawnRootName);
            var root = existingRoot != null ? existingRoot : new GameObject(CarSpawnRootName);
            if (existingRoot == null) Undo.RegisterCreatedObjectUndo(root, "Create Car Spawn Points");

            var transforms = new List<Object>();
            for (int i = 0; i < positions.Count; i++)
            {
                var point = new GameObject($"CarSpawn ({i})");
                point.transform.SetParent(root.transform);
                point.transform.position = positions[i];
                point.transform.rotation = Quaternion.LookRotation(FindClearestDirection(positions[i], obstacles), Vector3.up);
                transforms.Add(point.transform);
            }

            SetArray(spawnPointsProperty, transforms);
            Debug.Log($"[Bootstrap] Generated {transforms.Count} car spawn point(s) under '{CarSpawnRootName}'.");
        }

        serialized.ApplyModifiedProperties();
        Debug.Log("[Bootstrap] CarSpawner configured. Traffic stays capped by its maxConcurrentCars field.");
        MarkDirty();
    }

    [MenuItem("Tools/GMTK/8 Build HUD Canvas", priority = 8)]
    public static void BuildHudCanvas()
    {
        if (GameObject.Find(CanvasName) != null)
        {
            Debug.Log("[Bootstrap] HUD canvas already exists - skipping.");
            return;
        }

        var canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create HUD Canvas");

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
        {
            // The project runs on the new Input System only, so the legacy
            // StandaloneInputModule would throw instead of driving the button.
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        }

        var hud = CreateUIObject("HUD", canvasObject.transform);
        Stretch(hud.GetComponent<RectTransform>());

        var timerText = CreateText(hud.transform, "TimerText", "Time: 0", 54, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(600f, 80f));
        var scoreText = CreateText(hud.transform, "ScoreText", "Score: 0", 44, TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(220f, -70f), new Vector2(400f, 70f));

        var gameOverPanel = CreateUIObject("GameOverPanel", canvasObject.transform);
        Stretch(gameOverPanel.GetComponent<RectTransform>());
        var background = gameOverPanel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.75f);

        CreateText(gameOverPanel.transform, "GameOverText", "GAME OVER", 96, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(900f, 140f));
        var finalScoreText = CreateText(gameOverPanel.transform, "FinalScoreText", "Final Score: 0", 56, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(900f, 90f));

        var restartButton = CreateUIObject("RestartButton", gameOverPanel.transform);
        var buttonRect = restartButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -110f);
        buttonRect.sizeDelta = new Vector2(460f, 100f);
        restartButton.AddComponent<Image>().color = new Color(0.2f, 0.6f, 0.95f);
        var button = restartButton.AddComponent<Button>();
        CreateText(restartButton.transform, "Label", "Restart (any key)", 40, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 90f));

        var hudController = canvasObject.AddComponent<HUDController>();
        SetPrivateRef(hudController, "timerText", timerText);
        SetPrivateRef(hudController, "scoreText", scoreText);
        SetPrivateRef(hudController, "timerManager", Object.FindFirstObjectByType<TimerManager>(FindObjectsInactive.Include));

        var gameOverUI = canvasObject.AddComponent<GameOverUI>();
        SetPrivateRef(gameOverUI, "gameOverPanel", gameOverPanel);
        SetPrivateRef(gameOverUI, "finalScoreText", finalScoreText);

        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, gameOverUI.Restart);
        gameOverPanel.SetActive(false);

        Debug.Log("[Bootstrap] Built HUD canvas with timer, score and game over panel.");
        MarkDirty();
    }

    [MenuItem("Tools/GMTK/9 Build Delivery Arrow Compass", priority = 9)]
    public static void BuildDeliveryArrowCompass()
    {
        if (GameObject.Find(ArrowRigName) != null)
        {
            Debug.Log("[Bootstrap] Delivery arrow rig already exists - skipping.");
            return;
        }

        var canvas = GameObject.Find(CanvasName);
        if (canvas == null)
        {
            Debug.LogError("[Bootstrap] HUD canvas missing - run step 8 first.");
            return;
        }

        int arrowLayer = EnsureLayer(ArrowLayerName);
        if (arrowLayer < 0)
        {
            Debug.LogError("[Bootstrap] No free layer slot available for the arrow.");
            return;
        }

        var renderTexture = EnsureRenderTexture();
        var arrowMaterial = EnsureMaterial(ArrowMaterialPath, new Color(1f, 0.85f, 0.1f), unlit: true);

        // Parked far under the city so the dedicated camera only ever frames the
        // arrow, with nothing from the playable area leaking into the shot.
        var rig = new GameObject(ArrowRigName);
        rig.transform.position = new Vector3(0f, -500f, 0f);
        Undo.RegisterCreatedObjectUndo(rig, "Create Delivery Arrow Rig");

        var arrowModel = new GameObject("ArrowModel");
        arrowModel.transform.SetParent(rig.transform, false);

        CreateArrowPart("Shaft", arrowModel.transform, new Vector3(0f, 0f, -0.15f),
            new Vector3(0.3f, 0.2f, 0.9f), Quaternion.identity, arrowMaterial);
        CreateArrowPart("Head", arrowModel.transform, new Vector3(0f, 0f, 0.5f),
            new Vector3(0.55f, 0.2f, 0.55f), Quaternion.Euler(0f, 45f, 0f), arrowMaterial);
        SetLayerRecursive(arrowModel, arrowLayer);

        var cameraObject = new GameObject("ArrowCamera");
        cameraObject.transform.SetParent(rig.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 5f, 0f);
        cameraObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var arrowCamera = cameraObject.AddComponent<Camera>();
        arrowCamera.orthographic = true;
        arrowCamera.orthographicSize = 1.1f;
        arrowCamera.cullingMask = 1 << arrowLayer;
        arrowCamera.clearFlags = CameraClearFlags.SolidColor;
        arrowCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        arrowCamera.targetTexture = renderTexture;
        arrowCamera.nearClipPlane = 0.1f;
        arrowCamera.farClipPlane = 20f;

        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera == arrowCamera) continue;
            camera.cullingMask &= ~(1 << arrowLayer);
        }

        var hud = canvas.transform.Find("HUD");
        var compass = CreateUIObject("ArrowCompass", hud != null ? hud : canvas.transform);
        var compassRect = compass.GetComponent<RectTransform>();
        compassRect.anchorMin = compassRect.anchorMax = compassRect.pivot = new Vector2(0.5f, 1f);
        compassRect.anchoredPosition = new Vector2(0f, -150f);
        compassRect.sizeDelta = new Vector2(150f, 150f);
        var rawImage = compass.AddComponent<RawImage>();
        rawImage.texture = renderTexture;

        var controller = rig.AddComponent<DeliveryArrowController>();
        SetPrivateRef(controller, "arrowModel", arrowModel.transform);

        Debug.Log($"[Bootstrap] Built the delivery arrow compass on layer '{ArrowLayerName}' (index {arrowLayer}).");
        MarkDirty();
    }

    [MenuItem("Tools/GMTK/Run All", priority = 20)]
    public static void RunAll()
    {
        DedupeOrderManager();
        CreateManagers();
        StripStrayOrderComponents();
        CreateOrderMarkerPrefabs();
        PlaceOrderMarkers();
        CreateCarObstaclePrefab();
        ConfigureCarSpawner();
        BuildHudCanvas();
        BuildDeliveryArrowCompass();

        AssetDatabase.SaveAssets();
        Debug.Log("[Bootstrap] Run All finished. Save the scene (Ctrl+S) if everything looks right.");
    }

    private static void CreateArrowPart(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateMarkerPrefab<T>(string prefabPath, string materialPath, Color color) where T : Component
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            Debug.Log($"[Bootstrap] '{prefabPath}' already exists - skipping.");
            return;
        }

        EnsureFolder("Assets/Prefabs");
        var material = EnsureMaterial(materialPath, color, unlit: true);

        var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
        var marker = root.AddComponent<T>();

        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        // No collider: the pickup test is a script-side overlap check, and a solid
        // collider here would bounce the player's car off the marker.
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.forward;
        visual.transform.localScale = Vector3.one * MarkerScale;
        visual.GetComponent<MeshRenderer>().sharedMaterial = material;

        SetPrivateRef(marker, "markerVisual", visual.GetComponent<MeshRenderer>());

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"[Bootstrap] Created '{prefabPath}'.");
    }

    /// <summary>
    /// Buildings are worth standing next to, mountains are only worth avoiding, so the
    /// two are collected separately. Names live on the FBX instance root while the
    /// renderers sit on its children, hence walking roots rather than renderers.
    /// </summary>
    private static void CollectSceneBounds(out List<Bounds> candidates, out List<Bounds> obstacles)
    {
        candidates = new List<Bounds>();
        obstacles = new List<Bounds>();

        foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var name = transform.name.ToLowerInvariant();
            bool isBuilding = name.StartsWith("building");
            bool isMountain = name.StartsWith("mountain");
            if (!isBuilding && !isMountain) continue;

            var renderers = transform.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) continue;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            obstacles.Add(bounds);
            if (isBuilding) candidates.Add(bounds);
        }
    }

    /// <summary>
    /// Buildings in this scene have no colliders, so open ground (i.e. the streets) is
    /// found by pushing out from each building and scoring how clear the spot is.
    /// </summary>
    private static List<Vector3> FindOpenPositions(List<Bounds> candidates, List<Bounds> obstacles, int wanted, float height, float minSeparation)
    {
        var chosen = new List<Vector3>();
        if (candidates.Count == 0) return chosen;

        var shuffled = candidates.OrderBy(_ => Random.value).ToList();
        foreach (var building in shuffled)
        {
            if (chosen.Count >= wanted) break;

            Vector3 best = Vector3.zero;
            float bestClearance = float.MinValue;
            foreach (var direction in Cardinals)
            {
                float reach = Mathf.Abs(Vector3.Dot(building.extents, direction)) + StreetMargin;
                var candidate = building.center + direction * reach;
                candidate.y = height;

                float clearance = obstacles.Min(other => HorizontalDistanceToBounds(candidate, other));
                if (clearance > bestClearance)
                {
                    bestClearance = clearance;
                    best = candidate;
                }
            }

            if (bestClearance < MinClearance) continue;
            if (chosen.Any(position => Vector3.Distance(position, best) < minSeparation)) continue;
            chosen.Add(best);
        }

        return chosen;
    }

    private static Vector3 FindClearestDirection(Vector3 position, List<Bounds> obstacles)
    {
        if (obstacles.Count == 0) return Vector3.forward;

        Vector3 best = Vector3.forward;
        float bestScore = float.MinValue;
        foreach (var direction in Cardinals)
        {
            float score = 0f;
            for (int step = 1; step <= 6; step++)
            {
                var sample = position + direction * (step * 15f);
                score += obstacles.Min(bounds => HorizontalDistanceToBounds(sample, bounds));
            }
            if (score > bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }
        return best;
    }

    private static float HorizontalDistanceToBounds(Vector3 point, Bounds bounds)
    {
        float dx = Mathf.Max(bounds.min.x - point.x, 0f, point.x - bounds.max.x);
        float dz = Mathf.Max(bounds.min.z - point.z, 0f, point.z - bounds.max.z);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, new Vector3(2f, 1.5f, 4f));

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        // The root sits at the origin unrotated and unscaled, so world bounds are local bounds.
        return bounds;
    }

    private static T EnsureComponentObject<T>(string name) where T : Component
    {
        var existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Debug.Log($"[Bootstrap] {typeof(T).Name} already in the scene - skipping.");
            return existing;
        }

        var created = new GameObject(name);
        var component = created.AddComponent<T>();
        Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
        Debug.Log($"[Bootstrap] Created {name}.");
        return component;
    }

    private static RenderTexture EnsureRenderTexture()
    {
        var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(ArrowTexturePath);
        if (existing != null) return existing;

        EnsureFolder("Assets/Textures");
        var renderTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32)
        {
            name = "ArrowRenderTexture"
        };
        AssetDatabase.CreateAsset(renderTexture, ArrowTexturePath);
        return renderTexture;
    }

    private static Material EnsureMaterial(string path, Color color, bool unlit = false)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        EnsureFolder("Assets/Materials");

        var shader = unlit ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var material = new Material(shader);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static int EnsureLayer(string layerName)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return -1;

        var tagManager = new SerializedObject(assets[0]);
        var layers = tagManager.FindProperty("layers");

        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return i;
        }

        // 0-7 are reserved by Unity.
        for (int i = 8; i < layers.arraySize; i++)
        {
            var element = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(element.stringValue)) continue;
            element.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            return i;
        }

        return -1;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    private static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment,
        Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        var created = CreateUIObject(name, parent);
        var rect = created.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = created.AddComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.font = LoadBuiltinFont();
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Font LoadBuiltinFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetLayerRecursive(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform) SetLayerRecursive(child.gameObject, layer);
    }

    private static bool IsUnderRoot(Transform target, GameObject root)
    {
        return root != null && target.IsChildOf(root.transform);
    }

    private static void SetPrivateRef(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[Bootstrap] Could not find field '{field}' on {target.GetType().Name}.");
            return;
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivateValue(Object target, string field, Vector3 value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(field);
        if (property == null) return;
        property.vector3Value = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(SerializedProperty property, IList<Object> values)
    {
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static void MarkDirty()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
