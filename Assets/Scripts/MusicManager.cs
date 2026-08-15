using System.Collections;
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

    private AudioSource rhythmSource;
    private AudioSource menuSource;
    private AudioSource gameplaySource;

    private Coroutine menuFadeRoutine;
    private Coroutine gameplayFadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize 2D stereo looping sources
        rhythmSource = CreateSource("RhythmLoop_AudioSource", rhythmLoopClip);
        menuSource = CreateSource("MenuCushion_AudioSource", menuCushionClip);
        gameplaySource = CreateSource("GameplayCushion_AudioSource", gameplayCushionClip);
    }

    private void Start()
    {
        bool isMainMenu = GameManager.Instance == null || GameManager.Instance.State == GameState.MainMenu;

        // 1. Rhythm always starts playing immediately and loops forever
        if (rhythmSource != null && rhythmLoopClip != null)
        {
            rhythmSource.volume = rhythmVolume * masterMusicVolume;
            rhythmSource.Play();
        }

        // 2. Start layers
        if (menuSource != null && menuCushionClip != null)
        {
            menuSource.volume = isMainMenu ? (menuCushionVolume * masterMusicVolume) : 0f;
            menuSource.Play();
        }

        if (gameplaySource != null && gameplayCushionClip != null)
        {
            gameplaySource.volume = isMainMenu ? 0f : (gameplayCushionVolume * masterMusicVolume);
            gameplaySource.Play();
        }
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

    private void HandleGameStarted()
    {
        // Smoothly crossfade from Menu cushion to Gameplay cushion while Rhythm stays unbroken
        FadeSource(menuSource, 0f, crossfadeDuration, ref menuFadeRoutine);
        FadeSource(gameplaySource, gameplayCushionVolume * masterMusicVolume, crossfadeDuration, ref gameplayFadeRoutine);
    }

    private void HandleGameOver()
    {
        // Duck or fade out gameplay cushion on game over
        FadeSource(gameplaySource, 0f, 1.2f, ref gameplayFadeRoutine);
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

    private AudioSource CreateSource(string sourceName, AudioClip clip)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform);
        child.transform.localPosition = Vector3.zero;

        AudioSource source = child.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // Pure 2D Stereo
        source.dopplerLevel = 0f;
        source.volume = 0f;
        return source;
    }
}
