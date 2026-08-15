using UnityEngine;
using Code.Scripts.Audio;
using Unity.Cinemachine;

public class PlayerCollision : MonoBehaviour
{
    Rigidbody rb;
    PlayerController playerController;
    [SerializeField] private float knockbackForce = 10;
    [SerializeField] private float knockbackDuration = 0.25f;

    [Header("Hit Feedback")]
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [Tooltip("Relative impact speed mapped to impulse force, so a wall or another car " +
             "shakes proportionally to how hard it landed instead of a fixed amount.")]
    [SerializeField] private float hitImpulseScale = 0.10f;
    [SerializeField] private float hitImpulseMin = 0.5f;
    [SerializeField] private float hitImpulseMax = 1.8f;

    [Header("Sidewalk")]
    [Tooltip("Tag used by sidewalk colliders. Hitting one of these skips wall knockback and crash feedback.")]
    [SerializeField] private string sidewalkTag = "Sidewalk";

    [Header("Visual Feedback")]
    [Tooltip("Visual-only mesh child (not the physics root) that gets deformed on crash or hopped on pickup.")]
    [SerializeField] private Transform carVisual;

    [Header("Pickup Feedback")]
    [SerializeField] private float pickupImpulseForce = 0.4f;
    [SerializeField] private float pickupHopHeight = 0.4f;
    [SerializeField] private float pickupHopDuration = 0.18f;

    [Header("Landing Squash")]
    [SerializeField] private float squashAmount = 0.2f;
    [SerializeField] private float squashDuration = 0.12f;

    private Vector3 carVisualRestLocalPosition;
    private Vector3 carVisualRestLocalScale = Vector3.one;
    private Coroutine hopRoutine;
    private Coroutine crashDeformRoutine;

    public bool IsDeforming => hopRoutine != null || crashDeformRoutine != null;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerController = GetComponent<PlayerController>();
        if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
        if (carVisual != null)
        {
            carVisualRestLocalPosition = carVisual.localPosition;
            carVisualRestLocalScale = carVisual.localScale;
        }
    }

    private void OnEnable()
    {
        OrderHolder.OnPickup += OnOrderPickup;
    }

    private void OnDisable()
    {
        OrderHolder.OnPickup -= OnOrderPickup;
    }

    private bool IsSidewalk(GameObject go)
    {
        if (go == null) return false;
        return go.CompareTag(sidewalkTag) || go.name.ToLower().Contains("sidewalk");
    }

    void OnCollisionEnter(Collision collision)
    {
        // Ignorar aceras para que no cuenten como choque con pared ni apliquen shake/movimiento
        if (IsSidewalk(collision.gameObject))
        {
            return;
        }

        rb.angularVelocity = Vector3.zero;

        var opositeDirection = collision.contacts[0].normal;

        opositeDirection.y = 0;
        if (opositeDirection.sqrMagnitude < 0.0001f) return;
        opositeDirection.Normalize();

        playerController.ApplyKnockback(opositeDirection, knockbackForce, knockbackDuration);
        AudioManager.Instance.PlaySFX("car_hit");

        float force = Mathf.Clamp(collision.relativeVelocity.magnitude * hitImpulseScale, hitImpulseMin, hitImpulseMax);
        Debug.Log($"<color=orange>[Crash Feedback]</color> Choque contra: '{collision.gameObject.name}', Fuerza Shake: {force:F2}");
        GenerateImpulse(force);

        PlayCrashDeform(0.4f, 0.15f);
    }

    // Lighter twin of the curb bump - enough weight to feel like the parcel just landed
    // in the trunk without reading as an impact.
    private void OnOrderPickup(Vector3 pickupPosition)
    {
        GenerateImpulse(pickupImpulseForce);
        PlayHop(pickupHopHeight, pickupHopDuration);
    }

    private void GenerateImpulse(float force)
    {
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(force);
        }
        else
        {
            Debug.LogWarning("[Impulse Warning] impulseSource es NULL en PlayerCollision!");
        }
    }

    private void PlayHop(float height, float duration)
    {
        if (carVisual == null) return;
        if (hopRoutine != null) StopCoroutine(hopRoutine);
        hopRoutine = StartCoroutine(HopCoroutine(height, duration));
    }

    private System.Collections.IEnumerator HopCoroutine(float height, float duration)
    {
        // Defensive reset: a re-trigger mid-squash (StopCoroutine below) would otherwise
        // leave the mesh stretched from the interrupted landing for the whole new hop.
        carVisual.localScale = carVisualRestLocalScale;

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float offset = Mathf.Sin(t * Mathf.PI) * height;
            carVisual.localPosition = carVisualRestLocalPosition + Vector3.up * offset;
            yield return null;
        }

        carVisual.localPosition = carVisualRestLocalPosition;

        // Landing squash: a beat of the suspension taking the hit before settling flat,
        // instead of the hop just reversing itself and stopping dead.
        float squashTimer = 0f;
        while (squashTimer < squashDuration)
        {
            squashTimer += Time.deltaTime;
            float st = Mathf.Clamp01(squashTimer / squashDuration);
            float squash = Mathf.Sin(st * Mathf.PI) * squashAmount;
            carVisual.localScale = new Vector3(
                carVisualRestLocalScale.x * (1f + squash),
                carVisualRestLocalScale.y * (1f - squash),
                carVisualRestLocalScale.z * (1f + squash));
            yield return null;
        }

        carVisual.localScale = carVisualRestLocalScale;
        hopRoutine = null;
    }

    private void PlayCrashDeform(float amount, float duration)
    {
        if (carVisual == null) return;
        if (crashDeformRoutine != null) StopCoroutine(crashDeformRoutine);
        // Also stop hop routine to prevent fighting over localScale
        if (hopRoutine != null) StopCoroutine(hopRoutine);
        
        crashDeformRoutine = StartCoroutine(CrashDeformCoroutine(amount, duration));
    }

    private System.Collections.IEnumerator CrashDeformCoroutine(float amount, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            // Sin curve: 0 -> 1 -> 0
            float squash = Mathf.Sin(t * Mathf.PI) * amount;
            
            carVisual.localScale = new Vector3(
                carVisualRestLocalScale.x * (1f + squash * 0.5f), // bulge sideways
                carVisualRestLocalScale.y * (1f + squash * 0.5f), // bulge upwards
                carVisualRestLocalScale.z * (1f - squash)         // squash forwards (Z)
            );
            yield return null;
        }

        carVisual.localScale = carVisualRestLocalScale;
        crashDeformRoutine = null;
    }
}