using System.Collections;
using UnityEngine;

public class DeathFaceAnimator : MonoBehaviour
{
    [Header("Face Settings")]
    public GameObject face;
    
    [Range(0.1f, 2f)]
    public float blinkSpeed = 0.2f;
    
    private bool isBlinking;
    private MeshRenderer renderer;
    
    void OnEnable()
    {
        // Initialize renderer
        if (face != null)
        {
            renderer = face.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogError("Face GameObject doesn't have a MeshRenderer component!");
                return;
            }
            if (renderer.material == null)
            {
                Debug.LogError("MeshRenderer has no material assigned!");
                return;
            }
        }
        else
        {
            Debug.LogError("Face GameObject is not assigned!");
            return;
        }

        // Start blinking if we're initialized
        if (renderer != null && renderer.material != null)
        {
            StartCoroutine(PerformBlink());
        }
    }

    private IEnumerator PerformBlink()
    {
        isBlinking = true;
        
        yield return StartCoroutine(AnimateEyelids(6f, 8f, blinkSpeed * 0.5f));
        yield return StartCoroutine(AnimateEyelids(8f, 6f, blinkSpeed * 0.5f));
        
        isBlinking = false;

        StartCoroutine(PerformBlink());
    }
    
    private IEnumerator AnimateEyelids(float startIndex, float endIndex, float duration)
    {
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);
            
            float currentIndex = Mathf.Lerp(startIndex, endIndex, t);
            
            // Add null check here as well
            if (renderer != null && renderer.material != null)
            {
                renderer.material.SetFloat("_Index", currentIndex);
            }
            
            yield return null;
        }
        
        // Final value
        if (renderer != null && renderer.material != null)
        {
            renderer.material.SetFloat("_Index", endIndex);
        }
    }
}