using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;


public class OrderManager : MonoBehaviour
{
    [SerializeField] public Vector3 offset = Vector3.zero;
    private List<OrderHolder> orderTransform;
    private List<OrderDestination> orderDestination;

    
    private Queue<OrderHolder> activesOrderHolder = new Queue<OrderHolder>();
    private Queue<OrderDestination> activesOrderDestination = new Queue<OrderDestination>();


    public static Action OnOrderFinished;
    public static Action OnOrderAdded;

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
        var newOrderDestination = availableOrderDestinations[UnityEngine.Random.Range(0, availableOrderDestinations.Count)];
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
        Debug.Log("Order Finished");
        OnOrderFinished?.Invoke();

        activesOrderHolder.Dequeue();
        activesOrderDestination.Dequeue();

        if (activesOrderHolder.Count > 0)
        {
            activesOrderHolder.Peek().isCurrent = true;
        }

        AddOrder();
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