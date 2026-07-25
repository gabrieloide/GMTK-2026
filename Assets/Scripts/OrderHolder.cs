using UnityEngine;

public class OrderHolder : MonoBehaviour
{
    public bool isActive = false;
    public bool isCurrent = false;
    public OrderDestination orderDestination;


    private void Update()
    {
        if (!isActive || !isCurrent) return;

        Physics.BoxCast(OrderManager.GetPickupPoint(transform), Vector3.one * 0.5f, transform.forward, out RaycastHit hit, transform.rotation, 0.1f);
        if (hit.collider != null && hit.collider.CompareTag("Player"))
        {
            isActive = false;
            isCurrent = false;
            if (orderDestination != null) orderDestination.isPickedUp = true;
        }
    }
}