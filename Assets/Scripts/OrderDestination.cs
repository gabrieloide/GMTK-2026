using UnityEngine;

public class OrderDestination : MonoBehaviour
{
    public bool isActive = false;
    public OrderHolder orderHolder;

    private void Update()
    {
        if (!isActive) return;

        Physics.BoxCast(OrderManager.GetPickupPoint(transform), Vector3.one * 0.5f, transform.forward, out RaycastHit hit, transform.rotation, 0.1f);
        if (hit.collider != null && hit.collider.CompareTag("Player"))
        {
            OrderManager.Instance.OnFinishOrder();
            isActive = false;
            orderHolder = null;
        }
    }
}