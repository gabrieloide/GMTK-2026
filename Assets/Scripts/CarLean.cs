using UnityEngine;

// Banks the car's visual mesh into turns and drifts - the tilt that sells cornering
// speed, the way arcade racers since OutRun lean their car sprites/models. Only the
// visual child (CAR-BASE) ever rotates here; the Rigidbody's own rotation stays frozen
// and level so steering, grip and collision math never see the tilt.
public class CarLean : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Transform carVisual;

    [Header("Lean (turning / drift)")]
    [SerializeField] private float maxLeanAngle = 12f;
    [SerializeField] private float driftLeanBonus = 8f;
    [SerializeField] private float leanSmoothTime = 0.15f;
    // Yaw rate (deg/sec) that counts as a "full" lean - lower than PlayerController's
    // own turnSpeed so normal cornering already banks visibly, not just hairpins.
    [SerializeField] private float fullLeanYawRate = 60f;
    // Flip to -1 if the car banks into turns instead of away from them and outward
    // (weight-transfer) lean reads better for this car - it's a one-line taste call.
    [SerializeField] private float leanDirection = -1f;

    [Header("Pitch (brake / accelerate)")]
    [SerializeField] private float maxPitchAngle = 6f;
    // Units/sec/sec of speed change that counts as a "full" pitch - independent of
    // PlayerController's own accel/brake tuning so this can be dialed in by feel alone.
    [SerializeField] private float fullPitchAccelRate = 15f;
    [SerializeField] private float pitchSmoothTime = 0.12f;
    // Positive: nose dips under braking, lifts under acceleration. Flip to -1 to reverse.
    [SerializeField] private float pitchDirection = 1f;

    private Quaternion restLocalRotation = Quaternion.identity;
    private float previousYaw;
    private float currentLean;
    private float leanVelocity;
    private float previousSpeed;
    private float currentPitch;
    private float pitchVelocity;

    private void Start()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (carVisual == null)
        {
            var found = transform.Find("CAR-BASE");
            if (found != null) carVisual = found;
        }
        if (carVisual != null) restLocalRotation = carVisual.localRotation;
        previousYaw = transform.eulerAngles.y;
        previousSpeed = playerController != null ? playerController.currentSpeed : 0f;
    }

    private void Update()
    {
        if (carVisual == null || playerController == null) return;

        float dt = Time.deltaTime;

        float currentYaw = transform.eulerAngles.y;
        float signedDelta = Mathf.DeltaAngle(previousYaw, currentYaw);
        previousYaw = currentYaw;

        float yawRate01 = dt > 0f ? Mathf.Clamp((signedDelta / dt) / fullLeanYawRate, -1f, 1f) : 0f;

        float leanAmount = maxLeanAngle + driftLeanBonus * playerController.DriftFactor01;
        float targetLean = leanDirection * yawRate01 * leanAmount;
        currentLean = Mathf.SmoothDamp(currentLean, targetLean, ref leanVelocity, leanSmoothTime);

        float speedDelta = playerController.currentSpeed - previousSpeed;
        previousSpeed = playerController.currentSpeed;

        float accel01 = dt > 0f ? Mathf.Clamp((speedDelta / dt) / fullPitchAccelRate, -1f, 1f) : 0f;
        float targetPitch = pitchDirection * accel01 * maxPitchAngle;
        currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, pitchSmoothTime);

        carVisual.localRotation = restLocalRotation * Quaternion.Euler(currentPitch, 0f, currentLean);
    }
}
