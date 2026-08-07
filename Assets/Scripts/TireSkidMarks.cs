using UnityEngine;

// Two TrailRenderers, one per rear wheel, laid flat on the ground (alignment = TransformZ
// rather than the default camera-facing ribbon). Anchored directly under the physics root
// rather than the visual mesh, so CarLean's tilt and PlayerCollision's hop never drag the
// marks off the ground they are meant to be scarring.
[RequireComponent(typeof(PlayerController))]
public class TireSkidMarks : MonoBehaviour
{
    [SerializeField] private TrailRenderer leftTrail;
    [SerializeField] private TrailRenderer rightTrail;

    [Tooltip("Loss of grip (PlayerController.DriftFactor01) where the tyres start to mark. " +
             "Matches CarSmoke/PlayerAudio's threshold so smoke, screech and marks arrive together.")]
    [SerializeField, Range(0f, 1f)] private float driftThreshold = 0.35f;

    [Tooltip("Below this fraction of max speed the tyres cannot mark, so a car nudged " +
             "sideways at a standstill stays clean.")]
    [SerializeField, Range(0f, 1f)] private float minSpeedFactor = 0.1f;

    private PlayerController playerController;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        bool shouldMark = !playerController.IsReversing &&
                           playerController.SpeedFactor01 > minSpeedFactor &&
                           playerController.DriftFactor01 >= driftThreshold;

        if (leftTrail != null) leftTrail.emitting = shouldMark;
        if (rightTrail != null) rightTrail.emitting = shouldMark;
    }
}
