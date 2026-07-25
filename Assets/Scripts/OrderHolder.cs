using UnityEngine;

public class OrderHolder : MonoBehaviour
{
    public bool isActive = false;
    public OrderDestination orderDestination;


    private void Update()
    {
        if (!isActive) return;

        Physics.BoxCast(OrderManager.GetPickupPoint(transform), Vector3.one * 0.5f, transform.forward, out RaycastHit hit, transform.rotation, 0.1f);
        if (hit.collider != null && hit.collider.CompareTag("Player"))
        {
            Debug.Log("Player is in range of the order holder.");
            // Handle player interaction
        }
    }
}