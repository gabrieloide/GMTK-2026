using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("The Image component that has the VenetianBlindsTransition Material.")]
    [SerializeField] private Image transitionImage;
    [Tooltip("Total duration of the transition (in and out combined).")]
    [SerializeField] private float transitionDuration = 1.0f;
    [Tooltip("Should the game start with the transition opening up?")]
    [SerializeField] private bool playIntroOnStart = true;
    [Tooltip("How long to wait when the screen is fully covered before opening again.")]
    [SerializeField] private float midTransitionPause = 2.0f;
    [Tooltip("How long to stay frozen AFTER the transition fully opens.")]
    [SerializeField] private float postTransitionPause = 0.5f;
    
    // Shader property ID for better performance
    private int transitionPropertyID;
    private bool isTransitioning = false;

    private void Awake()
    {
        // Simple Singleton pattern
        if (Instance == null) 
        { 
            Instance = this; 
            // Uncomment the line below if you make it cross scenes, 
            // but since you use a single scene it's not strictly necessary.
            // DontDestroyOnLoad(gameObject); 
        }
        else 
        { 
            Destroy(gameObject); 
            return; 
        }

        transitionPropertyID = Shader.PropertyToID("_Transition");
        
        // Start fully covered if playIntroOnStart is true, otherwise transparent
        if (transitionImage != null)
        {
            float initialValue = playIntroOnStart ? 1f : 0f;
            transitionImage.material.SetFloat(transitionPropertyID, initialValue);
            transitionImage.raycastTarget = playIntroOnStart;
            transitionImage.enabled = playIntroOnStart;
        }
    }

    private void Start()
    {
        if (playIntroOnStart)
        {
            StartCoroutine(IntroRoutine());
        }
    }

    private IEnumerator IntroRoutine()
    {
        isTransitioning = true;
        if (transitionImage != null)
        {
            transitionImage.enabled = true;
            transitionImage.raycastTarget = true;
        }

        float halfDuration = transitionDuration / 2f;
        float time = 0f;

        // FADE OUT (1 to 0) - Open the blinds
        while (time < halfDuration)
        {
            time += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(time / halfDuration);
            transitionImage.material.SetFloat(transitionPropertyID, 1f - progress);
            yield return null;
        }

        transitionImage.material.SetFloat(transitionPropertyID, 0f);
        transitionImage.raycastTarget = false;
        transitionImage.enabled = false;
        isTransitioning = false;
    }

    /// <summary>
    /// Executes the transition effect.
    /// </summary>
    /// <param name="onScreenCovered">Logic to execute right in the middle (when screen is fully black/covered). Ideal for swapping panels.</param>
    public void PlayTransition(Action onScreenCovered, Action onTransitionFinished = null)
    {
        StopAllCoroutines();
        StartCoroutine(TransitionRoutine(onScreenCovered, onTransitionFinished));
    }

    private IEnumerator TransitionRoutine(Action onScreenCovered, Action onTransitionFinished)
    {
        isTransitioning = true;
        
        if (transitionImage == null)
        {
            Debug.LogError("Transition Image is not assigned!");
            isTransitioning = false;
            yield break;
        }

        // 1. Block UI clicks and enable rendering while transitioning
        transitionImage.enabled = true;
        transitionImage.raycastTarget = true;

        float halfDuration = transitionDuration / 2f;
        float time = 0f;

        // 2. FADE IN (0 to 1) - Close the blinds
        while (time < halfDuration)
        {
            time += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(time / halfDuration);
            transitionImage.material.SetFloat(transitionPropertyID, progress);
            yield return null;
        }

        transitionImage.material.SetFloat(transitionPropertyID, 1f);

        // 3. EXECUTE THE LOGIC (Change panels, restart game, etc.)
        onScreenCovered?.Invoke();

        // Pause while the screen is black
        if (midTransitionPause > 0f)
        {
            yield return new WaitForSecondsRealtime(midTransitionPause);
        }

        // 4. FADE OUT (1 to 0) - Open the blinds
        time = 0f;
        while (time < halfDuration)
        {
            time += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(time / halfDuration);
            transitionImage.material.SetFloat(transitionPropertyID, 1f - progress);
            yield return null;
        }

        transitionImage.material.SetFloat(transitionPropertyID, 0f);
        
        // 5. Restore UI clicks and disable Image to prevent overdraw
        transitionImage.raycastTarget = false;
        transitionImage.enabled = false;

        if (postTransitionPause > 0f)
        {
            yield return new WaitForSecondsRealtime(postTransitionPause);
        }

        isTransitioning = false;
        onTransitionFinished?.Invoke();
    }
}
