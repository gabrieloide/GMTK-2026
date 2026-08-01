using Game;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float maxSpeed = 12f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float brakeDeceleration = 20f;

    [Tooltip("How opposed to the current heading WASD has to be to count as braking " +
             "instead of steering. -1 = only dead backwards, 0 = anything past sideways.")]
    [SerializeField, Range(-1f, 0f)] private float brakeInputAlignment = -0.8f;

    [Tooltip("On: braking beats the throttle, so pressing the opposite key stops the " +
             "car without releasing accelerate. Off: the throttle has to be released.")]
    [SerializeField] private bool brakeWhileAccelerating = true;

    [Header("Reverse")]
    [Tooltip("Keep steering into the back of a stopped car and it backs up. Fraction of maxSpeed " +
             "the car reaches in reverse.")]
    [SerializeField, Range(0f, 1f)] private float reverseSpeedFactor = 0.35f;

    [Tooltip("How opposed to the car's nose WASD has to be to engage reverse. Looser than the " +
             "brake so that braking to a stop rolls straight into backing up.")]
    [SerializeField, Range(-1f, 0f)] private float reverseInputAlignment = -0.5f;

    [Tooltip("Reverse only engages below this speed, so it can never fight a car still rolling forward.")]
    [SerializeField] private float reverseEntrySpeed = 0.5f;

    [Header("Steering")]
    [SerializeField] private float turnSpeed = 140f;

    [Tooltip("Steering authority given while the throttle is held even at a standstill, " +
             "so the car pulls away towards WASD instead of driving off in its old heading.")]
    [SerializeField, Range(0f, 1f)] private float throttleTurnAssist = 0.35f;

    [Header("Drift")]
    [SerializeField] private float grip = 720f;
    [SerializeField] private float handbrakeGrip = 40f;
    [SerializeField] private float driftFriction = 6f;

    [Header("Knockback")]
    [SerializeField] private float knockbackDrag = 20f;

    private Rigidbody rb;
    public float currentSpeed;
    private Vector3 moveDirection;
    private float knockbackTimer;
    private bool reversing;

    public bool IsReversing => reversing;

    public float MaxSpeed => maxSpeed;
    public float SpeedFactor01 => maxSpeed > 0f ? Mathf.Clamp01(currentSpeed / maxSpeed) : 0f;
    public float DriftFactor01 { get; private set; }
    public float TurnRate01 { get; private set; }
    private float previousYaw;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        moveDirection = transform.forward;
        previousYaw = transform.eulerAngles.y;
    }

    public void ApplyKnockback(Vector3 direction, float speed, float duration)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        currentSpeed = 0f;
        moveDirection = direction;
        knockbackTimer = duration;
        reversing = false;
        rb.linearVelocity = new Vector3(direction.x * speed, rb.linearVelocity.y, direction.z * speed);
    }

    private void FixedUpdate()
    {
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            ApplyKnockbackDrag(Time.fixedDeltaTime);
            return;
        }

        Vector2 input = InputReader.Instance.MoveInput;
        Vector3 desiredDirection = new Vector3(input.x, 0f, input.y);
        bool hasDirection = desiredDirection.sqrMagnitude > 0.01f;
        float dt = Time.fixedDeltaTime;

        // Braking is inferred from the stick, so the cone has to be narrow enough that
        // a hard turn is never mistaken for it. Mid-drift the nose and the velocity
        // point somewhere different - opposing either one counts, otherwise the brake
        // would stop answering exactly when the car is sliding.
        bool canBrake = brakeWhileAccelerating || !InputReader.Instance.AcceleratePressed;
        float opposition = hasDirection
            ? Mathf.Min(Vector3.Dot(desiredDirection.normalized, moveDirection),
                        Vector3.Dot(desiredDirection.normalized, transform.forward))
            : 0f;
        bool isBraking = canBrake && hasDirection && currentSpeed > 0.01f &&
                         opposition <= brakeInputAlignment;

        if (UpdateReverse(hasDirection, desiredDirection, dt)) return;

        UpdateSpeed(isBraking, dt);
        // Steering is suppressed while braking: otherwise pressing back would swing
        // the car around towards the key instead of reading as a brake.
        if (hasDirection && !isBraking) UpdateRotation(desiredDirection, dt);
        UpdateMoveDirection(dt);
        ApplyDriftFriction(dt);
        UpdateTurnRate(dt);

        Vector3 velocity = moveDirection * currentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
    }

    /// <summary>
    /// Backing up is the tail of the brake: hold the key that stopped the car and it keeps
    /// going the other way. Returns true when reverse owns this frame's velocity.
    /// </summary>
    private bool UpdateReverse(bool hasDirection, Vector3 desiredDirection, float dt)
    {
        bool wantsReverse = hasDirection &&
                            !InputReader.Instance.AcceleratePressed &&
                            Vector3.Dot(desiredDirection.normalized, transform.forward) <= reverseInputAlignment;

        if (!reversing)
        {
            if (!wantsReverse || currentSpeed > reverseEntrySpeed) return false;
            reversing = true;
            currentSpeed = 0f;
        }

        float target = wantsReverse ? maxSpeed * reverseSpeedFactor : 0f;
        float rate = wantsReverse ? acceleration : brakeDeceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * dt);

        // Straight back, no steering: with WASD picking a world direction rather than a wheel
        // angle, turning while reversing would just spin the car on the spot.
        moveDirection = -transform.forward;
        DriftFactor01 = 0f;
        UpdateTurnRate(dt);

        Vector3 velocity = moveDirection * currentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;

        // Rolled to a stop with the key already released - hand the car back to normal driving.
        if (!wantsReverse && currentSpeed <= 0.01f)
        {
            reversing = false;
            moveDirection = transform.forward;
        }

        return true;
    }

    private void ApplyKnockbackDrag(float dt)
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        flat = Vector3.MoveTowards(flat, Vector3.zero, knockbackDrag * dt);
        rb.linearVelocity = new Vector3(flat.x, velocity.y, flat.z);
    }

    private void UpdateMoveDirection(float dt)
    {
        if (currentSpeed <= 0.01f)
        {
            moveDirection = transform.forward;
            return;
        }

        float currentGrip = InputReader.Instance.HandbrakeHeld ? handbrakeGrip : grip;
        moveDirection = Vector3.RotateTowards(moveDirection, transform.forward, currentGrip * Mathf.Deg2Rad * dt, 0f);
    }

    private void ApplyDriftFriction(float dt)
    {
        float slide = Mathf.Clamp01(Vector3.Angle(moveDirection, transform.forward) / 90f);
        DriftFactor01 = slide;
        if (slide <= 0f) return;

        currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, driftFriction * slide * dt);
    }

    private void UpdateTurnRate(float dt)
    {
        float currentYaw = transform.eulerAngles.y;
        float delta = Mathf.Abs(Mathf.DeltaAngle(previousYaw, currentYaw));
        previousYaw = currentYaw;
        TurnRate01 = turnSpeed > 0f ? Mathf.Clamp01((delta / dt) / turnSpeed) : 0f;
    }

    private void UpdateSpeed(bool isBraking, float dt)
    {
        // Steering back into the car is the brake, and it beats the throttle: holding
        // the accelerate button must not stop it from working.
        if (isBraking)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakeDeceleration * dt);
            return;
        }

        // Otherwise the throttle is the accelerate button on its own: the car pulls
        // away along its own nose, and WASD only decides where that nose points.
        float target = InputReader.Instance.AcceleratePressed ? maxSpeed : 0f;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, acceleration * dt);
    }

    private void UpdateRotation(Vector3 desiredDirection, float dt)
    {
        float speedFactor = Mathf.Clamp01(currentSpeed / maxSpeed);
        if (InputReader.Instance.AcceleratePressed) speedFactor = Mathf.Max(speedFactor, throttleTurnAssist);
        if (speedFactor <= 0f) return;

        Quaternion target = Quaternion.LookRotation(desiredDirection.normalized, Vector3.up);
        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, target, turnSpeed * speedFactor * dt));
    }
}
