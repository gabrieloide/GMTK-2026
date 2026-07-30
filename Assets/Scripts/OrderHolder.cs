using UnityEngine;
using Code.Scripts.Audio;

public class OrderHolder : MonoBehaviour
{
    public bool isActive = false;
    public bool isCurrent = false;
    public OrderDestination orderDestination;

    [SerializeField] private Renderer markerVisual;

    private void Update()
    {
        if (markerVisual != null) markerVisual.enabled = isActive && isCurrent;
        if (!isActive || !isCurrent) return;

        if (OrderManager.IsPlayerAtPoint(OrderManager.GetPickupPoint(transform), transform.rotation))
        {
            isActive = false;
            isCurrent = false;
            if (orderDestination != null) orderDestination.isPickedUp = true;
            AudioManager.Instance.PlaySFX("order_pickup");
        }
    }
}