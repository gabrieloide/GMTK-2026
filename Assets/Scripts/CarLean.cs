using UnityEngine;

// Banks the car's visual mesh into turns, tilts backwards (wheelie pop) when accelerating,
// and dives forward (rear wheels off the ground) when braking.
public class CarLean : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerCollision playerCollision;
    [SerializeField] private Transform carVisual;

    [Header("Lean (Turning / Roll)")]
    [Tooltip("Maximum roll angle when turning at full rate.")]
    [SerializeField] private float maxLeanAngle = 20f;
    [SerializeField] private float driftLeanBonus = 6f;
    [SerializeField] private float leanSmoothTime = 0.07f;
    [Tooltip("Yaw rate (deg/sec) considered full steering.")]
    [SerializeField] private float fullLeanYawRate = 90f;
    [Tooltip("-1: body roll (leans outward into inertia). 1: arcade bank (leans inward like a motorcycle).")]
    [SerializeField] private float leanDirection = -1f;

    [Header("Acceleration Pop (Wheelie)")]
    [Tooltip("Pitch angle (degrees) the nose tilts UP when beginning to accelerate. Negative values lift the front wheels.")]
    [SerializeField] private float accelPopAngle = -32f;
    [Tooltip("Duration in seconds of the initial wheelie pop before smoothly leveling out.")]
    [SerializeField] private float accelPopDuration = 0.5f;
    [Tooltip("Cartoon stretch along length during the acceleration kick.")]
    [SerializeField] private float accelPopStretch = 0.28f;

    [Header("Brake Pitch (Nose Down, Rear Up)")]
    [Tooltip("Pitch angle (degrees) the car tilts forward when braking. Positive values dip the nose and raise the rear wheels.")]
    [SerializeField] private float maxBrakePitchAngle = 22f;
    [SerializeField] private float brakePitchSmoothTime = 0.05f;
    [SerializeField] private float brakeRecoverSmoothTime = 0.12f;
    [Tooltip("Cartoon squash along length (and bulge in width/height) during braking.")]
    [SerializeField] private float brakeSquashAmount = 0.18f;

    [Header("Suspension Lift Compensation")]
    [Tooltip("Slight upward visual shift per degree of pitch so contact wheels stay on the road without clipping into the floor.")]
    [SerializeField] private float pitchLiftFactor = 0.035f;

    [Header("Idle Shake")]
    [SerializeField] private float idleShakeMagnitude = 0.3f;
    [SerializeField] private float idleShakeSpeed = 40f;

    private Quaternion restLocalRotation = Quaternion.identity;
    private Vector3 restLocalScale = Vector3.one;
    private Vector3 restLocalPosition = Vector3.zero;

    private float previousYaw;
    private float currentLean;
    private float leanVelocity;

    private float currentBrakePitch;
    private float brakePitchVelocity;
    private float currentBrakeSquash;
    private float brakeSquashVelocity;

    private float accelPopTimer;
    private float currentAccelPopPitch;
    private float currentAccelPopStretch;
    private bool wasAccelerating;

    private void Start()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (playerCollision == null) playerCollision = GetComponent<PlayerCollision>();
        if (carVisual == null)
        {
            var found = transform.Find("CAR-BASE");
            if (found != null) carVisual = found;
        }
        if (carVisual != null)
        {
            restLocalRotation = carVisual.localRotation;
            restLocalScale = carVisual.localScale;
            restLocalPosition = carVisual.localPosition;
        }
        previousYaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        if (carVisual == null || playerController == null) return;

        float dt = Time.deltaTime;

        // 1. Steering Roll / Lean (Z Axis in Root Space)
        float currentYaw = transform.eulerAngles.y;
        float signedDelta = Mathf.DeltaAngle(previousYaw, currentYaw);
        previousYaw = currentYaw;

        float yawRate01 = dt > 0f ? Mathf.Clamp((signedDelta / dt) / fullLeanYawRate, -1f, 1f) : 0f;
        float leanAmount = maxLeanAngle + driftLeanBonus * playerController.DriftFactor01;
        float targetLean = leanDirection * yawRate01 * leanAmount;
        currentLean = Mathf.SmoothDamp(currentLean, targetLean, ref leanVelocity, leanSmoothTime);

        // 2. Acceleration Pop (Front wheels lift up initially, then level out)
        bool isAccelerating = playerController.IsAccelerating;
        if (isAccelerating && !wasAccelerating)
        {
            accelPopTimer = accelPopDuration;
        }
        wasAccelerating = isAccelerating;

        if (accelPopTimer > 0f)
        {
            accelPopTimer -= dt;
            float t = Mathf.Clamp01(1f - (accelPopTimer / accelPopDuration));
            // Snappy rise with smooth damped decay back to zero
            float popFactor = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-t * 1.5f);
            currentAccelPopPitch = accelPopAngle * popFactor;
            currentAccelPopStretch = accelPopStretch * popFactor;
        }
        else
        {
            currentAccelPopPitch = 0f;
            currentAccelPopStretch = 0f;
        }

        // 3. Braking Pitch (Nose dives down, rear wheels lift up)
        float targetBrakePitch = playerController.IsBraking ? maxBrakePitchAngle : 0f;
        float targetBrakeSquash = playerController.IsBraking ? brakeSquashAmount : 0f;
        float smoothTime = playerController.IsBraking ? brakePitchSmoothTime : brakeRecoverSmoothTime;
        currentBrakePitch = Mathf.SmoothDamp(currentBrakePitch, targetBrakePitch, ref brakePitchVelocity, smoothTime);
        currentBrakeSquash = Mathf.SmoothDamp(currentBrakeSquash, targetBrakeSquash, ref brakeSquashVelocity, smoothTime);

        // 4. Idle Engine Shake
        float shake = 0f;
        if (playerController.SpeedFactor01 < 0.05f && Mathf.Abs(currentBrakePitch) < 1f && accelPopTimer <= 0f)
        {
            shake = Mathf.Sin(Time.time * idleShakeSpeed) * idleShakeMagnitude;
        }

        // 5. Visual Rotation in Root Space (Premultiply visual offset onto restLocalRotation)
        float totalPitch = currentBrakePitch + currentAccelPopPitch;
        Quaternion visualOffset = Quaternion.Euler(totalPitch + shake, 0f, currentLean + (shake * 0.5f));
        carVisual.localRotation = visualOffset * restLocalRotation;

        // 6. Visual Position Lift & Scale Deformations (Squash/Stretch)
        bool collisionDeforming = playerCollision != null && playerCollision.IsDeforming;
        if (!collisionDeforming)
        {
            // Vertical compensation to keep wheels on track rather than clipping through floor
            float lift = Mathf.Abs(totalPitch) * pitchLiftFactor;
            carVisual.localPosition = restLocalPosition + Vector3.up * lift;

            // Squash & Stretch (CAR-BASE model has its forward length along local X, sideways width along local Z)
            float totalStretch = currentAccelPopStretch - currentBrakeSquash;
            carVisual.localScale = new Vector3(
                restLocalScale.x * (1f + totalStretch),        // Length (Local X)
                restLocalScale.y * (1f - totalStretch * 0.5f), // Height (Local Y)
                restLocalScale.z * (1f - totalStretch * 0.5f)  // Width  (Local Z)
            );
        }
    }
}
