using UnityEngine;

public class DeliveryArrowController : MonoBehaviour
{
    [SerializeField] private Transform arrowModel;
    private Transform player;

    private void Start()
    {
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
    }

    private void Update()
    {
        if (OrderManager.Instance == null || player == null || arrowModel == null) return;

        Vector3 toTarget = OrderManager.Instance.GetCurrentTargetPosition() - player.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        float bearing = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
        float angle = bearing - player.eulerAngles.y;
        arrowModel.localRotation = Quaternion.Euler(0f, angle, 0f);
    }
}
