using System;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private bool deactivateOnDeath;
    [SerializeField] private float destroyDelay;

    private bool isDead;

    public event Action<HealthSystem> Died;
    public event Action<int, int> Damaged;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead => isDead;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDead || damageAmount <= 0)
        {
            return;
        }

        CurrentHealth = Mathf.Max(CurrentHealth - damageAmount, 0);
        Damaged?.Invoke(CurrentHealth, maxHealth);
        Debug.Log($"{name} took {damageAmount} damage. Health: {CurrentHealth}/{maxHealth}");

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    public void ResetHealth()
    {
        isDead = false;
        CurrentHealth = maxHealth;
    }

    public void Configure(int newMaxHealth, bool shouldDestroyOnDeath, bool shouldDeactivateOnDeath, float newDestroyDelay)
    {
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
        Debug.Log($"{name} died.");
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

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        destroyDelay = Mathf.Max(0f, destroyDelay);
    }
}
