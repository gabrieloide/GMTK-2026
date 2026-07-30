using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerAudio : MonoBehaviour
{
    [Header("Clips (drag the exported WAVs here)")]
    [SerializeField] private AudioClip engineClip;
    [SerializeField] private AudioClip skidClip;
    [SerializeField] private AudioClip cornerClip;

    [Header("Engine")]
    [SerializeField] private float engineMinPitch = 0.85f;
    [SerializeField] private float engineMaxPitch = 1.25f;
    [SerializeField] private float engineMinVolume = 0.1f;
    [SerializeField] private float engineMaxVolume = 0.3f;

    [Header("Skid (handbrake / real drift)")]
    [Tooltip("Loss-of-grip amount (PlayerController.DriftFactor01) needed to trigger the skid screech.")]
    [SerializeField, Range(0f, 1f)] private float driftThreshold = 0.35f;
    [SerializeField] private float skidFadeSpeed = 4f;
    [SerializeField] private float skidMaxVolume = 0.5f;

    [Header("Cornering (sharp turn, tires still gripping)")]
    [Tooltip("Turn rate (PlayerController.TurnRate01) needed to trigger the subtle cornering tire sound.")]
    [SerializeField, Range(0f, 1f)] private float turnThreshold = 0.55f;
    [SerializeField] private float cornerFadeSpeed = 5f;
    [SerializeField] private float cornerMaxVolume = 0.22f;

    private PlayerController playerController;
    private AudioSource engineSource;
    private AudioSource skidSource;
    private AudioSource cornerSource;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        engineSource = CreateLoopingSource("EngineAudio", engineClip);
        skidSource = CreateLoopingSource("SkidAudio", skidClip);
        cornerSource = CreateLoopingSource("CornerAudio", cornerClip);
        skidSource.volume = 0f;
        cornerSource.volume = 0f;
    }

    private void Start()
    {
        if (engineClip != null) engineSource.Play();
    }

    private void Update()
    {
        float speedFactor = playerController.SpeedFactor01;
        engineSource.pitch = Mathf.Lerp(engineMinPitch, engineMaxPitch, speedFactor);
        engineSource.volume = Mathf.Lerp(engineMinVolume, engineMaxVolume, speedFactor);

        // Real loss of grip (handbrake drift) takes priority - a corner squeal
        // underneath a full drift screech would just be mud.
        bool isDrifting = speedFactor > 0.1f && playerController.DriftFactor01 >= driftThreshold;
        bool isCornering = !isDrifting && speedFactor > 0.1f && playerController.TurnRate01 >= turnThreshold;

        FadeLoop(skidSource, isDrifting ? skidMaxVolume : 0f, skidFadeSpeed);
        FadeLoop(cornerSource, isCornering ? cornerMaxVolume : 0f, cornerFadeSpeed);
    }

    private static void FadeLoop(AudioSource source, float targetVolume, float fadeSpeed)
    {
        source.volume = Mathf.MoveTowards(source.volume, targetVolume, fadeSpeed * Time.deltaTime);

        if (source.volume > 0f && !source.isPlaying) source.Play();
        if (source.volume <= 0f && source.isPlaying) source.Stop();
    }

    private AudioSource CreateLoopingSource(string childName, AudioClip clip)
    {
        var child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }
}
