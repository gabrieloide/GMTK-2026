using UnityEngine;

public class OrderDestination : MonoBehaviour
{
    public bool isActive = false;
    public OrderHolder orderHolder;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            OrderManager.Instance.OnFinishOrder();
            isActive = false;
            orderHolder = null;
        }
    }
}