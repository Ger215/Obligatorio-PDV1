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

    [Header("Aturdimiento tras el melee (ventana para golpearlo)")]
    [Tooltip("Color del parpadeo de aturdimiento tras el guadañazo. Rojo intenso = vulnerable.")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0.25f, 0.05f);
    [Tooltip("Velocidad del parpadeo. Más bajo = pulsos más lentos y visibles.")]
    [SerializeField] private float telegraphBlinkSpeed = 9f;
    [Tooltip("Segundos que el boss queda aturdido tras el guadañazo: no se mueve ni ataca, parpadea " +
             "del color de aviso para mostrar que es el momento de pegarle.")]
    [SerializeField] private float meleeStunDuration = 2f;
    [Tooltip("Tramo final del aturdimiento en el que parpadea más rápido para avisar que la ventana " +
             "se está por cerrar.")]
    [SerializeField] private float stunWarningTime = 0.7f;
    [Tooltip("Cuánto más rápido parpadea en ese tramo final (multiplicador sobre la velocidad normal).")]
    [SerializeField] private float stunFastBlinkMultiplier = 2.75f;

    private static readonly int AnimCast = Animator.StringToHash("cast");

    private EnemyController enemyController;
    private HealthSystem healthSystem;
    private Animator animator;
    private AttackSystem attackSystem;
    private SpriteRenderer spriteRenderer;

    private bool isActive;
    private bool isDead;
    private bool isCasting;
    private float nextSpellTime;
    private bool healthBarVisible;
    private Coroutine meleeStunRoutine;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        healthSystem = GetComponent<HealthSystem>();
        animator = GetComponent<Animator>();
        attackSystem = GetComponent<AttackSystem>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()
    {
        healthSystem.Damaged += HandleDamaged;
        healthSystem.Died += HandleDied;
        if (attackSystem != null)
        {
            attackSystem.AttackCompleted += HandleAttackCompleted;
        }
    }

    private void OnDisable()
    {
        healthSystem.Damaged -= HandleDamaged;
        healthSystem.Died -= HandleDied;
        if (attackSystem != null)
        {
            attackSystem.AttackCompleted -= HandleAttackCompleted;
        }
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

        // El cast NO tiñe el sprite: solo espera la telegrafía de la animación.
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

    // Tras terminar el guadañazo (AttackCompleted dispara después de tu animation event de golpe),
    // el boss queda aturdido y vulnerable: frenado, parpadeando del color de aviso. En el tramo
    // final parpadea más rápido para avisar que la ventana se cierra.
    private void HandleAttackCompleted()
    {
        if (isDead || meleeStunDuration <= 0f) return;

        if (meleeStunRoutine != null) StopCoroutine(meleeStunRoutine);
        meleeStunRoutine = StartCoroutine(MeleeStun());
    }

    private IEnumerator MeleeStun()
    {
        if (enemyController != null) enemyController.SetStunned(true);

        float elapsed = 0f;
        while (elapsed < meleeStunDuration && !isDead)
        {
            float remaining = meleeStunDuration - elapsed;
            // Cerca del final acelera el parpadeo para avisar que el aturdimiento se termina.
            float speed = remaining <= stunWarningTime
                ? telegraphBlinkSpeed * stunFastBlinkMultiplier
                : telegraphBlinkSpeed;
            BlinkTick(speed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        RestoreColor();
        if (enemyController != null) enemyController.SetStunned(false);
        meleeStunRoutine = null;
    }

    private void BlinkTick(float speed)
    {
        if (spriteRenderer == null) return;
        // Pulsa entre el color fuerte y una versión apenas más clara: nunca vuelve al blanco puro,
        // así el aviso se ve intenso todo el tiempo en vez de "lavarse".
        float t = Mathf.PingPong(Time.time * speed, 1f);
        Color peak = Color.Lerp(telegraphColor, Color.white, 0.45f);
        spriteRenderer.color = Color.Lerp(telegraphColor, peak, t);
    }

    private void RestoreColor()
    {
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
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

        RestoreColor();
        if (enemyController != null) enemyController.SetStunned(false);
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
