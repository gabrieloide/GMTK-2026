using System.Collections;
using Game;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Audio Clips")]
    [Tooltip("The core rhythm loop that MUST always be heard and never stops playing.")]
    [SerializeField] private AudioClip rhythmLoopClip;
    [Tooltip("The musical cushion/layer for the Main Menu.")]
    [SerializeField] private AudioClip menuCushionClip;
    [Tooltip("The musical cushion/layer for Gameplay.")]
    [SerializeField] private AudioClip gameplayCushionClip;

    [Header("Volume Settings")]
    [Range(0f, 1f)] [SerializeField] private float masterMusicVolume = 0.85f;
    [Range(0f, 1f)] [SerializeField] private float rhythmVolume = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float menuCushionVolume = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float gameplayCushionVolume = 0.65f;

    [Header("Transitions")]
    [Tooltip("Crossfade duration between menu and gameplay cushions.")]
    [SerializeField] private float crossfadeDuration = 0.8f;

    [Header("Radio Stations")]
    [Tooltip("Full songs cycled through with Next/Previous (keys 1/2, Z/X, or gamepad D-pad). Station 0 is always the layered mix above.")]
    [SerializeField] private AudioClip[] stationClips;
    [Tooltip("Short static/tuning stingers played once when switching stations, picked at random.")]
    [SerializeField] private AudioClip[] stationSwitchStingers;
    [Range(0f, 1f)] [SerializeField] private float stationVolume = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float stingerVolume = 0.7f;
    [Tooltip("Crossfade duration when switching radio stations.")]
    [SerializeField] private float stationCrossfadeDuration = 0.5f;

    private AudioSource rhythmSource;
    private AudioSource menuSource;
    private AudioSource gameplaySource;
    private AudioSource stationSource;
    private AudioSource stingerSource;

    private Coroutine menuFadeRoutine;
    private Coroutine gameplayFadeRoutine;
    private Coroutine rhythmFadeRoutine;
    private Coroutine stationFadeRoutine;

    // 0 = the layered mix above; 1..stationClips.Length = an index into stationClips.
    private int currentStation;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize 2D stereo looping sources
        rhythmSource = CreateSource("RhythmLoop_AudioSource", rhythmLoopClip, loop: true);
        menuSource = CreateSource("MenuCushion_AudioSource", menuCushionClip, loop: true);
        gameplaySource = CreateSource("GameplayCushion_AudioSource", gameplayCushionClip, loop: true);
        stationSource = CreateSource("RadioStation_AudioSource", null, loop: true);
        stingerSource = CreateSource("RadioStinger_AudioSource", null, loop: false);
        // PlayOneShot scales its volumeScale argument by the source's own volume,
        // so this must stay at 1 (the desired loudness is passed per-call instead).
        stingerSource.volume = 1f;
    }

    private void Start()
    {
        // Preload audio data to prevent any buffer latency differences
        if (rhythmLoopClip != null) rhythmLoopClip.LoadAudioData();
        if (menuCushionClip != null) menuCushionClip.LoadAudioData();
        if (gameplayCushionClip != null) gameplayCushionClip.LoadAudioData();

        bool isMainMenu = GameManager.Instance == null || GameManager.Instance.State == GameState.MainMenu;

        // Set initial volumes
        if (rhythmSource != null) rhythmSource.volume = rhythmVolume * masterMusicVolume;
        if (menuSource != null) menuSource.volume = isMainMenu ? (menuCushionVolume * masterMusicVolume) : 0f;
        if (gameplaySource != null) gameplaySource.volume = isMainMenu ? 0f : (gameplayCushionVolume * masterMusicVolume);

        // Schedule all 3 sources to start playing at the EXACT SAME DSP timestamp
        double dspStartTime = AudioSettings.dspTime + 0.1;
        if (rhythmSource != null) rhythmSource.PlayScheduled(dspStartTime);
        if (menuSource != null) menuSource.PlayScheduled(dspStartTime);
        if (gameplaySource != null) gameplaySource.PlayScheduled(dspStartTime);
    }

    private void OnEnable()
    {
        GameManager.OnGameStarted += HandleGameStarted;
        GameManager.OnGameOver += HandleGameOver;
    }

    private void OnDisable()
    {
        GameManager.OnGameStarted -= HandleGameStarted;
        GameManager.OnGameOver -= HandleGameOver;
    }

    private void Update()
    {
        if (InputReader.Instance == null || stationClips == null || stationClips.Length == 0) return;

        if (InputReader.Instance.NextTrackPressed) ChangeStation(1);
        else if (InputReader.Instance.PreviousTrackPressed) ChangeStation(-1);
    }

    private void HandleGameStarted()
    {
        // Tuned to a radio station: the layered mix is silent, leave it alone.
        if (currentStation != 0) return;

        // When gameplay starts: fade out Menu cushion to 0, fade in Gameplay cushion to target volume.
        // The rhythm loop keeps running undisturbed in 100% synchronization.
        FadeSource(menuSource, 0f, crossfadeDuration, ref menuFadeRoutine);
        FadeSource(gameplaySource, gameplayCushionVolume * masterMusicVolume, crossfadeDuration, ref gameplayFadeRoutine);
    }

    private void HandleGameOver()
    {
        if (currentStation != 0) return;

        // Smoothly fade out gameplay cushion on game over
        FadeSource(gameplaySource, 0f, 1.2f, ref gameplayFadeRoutine);
    }

    // Radio: cycles Next(+1)/Previous(-1) between the layered mix (station 0) and each
    // song in stationClips. Switching away mutes the layered mix entirely - like a real
    // radio, the previous station stops being heard the moment you change the channel.
    private void ChangeStation(int direction)
    {
        int totalStations = stationClips.Length + 1;
        currentStation = ((currentStation + direction) % totalStations + totalStations) % totalStations;

        PlaySwitchStinger();

        if (currentStation == 0)
        {
            FadeSource(stationSource, 0f, stationCrossfadeDuration, ref stationFadeRoutine);
            RestoreLayeredMix();
        }
        else
        {
            stationSource.clip = stationClips[currentStation - 1];
            stationSource.Play();
            FadeSource(stationSource, stationVolume * masterMusicVolume, stationCrossfadeDuration, ref stationFadeRoutine);

            FadeSource(rhythmSource, 0f, stationCrossfadeDuration, ref rhythmFadeRoutine);
            FadeSource(menuSource, 0f, stationCrossfadeDuration, ref menuFadeRoutine);
            FadeSource(gameplaySource, 0f, stationCrossfadeDuration, ref gameplayFadeRoutine);
        }
    }

    private void RestoreLayeredMix()
    {
        bool isMainMenu = GameManager.Instance == null || GameManager.Instance.State == GameState.MainMenu;
        bool isGameOver = GameManager.Instance != null && GameManager.Instance.isGameOver;
        FadeSource(rhythmSource, rhythmVolume * masterMusicVolume, stationCrossfadeDuration, ref rhythmFadeRoutine);
        FadeSource(menuSource, isMainMenu ? menuCushionVolume * masterMusicVolume : 0f, stationCrossfadeDuration, ref menuFadeRoutine);
        FadeSource(gameplaySource, (isMainMenu || isGameOver) ? 0f : gameplayCushionVolume * masterMusicVolume, stationCrossfadeDuration, ref gameplayFadeRoutine);
    }

    private void PlaySwitchStinger()
    {
        if (stationSwitchStingers == null || stationSwitchStingers.Length == 0) return;
        AudioClip stinger = stationSwitchStingers[Random.Range(0, stationSwitchStingers.Length)];
        stingerSource.PlayOneShot(stinger, stingerVolume * masterMusicVolume);
    }

    private void FadeSource(AudioSource source, float targetVolume, float duration, ref Coroutine routine)
    {
        if (source == null) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FadeRoutine(source, targetVolume, duration));
    }

    private IEnumerator FadeRoutine(AudioSource source, float targetVolume, float duration)
    {
        if (duration <= 0f)
        {
            source.volume = targetVolume;
            yield break;
        }

        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            source.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        source.volume = targetVolume;
    }

    private AudioSource CreateSource(string sourceName, AudioClip clip, bool loop)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform);
        child.transform.localPosition = Vector3.zero;

        AudioSource source = child.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D Stereo
        source.dopplerLevel = 0f;
        source.volume = 0f;
        return source;
    }
}
