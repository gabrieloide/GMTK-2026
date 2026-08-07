using UnityEngine;
using System;
using Code.Scripts.Audio;

public class OrderHolder : MonoBehaviour
{
    public bool isActive = false;
    public bool isCurrent = false;
    public OrderDestination orderDestination;

    [SerializeField] private Renderer markerVisual;

    public static Action<Vector3> OnPickup;

    private void Update()
    {
        if (markerVisual != null) markerVisual.enabled = isActive && isCurrent;
        if (!isActive || !isCurrent) return;

        Vector3 pickupPoint = OrderManager.GetPickupPoint(transform);
        if (OrderManager.IsPlayerAtPoint(pickupPoint, transform.rotation))
        {
            isActive = false;
            isCurrent = false;
            if (orderDestination != null) orderDestination.isPickedUp = true;
            AudioManager.Instance.PlaySFX("order_pickup");
            OnPickup?.Invoke(pickupPoint);
        }
    }
}