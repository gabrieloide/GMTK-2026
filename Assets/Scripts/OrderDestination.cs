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
            OrderManager.Instance.OnFinishOrder();
            isActive = false;
            isPickedUp = false;
            orderHolder = null;
        }
    }

    // Every drop-off point is drawn dim so the layout is readable while editing;
    // the one the player owes a parcel to lights up.
    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;

        bool isTarget = isActive && isPickedUp;
        Vector3 point = OrderManager.GetPickupPoint(transform);
        Vector3 top = point + Vector3.up * gizmoBeamHeight;

        Gizmos.color = isTarget ? new Color(1f, 0.4f, 0.1f) : new Color(1f, 0.4f, 0.1f, 0.25f);
        Gizmos.DrawLine(point, top);
        Gizmos.DrawWireSphere(point, isTarget ? 2f : 1f);

        // Little pennant on top of the pole - reads as a drop-off flag from far away.
        Gizmos.DrawLine(top, top + new Vector3(1.5f, -1f, 0f));
        Gizmos.DrawLine(top + new Vector3(1.5f, -1f, 0f), point + Vector3.up * (gizmoBeamHeight - 2f));
    }
}