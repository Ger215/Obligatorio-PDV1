using System;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private HealthConfig config;
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private bool deactivateOnDeath;
    [SerializeField] private float destroyDelay;

    private bool isDead;
    private float incomingDamageMultiplier = 1f;

    public event Action<HealthSystem> Died;
    public event Action<int, int> Damaged;
    public event Action<int, int> Healed;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead => isDead;

    private void Awake()
    {
        ApplyConfig(config);
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDead || damageAmount <= 0)
        {
            return;
        }

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damageAmount * incomingDamageMultiplier));
        CurrentHealth = Mathf.Max(CurrentHealth - finalDamage, 0);
        Damaged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (isDead || amount <= 0 || CurrentHealth >= maxHealth)
        {
            return;
        }

        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
        Healed?.Invoke(CurrentHealth, maxHealth);
    }

    public void ResetHealth()
    {
        isDead = false;
        CurrentHealth = maxHealth;
        incomingDamageMultiplier = 1f;
    }

    public void SetIncomingDamageMultiplier(float multiplier)
    {
        incomingDamageMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void Configure(HealthConfig newConfig)
    {
        config = newConfig;
        ApplyConfig(config);
        ResetHealth();
    }

    public void Configure(int newMaxHealth, bool shouldDestroyOnDeath, bool shouldDeactivateOnDeath, float newDestroyDelay)
    {
        config = null;
        maxHealth = Mathf.Max(1, newMaxHealth);
        destroyOnDeath = shouldDestroyOnDeath;
        deactivateOnDeath = shouldDeactivateOnDeath;
        destroyDelay = Mathf.Max(0f, newDestroyDelay);
        ResetHealth();
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        Died?.Invoke(this);

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
            return;
        }

        if (deactivateOnDeath)
        {
            gameObject.SetActive(false);
        }
    }

    private void ApplyConfig(HealthConfig newConfig)
    {
        if (newConfig == null)
        {
            return;
        }

        maxHealth = Mathf.Max(1, newConfig.maxHealth);
        destroyOnDeath = newConfig.destroyOnDeath;
        deactivateOnDeath = newConfig.deactivateOnDeath;
        destroyDelay = Mathf.Max(0f, newConfig.destroyDelay);
    }

    private void OnValidate()
    {
        ApplyConfig(config);
        maxHealth = Mathf.Max(1, maxHealth);
        destroyDelay = Mathf.Max(0f, destroyDelay);
    }
}
