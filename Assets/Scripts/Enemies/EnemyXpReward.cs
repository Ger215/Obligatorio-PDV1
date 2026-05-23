using UnityEngine;

/// <summary>
/// Cada enemigo lleva este componente para declarar cuánto XP da al morir.
/// Se suscribe a su propio HealthSystem.Died y avisa al GameManager.
/// </summary>
[RequireComponent(typeof(HealthSystem))]
public class EnemyXpReward : MonoBehaviour
{
    [Tooltip("XP que recibe el jugador al matarlo.")]
    [SerializeField] private int xpReward = 5;

    private HealthSystem health;

    private void Awake()
    {
        health = GetComponent<HealthSystem>();
    }

    private void OnEnable()
    {
        if (health != null) health.Died += HandleDied;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= HandleDied;
    }

    private void HandleDied(HealthSystem _)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AwardEnemyKill(transform.position, xpReward);
        }
    }
}
