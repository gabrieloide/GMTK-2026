using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using Code.Scripts.Audio;

public class OrderManager : MonoBehaviour
{
    [Serializable]
    public class DifficultyTier
    {
        public int deliveriesRequired;
        public float minDistance;
        public float maxDistance;
    }

    [SerializeField] public Vector3 offset = Vector3.zero;

    // Tall on Y so a marker sitting near street level still overlaps the car's
    // collider (which is offset well above its transform), wide enough on XZ that
    // driving past at speed reliably registers.
    [SerializeField] public Vector3 detectionHalfExtents = new Vector3(3f, 10f, 3f);

    [Header("Gizmos")]
    [SerializeField] private bool drawTargetGizmo = true;
    [SerializeField] private float targetGizmoHeight = 30f;
    [SerializeField] private Color pickupGizmoColor = new Color(0.2f, 1f, 0.4f);
    [SerializeField] private Color dropoffGizmoColor = new Color(1f, 0.4f, 0.1f);

    [Header("Delivery Confetti")]
    [Tooltip("Texture used on the cash particles that burst out on delivery.")]
    [SerializeField] private Texture2D confettiTexture;

    [SerializeField]
    private DifficultyTier[] difficultyTiers = new DifficultyTier[]
    {
        new DifficultyTier { deliveriesRequired = 0, minDistance = 40f, maxDistance = 100f },
        new DifficultyTier { deliveriesRequired = 3, minDistance = 80f, maxDistance = 160f },
        new DifficultyTier { deliveriesRequired = 6, minDistance = 140f, maxDistance = 240f },
        new DifficultyTier { deliveriesRequired = 10, minDistance = 200f, maxDistance = 350f },
        new DifficultyTier { deliveriesRequired = 15, minDistance = 300f, maxDistance = 500f },
    };

    [Header("Delivery Timer")]
    [Tooltip("Seconds of driving time granted per world unit of distance between pickup and drop-off.")]
    [SerializeField] private float secondsPerDistanceUnit = 0.25f;
    [Tooltip("Flat seconds added on top of the distance-based time, for parking/turning/mistakes.")]
    [SerializeField] private float deliveryTimeBuffer = 8f;
    [Tooltip("Absolute minimum time granted for any single delivery, regardless of distance.")]
    [SerializeField] private float minDeliveryTime = 12f;

    private List<OrderHolder> allOrderHolders = new List<OrderHolder>();
    private List<OrderDestination> allOrderDestinations = new List<OrderDestination>();

    public OrderHolder CurrentHolder { get; private set; }
    public OrderDestination CurrentDestination { get; private set; }

    public bool HasActiveOrder => CurrentHolder != null && CurrentDestination != null;
    public bool IsCarryingParcel => HasActiveOrder && CurrentDestination.isPickedUp;

    private int deliveriesCompleted = 0;
    public int DeliveriesCompleted => deliveriesCompleted;

    public static Action<Vector3> OnOrderFinished;
    public static Action OnOrderAdded;

    // Fired whenever a new order starts (including the first one), with the fresh time
    // budget for that specific delivery, distance-based rather than one accumulating clock.
    public static Action<float> OnDeliveryTimeLimitSet;

