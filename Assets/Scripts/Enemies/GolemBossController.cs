using UnityEngine;

/// <summary>
/// Boss melee simple (Golem): camina hacia el jugador y pega cuerpo a cuerpo. Todo el movimiento y el
/// ataque los maneja el <see cref="EnemyController"/> (+ <see cref="AttackSystem"/>); esta clase solo
/// agrega la capa de BOSS: se activa cuando el jugador se acerca, muestra/actualiza la barra de vida de
/// boss en el HUD y dispara la victoria del nivel al morir.
///
/// A diferencia del Skeleton King, el Golem no tiene ataques especiales (ni ground slam): "solo melee".
/// Poné este componente en el mismo GameObject que el EnemyController del Golem, con enemyType Walker o
/// Chaser en el EnemyController.
/// </summary>
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(HealthSystem))]
public class GolemBossController : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string bossName = "Golem";

    [Header("Activation")]
    [Tooltip("Distancia a la que el boss se despierta y empieza a perseguir. También lo activa el " +
             "BossTrigger si lo usás.")]
    [SerializeField] private float activationRange = 10f;

    [Header("Health Bar")]
    [Tooltip("Distancia a la que aparece la barra de vida de boss en el HUD.")]
    [SerializeField] private float healthBarProximity = 12f;

    private EnemyController enemyController;
    private HealthSystem healthSystem;

    private bool isActive;
    private bool isDead;
    private bool healthBarVisible;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        healthSystem = GetComponent<HealthSystem>();
    }

    private void OnEnable()
    {
        healthSystem.Damaged += HandleDamaged;
        healthSystem.Died += HandleDied;
    }

    private void OnDisable()
    {
        healthSystem.Damaged -= HandleDamaged;
        healthSystem.Died -= HandleDied;
    }

    /// <summary>Despierta al boss. Lo llama el BossTrigger o la proximidad del jugador.</summary>
    public void Activate()
    {
        isActive = true;
    }

    private void Update()
    {
        if (isDead) return;

        if (!isActive)
        {
            if (PlayerController.Instance != null)
            {
                float dist = Vector2.Distance(transform.position, PlayerController.Instance.transform.position);
                if (dist <= activationRange) Activate();
            }
            return;
        }

        UpdateHealthBarVisibility();
    }

    private void UpdateHealthBarVisibility()
    {
        if (PlayerController.Instance == null) return;

        float dist = Vector2.Distance(transform.position, PlayerController.Instance.transform.position);
        bool shouldShow = dist <= healthBarProximity;

        if (shouldShow && !healthBarVisible)
        {
            healthBarVisible = true;
            GameHudController.Instance?.ShowBossHealthBar(bossName, healthSystem.CurrentHealth, healthSystem.MaxHealth);
        }
        else if (!shouldShow && healthBarVisible)
        {
            healthBarVisible = false;
            GameHudController.Instance?.HideBossHealthBar();
        }
    }

    private void HandleDamaged(int current, int max)
    {
        // Reflejar el golpe en la barra aunque todavía no estuviera visible.
        if (!healthBarVisible)
        {
            healthBarVisible = true;
            GameHudController.Instance?.ShowBossHealthBar(bossName, current, max);
        }
        else
        {
            GameHudController.Instance?.UpdateBossHealthBar(current, max);
        }
    }

    private void HandleDied(HealthSystem _)
    {
        if (isDead) return;
        isDead = true;

        GameHudController.Instance?.HideBossHealthBar();
        GameManager.Instance?.TriggerVictory();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, activationRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, healthBarProximity);
    }
}
