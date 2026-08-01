using Game;
using UnityEngine;

/// <summary>
/// Feeds the car's smoke from the driving state: a tailpipe plume that thickens with
/// the throttle, and tyre smoke that only shows up once the back end is actually
/// sliding. Rates are faded rather than switched, the same way <see cref="PlayerAudio"/>
/// handles its loops - a particle system snapping from nothing to full reads as a glitch.
/// The systems themselves are built by CarSmokeBuilder, so all the look tuning lives
/// on them and this only decides how much comes out.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CarSmoke : MonoBehaviour
{
    [Header("Systems")]
    [SerializeField] private ParticleSystem exhaust;
    [SerializeField] private ParticleSystem tyreSmokeLeft;
    [SerializeField] private ParticleSystem tyreSmokeRight;

    [Header("Exhaust")]
    [Tooltip("Particles per second with the throttle released - the car idling.")]
    [SerializeField] private float exhaustIdleRate = 7f;
    [SerializeField] private float exhaustMaxRate = 34f;
    [SerializeField] private float exhaustFadeSpeed = 40f;

    [Tooltip("Extra particles coughed out the instant the throttle is tapped.")]
    [SerializeField, Range(0, 20)] private int throttleBlip = 5;

    [Tooltip("How far the exhaust plume is held back while the tyres are smoking. " +
             "1 shuts it off completely at full slide, 0 lets both run together.")]
    [SerializeField, Range(0f, 1f)] private float exhaustDuckWhileSliding = 1f;

    [Header("Tyre smoke")]
    [Tooltip("Loss of grip (PlayerController.DriftFactor01) where the tyres start to smoke. " +
             "Matches PlayerAudio's skid threshold so the screech and the smoke arrive together.")]
    [SerializeField, Range(0f, 1f)] private float driftThreshold = 0.35f;

    [Tooltip("Drift amount where the tyres are burning flat out. A full handbrake turn rarely " +
             "reaches 1, so this sits well below it or the smoke never ramps up.")]
    [SerializeField, Range(0f, 1f)] private float driftFull = 0.7f;

    [SerializeField] private float tyreMaxRate = 90f;
    [SerializeField] private float tyreFadeSpeed = 160f;

    [Tooltip("Below this fraction of max speed the tyres cannot smoke, so a car nudged " +
             "sideways at a standstill stays clean.")]
    [SerializeField, Range(0f, 1f)] private float minSpeedFactor = 0.1f;

    /// <summary>How fast the ducking follows the slide, in units of slide per second.</summary>
    private const float SlideFadeSpeed = 5f;

    private PlayerController playerController;
    private float exhaustRate;
    private float tyreRate;
    private float slide01;
    private bool throttleWasPressed;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        // Tyres first: they work out how hard the car is sliding, and the exhaust needs
        // that this frame to know how far to duck out of their way.
        float dt = Time.deltaTime;
        UpdateTyreSmoke(dt);
        UpdateExhaust(dt);
    }

    private void UpdateExhaust(float dt)
    {
        bool throttle = InputReader.Instance.AcceleratePressed;

        // The plume answers the throttle rather than the speed: a car pinned against a
        // wall at full revs should still be belching, and one coasting should not.
        float target = throttle ? exhaustMaxRate : exhaustIdleRate;

        // Mid-slide the tyres own the back of the car. Both plumes at once just smeared
        // into each other and neither read as anything, so the exhaust gives way.
        target *= 1f - slide01 * exhaustDuckWhileSliding;

        exhaustRate = Mathf.MoveTowards(exhaustRate, target, exhaustFadeSpeed * dt);

        SetRate(exhaust, exhaustRate);

        // The blip would punch straight through the duck, so it keeps quiet too.
        bool blipAllowed = slide01 * exhaustDuckWhileSliding < 0.5f;
        if (throttle && !throttleWasPressed && blipAllowed && throttleBlip > 0 && exhaust != null)
            exhaust.Emit(throttleBlip);

        throttleWasPressed = throttle;
    }

    private void UpdateTyreSmoke(float dt)
    {
        float speedFactor = playerController.SpeedFactor01;

        // Reverse is capped low and never drifts, so smoking there would just be noise.
        bool canSlide = !playerController.IsReversing && speedFactor > minSpeedFactor;

        // Past the threshold the burn scales with how far past it the car is, so a lazy
        // slide only wisps while a full handbrake turn lays down a proper cloud.
        float intensity = canSlide
            ? Mathf.InverseLerp(driftThreshold, Mathf.Max(driftFull, driftThreshold + 0.01f),
                                playerController.DriftFactor01)
            : 0f;

        slide01 = Mathf.MoveTowards(slide01, intensity, SlideFadeSpeed * dt);

        float target = intensity * tyreMaxRate * speedFactor;
        tyreRate = Mathf.MoveTowards(tyreRate, target, tyreFadeSpeed * dt);

        SetRate(tyreSmokeLeft, tyreRate);
        SetRate(tyreSmokeRight, tyreRate);
    }

    private static void SetRate(ParticleSystem system, float rate)
    {
        if (system == null) return;

        var emission = system.emission;
        emission.rateOverTime = rate;
    }
}