    private Transform playerTransform;
    public static OrderManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        FindAllPoints();
    }

    private void FindAllPoints()
    {
        allOrderHolders = FindObjectsByType<OrderHolder>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
        allOrderDestinations = FindObjectsByType<OrderDestination>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
    }

    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;

        AddOrder();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

        if (playerTransform == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        // Self-heal: If playing and somehow no order is active, spawn one immediately!
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing && !HasActiveOrder)
        {
            AddOrder();
        }
    }

    public static Vector3 GetPickupPoint(Transform target)
    {
        if (target == null) return Vector3.zero;
        Vector3 offset = Instance != null ? Instance.offset : Vector3.zero;
        return target.position + target.TransformDirection(Vector3.forward + offset);
    }

    public static bool IsPlayerAtPoint(Vector3 point, Quaternion rotation)
    {
        var halfExtents = Instance != null ? Instance.detectionHalfExtents : new Vector3(3f, 10f, 3f);
        foreach (var hit in Physics.OverlapBox(point, halfExtents, rotation))
        {
            if (hit.CompareTag("Player")) return true;
        }
        return false;
    }

    public Vector3 GetCurrentTargetPosition()
    {
        var target = GetCurrentTargetTransform();
        if (target == null)
        {
            return playerTransform != null ? playerTransform.position : transform.position;
        }
        return GetPickupPoint(target);
    }

    // The marker the player has to reach right now: the destination once the
    // parcel is on board, the holder while it still has to be picked up.
    public Transform GetCurrentTargetTransform()
    {
        if (!HasActiveOrder) return null;
        if (IsCarryingParcel) return CurrentDestination.transform;
        return CurrentHolder.transform;
    }

    private DifficultyTier GetCurrentTier()
    {
        if (difficultyTiers == null || difficultyTiers.Length == 0)
        {
            return new DifficultyTier { deliveriesRequired = 0, minDistance = 40f, maxDistance = 100f };
        }

        var tier = difficultyTiers[0];
        foreach (var candidate in difficultyTiers)
        {
            if (deliveriesCompleted >= candidate.deliveriesRequired) tier = candidate;
        }
        return tier;
    }

    public void AddOrder()
    {
        if (allOrderHolders == null || allOrderHolders.Count == 0 || allOrderDestinations == null || allOrderDestinations.Count == 0)
        {
            FindAllPoints();
        }

        if (allOrderHolders.Count == 0 || allOrderDestinations.Count == 0)
        {
            Debug.LogWarning("[OrderManager] No OrderHolders or OrderDestinations found in scene!");
            return;
        }

        // Clean up any previous state
        if (CurrentHolder != null)
        {
            CurrentHolder.isActive = false;
            CurrentHolder.isCurrent = false;
        }
        if (CurrentDestination != null)
        {
            CurrentDestination.isActive = false;
            CurrentDestination.isPickedUp = false;
        }

        // Select new Holder (try to avoid picking the exact same one if multiple exist)
        List<OrderHolder> availableHolders = allOrderHolders.Where(h => h != null).ToList();
        if (availableHolders.Count > 1 && CurrentHolder != null)
        {
            availableHolders = availableHolders.Where(h => h != CurrentHolder).ToList();
        }
        OrderHolder newOrderHolder = availableHolders[UnityEngine.Random.Range(0, availableHolders.Count)];

        // Select new Destination matching distance tier
        List<OrderDestination> availableDestinations = allOrderDestinations.Where(d => d != null).ToList();
        if (availableDestinations.Count > 1 && CurrentDestination != null)
        {
            availableDestinations = availableDestinations.Where(d => d != CurrentDestination).ToList();
        }

        var tier = GetCurrentTier();
        var bandedDestinations = availableDestinations.Where(d =>
        {
            float dist = Vector3.Distance(newOrderHolder.transform.position, d.transform.position);
            return dist >= tier.minDistance && dist <= tier.maxDistance;
        }).ToList();

        if (bandedDestinations.Count == 0)
        {
            float mid = (tier.minDistance + tier.maxDistance) * 0.5f;
            bandedDestinations = availableDestinations
                .OrderBy(d => Mathf.Abs(Vector3.Distance(newOrderHolder.transform.position, d.transform.position) - mid))
                .Take(Mathf.Min(3, availableDestinations.Count)).ToList();
        }

        OrderDestination newOrderDestination = bandedDestinations[UnityEngine.Random.Range(0, bandedDestinations.Count)];

        CurrentHolder = newOrderHolder;
        CurrentDestination = newOrderDestination;

        newOrderHolder.orderDestination = newOrderDestination;
        newOrderDestination.orderHolder = newOrderHolder;

        newOrderHolder.isActive = true;
        newOrderHolder.isCurrent = true;
        newOrderDestination.isActive = true;
        newOrderDestination.isPickedUp = false;

        // Time budget scales with the actual pickup-to-dropoff distance, so the clock
        // measures time for THIS delivery rather than one timer running for the whole game.
        float deliveryDistance = Vector3.Distance(newOrderHolder.transform.position, newOrderDestination.transform.position);
        float deliveryTimeLimit = Mathf.Max(minDeliveryTime, deliveryDistance * secondsPerDistanceUnit + deliveryTimeBuffer);
        OnDeliveryTimeLimitSet?.Invoke(deliveryTimeLimit);

        OnOrderAdded?.Invoke();
    }

    public void OnFinishOrder()
    {
        // Without this the score and clock keep ticking up behind the game over screen.
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

        Debug.Log("Order Finished");
        // Captured while CurrentDestination is still valid
        Vector3 deliveryPosition = CurrentDestination != null 
            ? GetPickupPoint(CurrentDestination.transform) 
            : (playerTransform != null ? playerTransform.position : transform.position);

        OnOrderFinished?.Invoke(deliveryPosition);
        AudioManager.Instance?.PlaySFX("order_delivered");

        deliveriesCompleted++;

        AddOrder();
        AudioManager.Instance?.PlaySFX("order_new");
        
        // --- GAME FEEL: Hit Stop & Confetti ---
        StartCoroutine(HitStopRoutine(0.08f));
        SpawnConfetti(deliveryPosition);
    }

    private System.Collections.IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0.05f; // Almost paused, gives a better feel than 0
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    private void SpawnConfetti(Vector3 position)
    {
        GameObject confettiObj = new GameObject("ConfettiBurst");
        confettiObj.transform.position = position + Vector3.up * 2f;
        
        ParticleSystem ps = confettiObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(12f, 28f);
        main.startSize = new ParticleSystem.MinMaxCurve(5.5f, 10f);
        
        // Near-white gradient with a faint gold shimmer so the cash texture reads as
        // itself instead of being rainbow-tinted; only alpha fades it out at the end.
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 1f, 1f), 0.0f),
                new GradientColorKey(new Color(1f, 0.92f, 0.7f), 0.5f),
                new GradientColorKey(new Color(1f, 1f, 1f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0.0f),
                new GradientAlphaKey(1f, 0.8f),
                new GradientAlphaKey(0f, 1.0f)
            }
        );
        main.startColor = new ParticleSystem.MinMaxGradient(grad);
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 75) });
        
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.2f;

        // 3D tumbling rotation for festive confetti flutter
        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.x = new ParticleSystem.MinMaxCurve(-5f, 5f);
        rotationOverLifetime.y = new ParticleSystem.MinMaxCurve(-5f, 5f);
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.6f));

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        
        // Create a simple quad mesh for the confetti, proportioned like a bill (~2.35:1)
        // so the cash texture doesn't look stretched.
        Mesh quad = new Mesh();
        const float halfWidth = 0.5f;
        const float halfHeight = 0.21f;
        quad.vertices = new Vector3[] { new Vector3(-halfWidth,-halfHeight,0), new Vector3(halfWidth,-halfHeight,0), new Vector3(-halfWidth,halfHeight,0), new Vector3(halfWidth,halfHeight,0) };
        quad.uv = new Vector2[] { new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1) };
        quad.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        quad.RecalculateNormals();

        renderer.mesh = quad;
        Material confettiMaterial = new Material(Shader.Find("Sprites/Default"));
        if (confettiTexture != null) confettiMaterial.mainTexture = confettiTexture;
        renderer.material = confettiMaterial;

        ps.Play();

        // Destroy after it finishes. The GameObject destroy doesn't touch the mesh/material
        // assets referenced by the renderer, so they'd otherwise leak every delivery.
        Destroy(confettiObj, 3f);
        Destroy(quad, 3f);
        Destroy(confettiMaterial, 3f);
    }

    void OnDrawGizmos()
    {
        DrawCurrentTargetGizmo();
    }

    // Beacon over whatever the player has to reach right now, so the objective is
    // findable in the scene view without hunting through the marker list.
    private void DrawCurrentTargetGizmo()
    {
        if (!drawTargetGizmo || !Application.isPlaying) return;

        var target = GetCurrentTargetTransform();
        if (target == null) return;

        bool carrying = IsCarryingParcel;
        Vector3 point = GetPickupPoint(target);
        Vector3 top = point + Vector3.up * targetGizmoHeight;

        Gizmos.color = carrying ? dropoffGizmoColor : pickupGizmoColor;
        Gizmos.DrawLine(point, top);
        Gizmos.DrawWireSphere(point, 1f);
        Gizmos.DrawWireSphere(top, 1.5f);

        // The volume OnFinishOrder actually tests against, so a marker that never
        // triggers is visibly a marker the car cannot overlap.
        Gizmos.matrix = Matrix4x4.TRS(point, target.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, detectionHalfExtents * 2f);
        Gizmos.matrix = Matrix4x4.identity;

        if (playerTransform != null) Gizmos.DrawLine(playerTransform.position, point);

#if UNITY_EDITOR
        float distance = playerTransform != null ? Vector3.Distance(playerTransform.position, point) : 0f;
        string label = carrying ? "ENTREGA" : "RECOGIDA";
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.Label(top + Vector3.up * 2f, $"{label}  ({distance:0} m)");
#endif
    }
}