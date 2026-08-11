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

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.7f); // Green for Pickup
        
        OrderManager manager = OrderManager.Instance;
#if UNITY_EDITOR
        if (manager == null) manager = FindObjectOfType<OrderManager>();
#endif
        
        Vector3 point = transform.position;
        Vector3 size = new Vector3(6f, 20f, 6f); // Default (halfExtents * 2)

        if (manager != null)
        {
            // Use the manager's offset to find the exact point
            point = point + transform.TransformDirection(Vector3.forward + manager.offset);
            size = manager.detectionHalfExtents * 2f;
        }

        Gizmos.matrix = Matrix4x4.TRS(point, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = Matrix4x4.identity;
        
        Gizmos.DrawLine(point, point + Vector3.up * 10f);
    }
}