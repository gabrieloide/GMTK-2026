using UnityEngine;

public class OrderDestination : MonoBehaviour
{
    public bool isActive = false;
    public bool isPickedUp = false;
    public OrderHolder orderHolder;

    [SerializeField] private Renderer markerVisual;

    private void Update()
    {
        if (markerVisual != null) markerVisual.enabled = isActive && isPickedUp;
        if (!isActive || !isPickedUp) return;

        if (OrderManager.IsPlayerAtPoint(OrderManager.GetPickupPoint(transform), transform.rotation))
        {
            OrderManager.Instance.OnFinishOrder();
            isActive = false;
            isPickedUp = false;
            orderHolder = null;
        }
    }
}