using Game;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float maxSpeed = 12f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float brakeDeceleration = 20f;

    [Header("Steering")]
    [SerializeField] private float turnSpeed = 140f;

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

        UpdateSpeed(hasDirection, desiredDirection, dt);
        if (hasDirection) UpdateRotation(desiredDirection, dt);
        UpdateMoveDirection(dt);
        ApplyDriftFriction(dt);
        UpdateTurnRate(dt);

        Vector3 velocity = moveDirection * currentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
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

    private void UpdateSpeed(bool hasDirection, Vector3 desiredDirection, float dt)
    {
        bool isBraking = hasDirection && currentSpeed > 0.01f && Vector3.Dot(desiredDirection.normalized, moveDirection) < -0.5f;
        if (isBraking)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakeDeceleration * dt);
            return;
        }

        bool accelerating = hasDirection && InputReader.Instance.AcceleratePressed;
        float target = accelerating ? maxSpeed : 0f;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, acceleration * dt);
    }

    private void UpdateRotation(Vector3 desiredDirection, float dt)
    {
        float speedFactor = Mathf.Clamp01(currentSpeed / maxSpeed);
        if (speedFactor <= 0f) return;

        Quaternion target = Quaternion.LookRotation(desiredDirection.normalized, Vector3.up);
        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, target, turnSpeed * speedFactor * dt));
    }
}
