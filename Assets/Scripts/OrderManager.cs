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
        public float timeBonus;
    }

    [SerializeField] public Vector3 offset = Vector3.zero;

    // Tall on Y so a marker sitting near street level still overlaps the car's
    // collider (which is offset well above its transform), wide enough on XZ that
    // driving past at speed reliably registers.
    [SerializeField] private Vector3 detectionHalfExtents = new Vector3(3f, 10f, 3f);

    [SerializeField]
    private DifficultyTier[] difficultyTiers = new DifficultyTier[]
    {
        new DifficultyTier { deliveriesRequired = 0, minDistance = 40f, maxDistance = 100f, timeBonus = 15f },
        new DifficultyTier { deliveriesRequired = 3, minDistance = 80f, maxDistance = 160f, timeBonus = 13f },
        new DifficultyTier { deliveriesRequired = 6, minDistance = 140f, maxDistance = 240f, timeBonus = 11f },
        new DifficultyTier { deliveriesRequired = 10, minDistance = 200f, maxDistance = 350f, timeBonus = 9f },
        new DifficultyTier { deliveriesRequired = 15, minDistance = 300f, maxDistance = 500f, timeBonus = 7f },
    };

    private List<OrderHolder> orderTransform;
    private List<OrderDestination> orderDestination;


    private Queue<OrderHolder> activesOrderHolder = new Queue<OrderHolder>();
    private Queue<OrderDestination> activesOrderDestination = new Queue<OrderDestination>();

    private int deliveriesCompleted = 0;
    public int DeliveriesCompleted => deliveriesCompleted;

    public static Action OnOrderFinished;
    public static Action OnOrderAdded;
    public static Action<float> OnTimeBonusAwarded;

    private Transform playerTransform;
    public static OrderManager Instance { get; private set; }
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
        orderTransform = FindObjectsByType<OrderHolder>().ToList();
        orderDestination = FindObjectsByType<OrderDestination>().ToList();
    }

    void Start()
    {

        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        AddOrder();
    }
    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

        if (activesOrderHolder.Count == 0)
        {
            Debug.LogWarning("No active orders available.");
            return;
        }

        var currentOrderHolder = activesOrderHolder.Peek();
        if (activesOrderHolder.Count == 1 && Vector3.Distance(playerTransform.position, GetPickupPoint(currentOrderHolder.transform)) < 1f)
        {
            AddOrder();
        }
    }

    public static Vector3 GetPickupPoint(Transform target)
    {
        return target.position + target.TransformDirection(Vector3.forward + Instance.offset);
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
        if (activesOrderHolder.Count == 0) return transform.position;

        var holder = activesOrderHolder.Peek();
        var destination = activesOrderDestination.Count > 0 ? activesOrderDestination.Peek() : null;

        if (destination != null && destination.isPickedUp) return GetPickupPoint(destination.transform);
        return GetPickupPoint(holder.transform);
    }

    private DifficultyTier GetCurrentTier()
    {
        var tier = difficultyTiers[0];
        foreach (var candidate in difficultyTiers)
        {
            if (deliveriesCompleted >= candidate.deliveriesRequired) tier = candidate;
        }
        return tier;
    }

    private static Mesh capsuleGizmoMesh;
    private static void DrawWireCapsule(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (capsuleGizmoMesh == null)
        {
            capsuleGizmoMesh = Resources.GetBuiltinResource<Mesh>("Capsule.fbx");
        }
        Gizmos.DrawWireMesh(capsuleGizmoMesh, position, rotation, scale);
    }

    public void AddOrder()
    {
        bool becomesCurrent = activesOrderHolder.Count == 0;

        var availableOrderHolders = orderTransform.Where(o => !activesOrderHolder.Contains(o)).ToList();
        if (availableOrderHolders.Count == 0) availableOrderHolders = orderTransform;
        var newOrderHolder = availableOrderHolders[UnityEngine.Random.Range(0, availableOrderHolders.Count)];
        activesOrderHolder.Enqueue(newOrderHolder);

        var availableOrderDestinations = orderDestination.Where(o => !activesOrderDestination.Contains(o)).ToList();
        if (availableOrderDestinations.Count == 0) availableOrderDestinations = orderDestination;

        var tier = GetCurrentTier();
        var bandedDestinations = availableOrderDestinations.Where(d =>
        {
            float dist = Vector3.Distance(newOrderHolder.transform.position, d.transform.position);
            return dist >= tier.minDistance && dist <= tier.maxDistance;
        }).ToList();
        if (bandedDestinations.Count == 0)
        {
            float mid = (tier.minDistance + tier.maxDistance) * 0.5f;
            bandedDestinations = availableOrderDestinations
                .OrderBy(d => Mathf.Abs(Vector3.Distance(newOrderHolder.transform.position, d.transform.position) - mid))
                .Take(Mathf.Min(3, availableOrderDestinations.Count)).ToList();
        }
        var newOrderDestination = bandedDestinations[UnityEngine.Random.Range(0, bandedDestinations.Count)];
        activesOrderDestination.Enqueue(newOrderDestination);

        newOrderHolder.orderDestination = newOrderDestination;
        newOrderDestination.orderHolder = newOrderHolder;

        newOrderHolder.isActive = true;
        newOrderHolder.isCurrent = becomesCurrent;
        newOrderDestination.isActive = true;
        newOrderDestination.isPickedUp = false;

        OnOrderAdded?.Invoke();
    }
    public void OnFinishOrder()
    {
        // Without this the score and clock keep ticking up behind the game over screen.
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

        Debug.Log("Order Finished");
        OnOrderFinished?.Invoke();
        AudioManager.Instance.PlaySFX("order_delivered");

        deliveriesCompleted++;
        OnTimeBonusAwarded?.Invoke(GetCurrentTier().timeBonus);
        AudioManager.Instance.PlaySFX("time_bonus");

        activesOrderHolder.Dequeue();
        activesOrderDestination.Dequeue();

        if (activesOrderHolder.Count > 0)
        {
            activesOrderHolder.Peek().isCurrent = true;
        }

        AddOrder();
        AudioManager.Instance.PlaySFX("order_new");
    }
    void OnDrawGizmos()
    {


        if(orderTransform == null) return;
        foreach (var order in orderTransform)
        {
            if(order == null) continue;

            Gizmos.color = Color.green;
            DrawWireCapsule(GetPickupPoint(order.transform), order.transform.rotation, Vector3.one * 8f);

            if (order.orderDestination == null) continue;

            if (order.isActive)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(GetPickupPoint(order.transform), 0.5f);
            }

            if (order.orderDestination.isActive)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(GetPickupPoint(order.orderDestination.transform), 0.5f);

                Gizmos.color = Color.turquoise;
                Gizmos.DrawLine(GetPickupPoint(order.transform), GetPickupPoint(order.orderDestination.transform));
            }
        }
    }
}