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

    [Tooltip("On: braking beats the throttle, so pressing the opposite key stops the car " +
             "without releasing accelerate - but then every hard change of direction reads " +
             "as a brake, because WASD picks a heading rather than a wheel angle. " +
             "Off: with the throttle held WASD only steers, and stopping means letting go " +
             "of accelerate first.")]
    [SerializeField] private bool brakeWhileAccelerating = false;

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

    [Header("Drift boost (mini-turbo)")]
    [Tooltip("Hold a slide and the car banks a boost, spent the moment the drift ends. " +
             "The streets are too tight to drift for fun, so this is what pays for it: " +
             "the charge is only worth having if you keep the slide going past the point " +
             "where a wall is a real risk, and hitting one throws the charge away.")]
    [SerializeField] private bool driftBoostEnabled = true;

    [Tooltip("Loss of grip needed for the drift to count as charging. Matches PlayerAudio " +
             "and CarSmoke, so the boost charges exactly while the tyres are screeching.")]
    [SerializeField, Range(0f, 1f)] private float boostDriftThreshold = 0.35f;

    [Tooltip("Fraction of max speed below which a slide charges nothing, so shuffling " +
             "sideways at walking pace cannot bank a turbo.")]
    [SerializeField, Range(0f, 1f)] private float boostMinSpeedFactor = 0.3f;

    [Tooltip("How long the slide is allowed to dip below the threshold before the charge " +
             "is spent. Bumping over a kerb mid-drift should not count as ending it.")]
    [SerializeField] private float boostChargeGrace = 0.2f;

    [Tooltip("Charge levels, shortest first. Each one is worth more than the last, " +
             "so a long slide beats two short ones.")]
    [SerializeField] private DriftBoostTier[] boostTiers =
    {
        new DriftBoostTier { chargeTime = 0.6f, speedMultiplier = 1.3f, duration = 0.8f },
        new DriftBoostTier { chargeTime = 1.4f, speedMultiplier = 1.6f, duration = 1.4f }
    };

    [Tooltip("A crash cancels both the charge in the bank and a boost already running - " +
             "the other half of the trade, and the reason a greedy drift can cost you.")]
    [SerializeField] private bool crashCancelsBoost = true;

    [Header("Knockback")]
    [SerializeField] private float knockbackDrag = 20f;

    /// <summary>One charge level of the mini-turbo: how long you have to hold the slide,
    /// and what the slide buys.</summary>
    [System.Serializable]
    public struct DriftBoostTier
    {
        [Tooltip("Seconds of sustained drift needed to reach this level.")]
        public float chargeTime;

        [Tooltip("Top speed while the boost lasts, as a multiple of maxSpeed.")]
        public float speedMultiplier;

        [Tooltip("Seconds the raised top speed is held before the car settles back down.")]
        public float duration;
    }

    private Rigidbody rb;
    public float currentSpeed;
    private Vector3 moveDirection;
    private float knockbackTimer;
    private bool reversing;

    private float driftCharge;
    private float chargeGraceTimer;
    private float boostTimer;
    private float boostDuration;
    private float boostMultiplier = 1f;

    public bool IsReversing => reversing;

    public float MaxSpeed => maxSpeed;

    /// <summary>Top speed right now, which a live boost raises above <see cref="MaxSpeed"/>.</summary>
    public float TopSpeed => maxSpeed * (boostTimer > 0f ? boostMultiplier : 1f);

    public float SpeedFactor01 => maxSpeed > 0f ? Mathf.Clamp01(currentSpeed / maxSpeed) : 0f;
    public float DriftFactor01 { get; private set; }
    public float TurnRate01 { get; private set; }

    /// <summary>Charge level banked so far: 0 when the slide has not paid off yet,
    /// otherwise the 1-based tier the driver would get for ending the drift now.</summary>
    public int DriftChargeTier { get; private set; }

    /// <summary>Progress towards the next charge level, 1 once the top one is reached.
    /// Meant for a spark/meter readout.</summary>
    public float DriftCharge01 { get; private set; }

    public bool IsBoosting => boostTimer > 0f;

    /// <summary>How much of the running boost is left, 0 when none is.</summary>
    public float Boost01 => boostDuration > 0f ? Mathf.Clamp01(boostTimer / boostDuration) : 0f;

    /// <summary>Fired the instant a boost starts, with its 1-based tier - for the smoke,
    /// the audio and anything else that wants to sell the kick.</summary>
    public event System.Action<int> BoostFired;

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
        if (crashCancelsBoost) CancelDriftBoost();
        rb.linearVelocity = new Vector3(direction.x * speed, rb.linearVelocity.y, direction.z * speed);
    }

    private void FixedUpdate()
    {
        if (boostTimer > 0f) boostTimer = Mathf.Max(0f, boostTimer - Time.fixedDeltaTime);

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

        // Braking is inferred from the stick, so it is measured against where the car is
        // actually travelling: a brake opposes motion, not aim. Testing the nose as well
        // meant that mid-drift - where the nose and the velocity point somewhere quite
        // different - steering further round the corner read as a brake, because the stick
        // was opposing a nose the car had already stopped following.
        bool canBrake = brakeWhileAccelerating || !InputReader.Instance.AcceleratePressed;
        float opposition = hasDirection
            ? Vector3.Dot(desiredDirection.normalized, moveDirection)
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
        // After the friction, because that is where DriftFactor01 for this frame is settled,
        // and before the velocity is written, so a boost that fires now is already in it.
        UpdateDriftBoost(dt);
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
        // Backing up ends the drift the dull way: a boost let go here would fire the car
        // backwards, so whatever was in the bank is simply dropped.
        CancelDriftBoost();
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

    /// <summary>
    /// The mini-turbo: a slide banks charge for as long as it is held, and the bank is
    /// spent the moment the drift ends. Nothing is awarded while the tyres are still
    /// sliding, so the payout always lands on a car that is straightening up and can
    /// use the speed - and a drift dragged out for the next tier is a drift spent
    /// pointing at whatever the street has coming.
    /// </summary>
    private void UpdateDriftBoost(float dt)
    {
        if (!driftBoostEnabled) return;

        bool handbrake = InputReader.Instance.HandbrakeHeld;
        bool charging = handbrake &&
                        SpeedFactor01 > boostMinSpeedFactor &&
                        DriftFactor01 >= boostDriftThreshold;

        if (charging)
        {
            driftCharge = Mathf.Min(driftCharge + dt, LongestChargeTime());
            chargeGraceTimer = boostChargeGrace;
        }
        else if (driftCharge > 0f)
        {
            // Letting go of the handbrake is a decision and pays out at once. The tyres
            // merely finding grip again is not, so that gets the grace period first -
            // otherwise a slide that flickers over a kerb would cash itself in early.
            if (!handbrake) ReleaseDriftBoost();
            else
            {
                chargeGraceTimer -= dt;
                if (chargeGraceTimer <= 0f) ReleaseDriftBoost();
            }
        }

        UpdateChargeReadout();
    }

    private void ReleaseDriftBoost()
    {
        int tier = ChargedTier();
        driftCharge = 0f;
        chargeGraceTimer = 0f;

        if (tier <= 0) return;

        var boost = boostTiers[tier - 1];
        boostMultiplier = Mathf.Max(1f, boost.speedMultiplier);
        boostDuration = boost.duration;
        boostTimer = boost.duration;

        // The kick is instant rather than something to accelerate into: a drift bleeds
        // speed all the way through, and a payout the car had to build up to would land
        // long after the corner it was earned on.
        currentSpeed = Mathf.Max(currentSpeed, maxSpeed * boostMultiplier);

        BoostFired?.Invoke(tier);
    }

    private void CancelDriftBoost()
    {
        driftCharge = 0f;
        chargeGraceTimer = 0f;
        boostTimer = 0f;
        boostDuration = 0f;
        boostMultiplier = 1f;
        UpdateChargeReadout();
    }

    /// <summary>Number of charge levels set up. Zero - an emptied list on an older scene
    /// object, say - simply means no boost, never a broken car.</summary>
    private int TierCount => boostTiers != null ? boostTiers.Length : 0;

    /// <summary>Highest tier the bank currently covers, 1-based, 0 for none.</summary>
    private int ChargedTier()
    {
        int tier = 0;
        for (int i = 0; i < TierCount; i++)
            if (driftCharge >= boostTiers[i].chargeTime) tier = i + 1;

        return tier;
    }

    private float LongestChargeTime()
    {
        float longest = 0f;
        for (int i = 0; i < TierCount; i++)
            longest = Mathf.Max(longest, boostTiers[i].chargeTime);

        return longest;
    }

    private void UpdateChargeReadout()
    {
        int tier = ChargedTier();
        DriftChargeTier = tier;

        if (TierCount == 0 || driftCharge <= 0f)
        {
            DriftCharge01 = 0f;
            return;
        }

        if (tier >= TierCount)
        {
            DriftCharge01 = 1f;
            return;
        }

        float from = tier > 0 ? boostTiers[tier - 1].chargeTime : 0f;
        DriftCharge01 = Mathf.InverseLerp(from, boostTiers[tier].chargeTime, driftCharge);
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
        // The ceiling is TopSpeed rather than maxSpeed, so a drift boost holds its
        // extra speed while it lasts and then leaks back down at the normal rate.
        float target = InputReader.Instance.AcceleratePressed ? TopSpeed : 0f;
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
