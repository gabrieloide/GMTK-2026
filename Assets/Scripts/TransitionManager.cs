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
        transitionImage.raycastTarget = true;

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
        isTransitioning = false;
    }

    /// <summary>
    /// Executes the transition effect.
    /// </summary>
    /// <param name="onScreenCovered">Logic to execute right in the middle (when screen is fully black/covered). Ideal for swapping panels.</param>
    public void PlayTransition(Action onScreenCovered)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionRoutine(onScreenCovered));
    }

    private IEnumerator TransitionRoutine(Action onScreenCovered)
    {
        isTransitioning = true;
        
        if (transitionImage == null)
        {
            Debug.LogError("Transition Image is not assigned!");
            isTransitioning = false;
            yield break;
        }

        // 1. Block UI clicks while transitioning
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

        // Optional small pause if you want the screen to remain black for a fraction of a second
        // yield return new WaitForSeconds(0.1f);

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
        
        // 5. Restore UI clicks
        transitionImage.raycastTarget = false;
        isTransitioning = false;
    }
}
