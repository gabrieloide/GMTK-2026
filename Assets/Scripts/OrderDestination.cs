using UnityEngine;

public class OrderDestination : MonoBehaviour
{
    public bool isActive = false;
    public bool isPickedUp = false;
    public OrderHolder orderHolder;

    [SerializeField] private Renderer markerVisual;

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmo = true;
    [SerializeField] private float gizmoBeamHeight = 12f;

    private void Update()
    {
        if (markerVisual != null) markerVisual.enabled = isActive && isPickedUp;
        if (!isActive || !isPickedUp) return;

        if (OrderManager.IsPlayerAtPoint(OrderManager.GetPickupPoint(transform), transform.rotation))
        {
            isActive = false;
            isPickedUp = false;
            orderHolder = null;
            if (OrderManager.Instance != null)
            {
                OrderManager.Instance.OnFinishOrder();
            }
        }
    }

    // Every drop-off point is drawn dim so the layout is readable while editing;
    // the one the player owes a parcel to lights up.
    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;

        bool isTarget = isActive && isPickedUp;
        OrderManager manager = OrderManager.Instance;
#if UNITY_EDITOR
        if (manager == null) manager = FindObjectOfType<OrderManager>();
#endif
        
        Vector3 point = transform.position;
        Vector3 size = new Vector3(6f, 20f, 6f);
        if (manager != null)
        {
            point = point + transform.TransformDirection(Vector3.forward + manager.offset);
            size = manager.detectionHalfExtents * 2f;
        }

        Vector3 top = point + Vector3.up * gizmoBeamHeight;

        Gizmos.color = isTarget ? new Color(1f, 0.4f, 0.1f) : new Color(1f, 0.4f, 0.1f, 0.7f);
        Gizmos.DrawLine(point, top);
        
        // Draw the exact detection box
        Gizmos.matrix = Matrix4x4.TRS(point, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = Matrix4x4.identity;

        // Little pennant on top of the pole - reads as a drop-off flag from far away.
        Gizmos.DrawLine(top, top + new Vector3(1.5f, -1f, 0f));
        Gizmos.DrawLine(top + new Vector3(1.5f, -1f, 0f), point + Vector3.up * (gizmoBeamHeight - 2f));
    }
}