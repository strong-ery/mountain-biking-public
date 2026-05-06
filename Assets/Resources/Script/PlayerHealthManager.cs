using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerHealthManager : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float regenDelay = 5f; // in seconds
    public float regenRate = 2.5f; // units/second
    
    [Header("Post Processing")]
    public Volume postProcessVolume;
    
    // Events
    public System.Action<float> OnHealthChanged;
    public System.Action OnPlayerDeath;
    
    public float currentHealth;
    private float lastDamageTime;
    private bool isRegenerating = false;
    private Vignette vignette;
    
    public float CurrentHealth => currentHealth;
    public float HealthPercentage => currentHealth / maxHealth;
    public bool IsAlive => currentHealth > 0f;

    public BasicSwap basicSwap;
    
    void Start()
    {
        // Initialize health
        currentHealth = maxHealth;
        
        // Get vignette component from post-process volume
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            if (!postProcessVolume.profile.TryGet(out vignette))
            {
                Debug.LogWarning("No Vignette override found in the post-process volume profile!");
            }
            else
            {
                vignette.active = true;
                vignette.intensity.overrideState = true;
            }
        }
        
        // Initialize vignette effect
        UpdateVignetteEffect();
    }
    
    void Update()
    {
        HandleHealthRegeneration();
    }
    
    /// <summary>
    /// Applies damage to the player
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (!IsAlive || damage <= 0f) return;
        
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        lastDamageTime = Time.time;
        isRegenerating = false;
        
        UpdateVignetteEffect();
        OnHealthChanged?.Invoke(currentHealth);
        
        if (currentHealth <= 0f)
        {
            HandlePlayerDeath();
        }
    }
    
    /// <summary>
    /// Heals the player by a specified amount
    /// </summary>
    public void Heal(float healAmount)
    {
        if (!IsAlive || healAmount <= 0f) return;
        
        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);
        
        UpdateVignetteEffect();
        OnHealthChanged?.Invoke(currentHealth);
    }
    
    /// <summary>
    /// Sets the player's health to a specific value
    /// </summary>
    public void SetHealth(float newHealth)
    {
        float oldHealth = currentHealth;
        currentHealth = Mathf.Clamp(newHealth, 0f, maxHealth);
        
        UpdateVignetteEffect();
        OnHealthChanged?.Invoke(currentHealth);
        
        if (oldHealth > 0f && currentHealth <= 0f)
        {
            HandlePlayerDeath();
        }
    }
    
    /// <summary>
    /// Instantly kills the player
    /// </summary>
    public void Kill()
    {
        TakeDamage(currentHealth);
    }
    
    /// <summary>
    /// Fully restores player health
    /// </summary>
    public void FullHeal()
    {
        SetHealth(maxHealth);
    }
    
    /// <summary>
    /// Checks if the player can regenerate health
    /// </summary>
    public bool CanRegenerate()
    {
        return IsAlive && 
               currentHealth < maxHealth && 
               Time.time >= lastDamageTime + regenDelay;
    }
    
    private void HandleHealthRegeneration()
    {
        if (!CanRegenerate()) return;
        
        if (!isRegenerating)
        {
            isRegenerating = true;
        }
        
        float regenAmount = regenRate * Time.deltaTime;
        currentHealth = Mathf.Min(maxHealth, currentHealth + regenAmount);
        
        UpdateVignetteEffect();
        OnHealthChanged?.Invoke(currentHealth);
        
        // Stop regenerating when at full health
        if (currentHealth >= maxHealth)
        {
            isRegenerating = false;
        }
    }
    
    private void UpdateVignetteEffect()
    {
        if (vignette == null) return;
        
        // Calculate vignette intensity: 1 when health is 0, 0 when health is full
        float vignetteIntensity = Mathf.Lerp(1f, 0f, HealthPercentage);
        
        // Non-linear smoothing for dramatic low-health effect
        vignetteIntensity = Mathf.Pow(vignetteIntensity, 2f);
        
        vignette.intensity.overrideState = true;
        vignette.intensity.value = vignetteIntensity;
    }
    
    private void HandlePlayerDeath()
    {
        isRegenerating = false;
        OnPlayerDeath?.Invoke();

        // Additional death logic can be added here
        
        basicSwap.dead = true;
        Debug.Log("Player has died!");
    }
    
    // Optional: Reset health manager (useful for respawning)
    public void ResetHealthManager()
    {
        currentHealth = maxHealth;
        isRegenerating = false;
        lastDamageTime = 0f;
        UpdateVignetteEffect();
        OnHealthChanged?.Invoke(currentHealth);
    }
    
    // Debug method for testing in editor
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugTakeDamage(float damage)
    {
        TakeDamage(damage);
    }
}
