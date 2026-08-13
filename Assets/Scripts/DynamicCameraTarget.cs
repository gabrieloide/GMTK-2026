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
    
    [Tooltip("How fast the camera catches up to the look-ahead position.")]
    [SerializeField] private float smoothSpeed = 2.5f;
    
    private Vector3 initialLocalPosition;

    private void Start()
    {
        initialLocalPosition = transform.localPosition;
        if (player == null) player = GetComponentInParent<PlayerController>();
    }

    private void Update()
    {
        if (player == null) return;

        // Calculate how far ahead we should look based on the car's current speed
        float zOffset = player.SpeedFactor01 * maxLookAhead;
        
        // The target local position pushes forward on the Z axis
        Vector3 targetLocalPos = initialLocalPosition + new Vector3(0, 0, zOffset);
        
        // Smoothly interpolate the local position so the camera doesn't snap instantly
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPos, Time.deltaTime * smoothSpeed);
    }
}
