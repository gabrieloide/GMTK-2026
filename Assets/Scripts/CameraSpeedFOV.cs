using UnityEngine;
using Unity.Cinemachine;

// A wider lens at speed - the cheap "you're going fast" cue arcade racers lean on
// (pulling the horizon in and pushing the edges out) without touching the follow rig
// itself. Reads whatever FOV is already authored on the vcam at Start and only ever
// adds a kick on top of it, so retuning the base framing in the Inspector never fights
// this script.
[RequireComponent(typeof(CinemachineCamera))]
public class CameraSpeedFOV : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private float fovKick = 8f;
    [SerializeField] private float smoothTime = 0.25f;

    private CinemachineCamera vcam;
    private float baseFOV;
    private float fovVelocity;

    private void Awake()
    {
        vcam = GetComponent<CinemachineCamera>();
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        baseFOV = vcam.Lens.FieldOfView;
    }

    private void Update()
    {
        if (player == null) return;

        float targetFOV = baseFOV + fovKick * player.SpeedFactor01;
        var lens = vcam.Lens;
        lens.FieldOfView = Mathf.SmoothDamp(lens.FieldOfView, targetFOV, ref fovVelocity, smoothTime);
        vcam.Lens = lens;
    }
}
