using UnityEngine;

/// <summary>
/// Feeds the car's smoke from cornering alone: both the tailpipe and the tyres only
/// puff while the car is actively turning (PlayerController.TurnRate01 past a threshold),
/// and stay clean the rest of the time - straight-line driving, coasting, and parked all
/// read as no smoke. Rates are faded rather than switched, the same way
/// <see cref="PlayerAudio"/> handles its loops - a particle system snapping from nothing
/// to full reads as a glitch. The systems themselves are built by CarSmokeBuilder, so all
/// the look tuning lives on them and this only decides how much comes out.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CarSmoke : MonoBehaviour
{
    [Header("Systems")]
    [SerializeField] private ParticleSystem exhaust;
    [SerializeField] private ParticleSystem tyreSmokeLeft;
    [SerializeField] private ParticleSystem tyreSmokeRight;

    [Header("Turning")]
    [Tooltip("Turn rate (PlayerController.TurnRate01) where smoke starts.")]
    [SerializeField, Range(0f, 1f)] private float turnThreshold = 0.3f;

    [Tooltip("Turn rate where smoke is at full intensity.")]
    [SerializeField, Range(0f, 1f)] private float turnFull = 0.75f;

    [Tooltip("Below this fraction of max speed the car cannot smoke, so turning the wheel " +
             "at a standstill stays clean.")]
    [SerializeField, Range(0f, 1f)] private float minSpeedFactor = 0.1f;

    [SerializeField] private float exhaustMaxRate = 34f;
    [SerializeField] private float tyreMaxRate = 90f;
    [SerializeField] private float fadeSpeed = 120f;

    private PlayerController playerController;
    private float exhaustRate;
    private float tyreRate;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // Reverse never steers (PlayerController drives straight back), so it can't turn
        // and stays clean on its own - the explicit check just makes that intent visible.
        bool canSmoke = !playerController.IsReversing && playerController.SpeedFactor01 > minSpeedFactor;
        float intensity = canSmoke
            ? Mathf.InverseLerp(turnThreshold, Mathf.Max(turnFull, turnThreshold + 0.01f),
                                playerController.TurnRate01)
            : 0f;

        exhaustRate = Mathf.MoveTowards(exhaustRate, intensity * exhaustMaxRate, fadeSpeed * dt);
        tyreRate = Mathf.MoveTowards(tyreRate, intensity * tyreMaxRate, fadeSpeed * dt);

        SetRate(exhaust, exhaustRate);
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
