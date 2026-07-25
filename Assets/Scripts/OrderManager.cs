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
        if(Vector3.Distance(playerTransform.position, activesOrderHolder.Last().transform.position) < 1f && activesOrderHolder.Count == 1)
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
        var availableOrderHolders = orderTransform[UnityEngine.Random.Range(0, orderTransform.Count)];
        activesOrderHolder.Enqueue(availableOrderHolders);

        var availableOrderDestinations = orderDestination[UnityEngine.Random.Range(0, orderDestination.Count)];
        activesOrderDestination.Enqueue(availableOrderDestinations);

        var currentOrderHolder = activesOrderHolder.Last();
        var currentOrderDestination = activesOrderDestination.Last();

        currentOrderHolder.orderDestination = currentOrderDestination;
        currentOrderDestination.orderHolder = currentOrderHolder;

        currentOrderHolder.isActive = true;
        currentOrderDestination.isActive = true;

        OnOrderAdded?.Invoke();
    }
    public void OnFinishOrder()
    {
        Debug.Log("Order Finished");
        OnOrderFinished?.Invoke();

        activesOrderHolder.Dequeue();
        activesOrderDestination.Dequeue();

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

            if(order.isActive == false) continue;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(GetPickupPoint(order.transform), 0.5f);

            if(order.orderDestination == null) continue;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(GetPickupPoint(order.orderDestination.transform), 0.5f);

            Gizmos.color = Color.turquoise;
            Gizmos.DrawLine(GetPickupPoint(order.transform), GetPickupPoint(order.orderDestination.transform));


        }
    }
}