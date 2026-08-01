using UnityEngine;

public class DeliveryArrowController : MonoBehaviour
{
    /// <summary>Which local axis of the art is its tip. Imported models rarely agree on this.</summary>
    public enum NoseAxis { Up, Down, Forward, Back, Right, Left }

    [SerializeField] private Transform arrowModel;

    [Header("Model orientation")]
    [Tooltip("Where the tip points when the model sits at rotation 0,0,0. Blender exports usually " +
             "come in nose-up; a model authored for Unity would be Forward.")]
    [SerializeField] private NoseAxis modelNose = NoseAxis.Up;

    [Tooltip("Degrees to spin the arrow around its own pointing axis, for when the art ends up " +
             "edge-on to the camera instead of face-up.")]
    [SerializeField] private float spin = 0f;

    [Tooltip("Degrees to lift the tip off the ground plane. Positive raises the nose towards the sky.")]
    [SerializeField] private float tilt = 0f;

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

        // Aim in world space rather than as a yaw off the player's heading, so the arrow keeps
        // working wherever it is parented - right now it hangs off the car.
        Quaternion aim = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

        // Then bend the art's own axis onto that aim: whatever the importer decided the tip
        // was, this maps it onto +Z before the aim is applied.
        Quaternion noseToForward = Quaternion.FromToRotation(NoseVector(modelNose), Vector3.forward);

        arrowModel.rotation = aim
            * Quaternion.Euler(-tilt, 0f, 0f)
            * Quaternion.AngleAxis(spin, Vector3.forward)
            * noseToForward;
    }

    private static Vector3 NoseVector(NoseAxis axis)
    {
        switch (axis)
        {
            case NoseAxis.Up: return Vector3.up;
            case NoseAxis.Down: return Vector3.down;
            case NoseAxis.Back: return Vector3.back;
            case NoseAxis.Right: return Vector3.right;
            case NoseAxis.Left: return Vector3.left;
            default: return Vector3.forward;
        }
    }
}
