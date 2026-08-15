using UnityEngine;

/// <summary>
/// Moves an object forward based on the player's speed to act as a look-ahead camera target.
/// Place this script on an empty GameObject that is a child of the Player.
/// Assign that empty GameObject to the CinemachineCamera's "Tracking Target" or "Follow".
/// </summary>
public class DynamicCameraTarget : MonoBehaviour
{
    [Tooltip("Reference to the player controller. If null, it will look in the parent.")]
    [SerializeField] private PlayerController player;
    
    [Tooltip("Maximum distance the camera target pushes forward at top speed.")]
    [SerializeField] private float maxLookAhead = 8f;
    
    [Tooltip("How fast the camera catches up to the look-ahead position (seconds).")]
    [SerializeField] private float smoothTime = 0.25f;
    
    private Vector3 initialLocalPosition;
    private Vector3 currentVelocity;

    private void Start()
    {
        initialLocalPosition = transform.localPosition;
        if (player == null) player = GetComponentInParent<PlayerController>();
    }

    private void LateUpdate()
    {
        if (player == null) return;

        // Calculate how far ahead we should look based on the car's current speed
        float zOffset = player.SpeedFactor01 * maxLookAhead;
        
        // The target local position pushes forward on the Z axis
        Vector3 targetLocalPos = initialLocalPosition + new Vector3(0f, 0f, zOffset);
        
        // Smoothly interpolate the local position so the camera doesn't jitter
        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, targetLocalPos, ref currentVelocity, smoothTime);
    }
}
