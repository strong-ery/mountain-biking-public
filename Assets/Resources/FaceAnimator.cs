using System.Collections;
using UnityEngine;

public class FaceAnimator : MonoBehaviour
{
    [Header("Face Settings")]
    public GameObject face;
    
    [Header("Blink Settings")]
    [Range(0.5f, 10f)]
    public float minBlinkRate = 2f; // Minimum time between blinks in seconds
    
    [Range(0.5f, 10f)]
    public float maxBlinkRate = 5f; // Maximum time between blinks in seconds
    
    [Range(0.1f, 2f)]
    public float blinkSpeed = 0.2f; // Duration of each blink animation
    
    private bool isBlinking;
    private MeshRenderer renderer;
    
    void OnEnable()
    {
        renderer = face.GetComponent<MeshRenderer>();
        StartCoroutine(BlinkLoop());
    }
    
    private IEnumerator BlinkLoop()
    {
        while (true)
        {
            // Wait for a random time between min and max blink rate
            float randomBlinkDelay = Random.Range(minBlinkRate, maxBlinkRate);
            yield return new WaitForSeconds(randomBlinkDelay);
            
            // Start the blink animation if not already blinking
            if (!isBlinking)
            {
                yield return StartCoroutine(PerformBlink());
            }
        }
    }
    
    private IEnumerator PerformBlink()
    {
        isBlinking = true;
        
        // Close eyes (0 -> 2)
        yield return StartCoroutine(AnimateEyelids(0f, 2f, blinkSpeed * 0.5f));
        
        // Open eyes (2 -> 0)
        yield return StartCoroutine(AnimateEyelids(2f, 0f, blinkSpeed * 0.5f));
        
        isBlinking = false;
    }
    
    private IEnumerator AnimateEyelids(float startIndex, float endIndex, float duration)
    {
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Use smooth step for more natural eyelid movement
            t = Mathf.SmoothStep(0f, 1f, t);
            
            float currentIndex = Mathf.Lerp(startIndex, endIndex, t);
            renderer.material.SetFloat("_Index", currentIndex);
            
            yield return null;
        }
        
        // Ensure we end exactly at the target value
        renderer.material.SetFloat("_Index", endIndex);
    }
}