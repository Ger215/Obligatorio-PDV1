using System.Collections;
using UnityEngine;

/// <summary>
/// Boss "Bringer of Death". El MELEE (guadañazo) lo maneja EnemyController + AttackSystem,
/// igual que cualquier enemigo. Este controller agrega el framework de boss (activación por
/// proximidad, barra de vida, victoria al morir) y el SPELL: un AoE a distancia telegrafiado
/// (animación Cast) que cae sobre la posición del player tras un delay, dándole tiempo a esquivar.
/// </summary>
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(Rigidbody2D))]
public class BringerOfDeathController : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string bossName = "Bringer of Death";

    [Header("Health Bar")]
    [SerializeField] private float healthBarProximity = 10f;

    [Header("Activation")]
    [SerializeField] private float activationRange = 12f;

    [Header("Spell (AoE a distancia)")]
    [Tooltip("Segundos entre hechizos.")]
    [SerializeField] private float spellCooldown = 7f;
    [Tooltip("Distancia máxima a la que el boss intenta lanzar el hechizo.")]
    [SerializeField] private float spellRange = 11f;
    [Tooltip("Distancia mínima: si el player está más cerca que esto, prefiere el melee y no castea.")]
    [SerializeField] private float spellMinRange = 3f;
    [Tooltip("Telegrafía: tiempo de la animación Cast antes de que caiga el daño.")]
    [SerializeField] private float castTime = 1f;
    [Tooltip("Radio del AoE del hechizo.")]
    [SerializeField] private float spellRadius = 2.5f;
    [Tooltip("Daño del hechizo.")]
    [SerializeField] private int spellDamage = 15;
    [Tooltip("Prefab de VFX del portal/pinchos (opcional). Se instancia en el punto del hechizo.")]
    [SerializeField] private GameObject spellVfxPrefab;
    [Tooltip("Cuánto vive el VFX antes de destruirse (si se asignó).")]
    [SerializeField] private float spellVfxLifetime = 1.5f;
    [SerializeField] private LayerMask playerLayer;

    private static readonly int AnimCast = Animator.StringToHash("cast");

    private EnemyController enemyController;
    private HealthSystem healthSystem;
    private Animator animator;

    private bool isActive;
    private bool isDead;
    private bool isCasting;
    private float nextSpellTime;
    private bool healthBarVisible;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        healthSystem = GetComponent<HealthSystem>();
        animator = GetComponent<Animator>();
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

    public void Activate()
    {
        isActive = true;
        nextSpellTime = Time.time + spellCooldown;
    }

    private void Update()
    {
        if (!isActive)
        {
            if (PlayerController.Instance != null)
            {
                float dist = Vector2.Distance(transform.position, PlayerController.Instance.transform.position);
                if (dist <= activationRange) Activate();
            }
        }

        if (!isActive || isDead) return;

        UpdateHealthBarVisibility();
        UpdateSpell();
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

    private void UpdateSpell()
    {
        if (isCasting || PlayerController.Instance == null) return;
        if (Time.time < nextSpellTime) return;

        float dist = Vector2.Distance(transform.position, PlayerController.Instance.transform.position);
        // Castea solo si el player está lejos (cerca lo deja al melee del EnemyController).
        if (dist > spellMinRange && dist <= spellRange)
        {
            StartCoroutine(CastSpell());
        }
    }

    private IEnumerator CastSpell()
    {
        isCasting = true;
        animator?.SetTrigger(AnimCast);

        // Telegrafía: fija el objetivo al inicio del cast para que el player pueda esquivar.
        Vector3 target = PlayerController.Instance != null
            ? PlayerController.Instance.transform.position
            : transform.position;

        yield return new WaitForSeconds(castTime);

        // Por si murió o se desactivó durante el cast.
        if (isDead)
        {
            isCasting = false;
            yield break;
        }

        if (spellVfxPrefab != null)
        {
            GameObject vfx = Instantiate(spellVfxPrefab, target, Quaternion.identity);
            if (spellVfxLifetime > 0f) Destroy(vfx, spellVfxLifetime);
        }

        // Daño AoE en el punto telegrafiado.
        Collider2D[] hits = Physics2D.OverlapCircleAll(target, spellRadius, playerLayer);
        foreach (Collider2D hit in hits)
        {
            hit.GetComponentInParent<HealthSystem>()?.TakeDamage(spellDamage);
        }

        nextSpellTime = Time.time + spellCooldown;
        isCasting = false;
    }

    private void HandleDamaged(int current, int max)
    {
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
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, spellRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spellMinRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, healthBarProximity);
    }
}
