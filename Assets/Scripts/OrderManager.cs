using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;


public class OrderManager : MonoBehaviour
{
    private List<OrderHolder> orderTransform;
    private List<OrderDestination> orderDestination;

    
    private Queue<OrderHolder> activesOrderHolder;
    private Queue<OrderDestination> activesOrderDestination;


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
    }

    void Start()
    {
        orderTransform = FindObjectsByType<OrderHolder>().ToList();
        orderDestination = FindObjectsByType<OrderDestination>().ToList();
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        AddOrder();
    }
    private void Update()
    {
        if(Vector3.Distance(playerTransform.position, activesOrderHolder.Last().transform.position) < 1f && activesOrderHolder.Count == 1)
        {
            AddOrder();
        }
    }

    public void AddOrder()
    {
        activesOrderHolder.Enqueue(orderTransform[UnityEngine.Random.Range(0, orderTransform.Count)]);
        activesOrderDestination.Enqueue(orderDestination[UnityEngine.Random.Range(0, orderDestination.Count)]);

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
        foreach (var order in orderTransform)
        {
            if(order == null) continue;
            if(order.isActive == false) continue;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(order.transform.position, 0.5f);

            if(order.orderDestination == null) continue;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(order.orderDestination.transform.position, 0.5f);

            Gizmos.color = Color.turquoise;
            Gizmos.DrawLine(order.transform.position, order.orderDestination.transform.position);
        }
    }
}