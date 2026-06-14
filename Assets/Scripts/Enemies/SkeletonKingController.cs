using UnityEngine;

[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(Rigidbody2D))]
public class SkeletonKingController : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string bossName = "Skeleton King";

    [Header("Health Bar")]
    [SerializeField] private float healthBarProximity = 8f;

    [Header("Ground Slam")]
    [SerializeField] private float slamCooldown = 6f;
    [SerializeField] private float slamJumpForce = 18f;
    [SerializeField] private float slamRadius = 3f;
    [SerializeField] private int slamDamage = 20;
    [SerializeField] private float slamCameraDuration = 0.4f;
    [SerializeField] private float slamCameraMagnitude = 0.25f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Slam Trigger")]
    [SerializeField] private float slamTriggerRange = 6f;

    [Header("Telegraph (aviso antes del slam)")]
    [Tooltip("Segundos de aviso antes de saltar. El boss se queda QUIETO y parpadea para que el " +
             "jugador vea venir el golpe y pueda alejarse. Subilo para hacerlo más fácil de leer.")]
    [SerializeField] private float slamTelegraphDuration = 0.9f;
    [Tooltip("Color del parpadeo de aviso (amarillo/naranja = peligro inminente).")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0.8f, 0.15f);
    [Tooltip("Velocidad del parpadeo durante el aviso.")]
    [SerializeField] private float telegraphBlinkSpeed = 12f;

    [Header("Recuperación (ventana para golpearlo)")]
    [Tooltip("Segundos que el boss queda ATURDIDO tras aterrizar el slam. Es el momento débil: " +
             "no se mueve ni ataca, parpadea para mostrar que el jugador puede acercarse y pegarle.")]
    [SerializeField] private float slamRecoveryDuration = 1.5f;
    [Tooltip("Tramo final del aturdimiento en el que parpadea más rápido para avisar que la ventana " +
             "se está por cerrar.")]
    [SerializeField] private float stunWarningTime = 0.7f;
    [Tooltip("Cuánto más rápido parpadea en ese tramo final (multiplicador sobre la velocidad normal).")]
    [SerializeField] private float stunFastBlinkMultiplier = 2.75f;

    [Header("Activation")]
    [SerializeField] private float activationRange = 10f;

    private EnemyController enemyController;
    private HealthSystem healthSystem;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    private enum SlamPhase { Ready, Telegraph, Airborne, Recovery }
    private SlamPhase slamPhase = SlamPhase.Ready;
    private float phaseTimer;
    private bool leftGroundDuringSlam;

    private bool isActive;
    private bool isDead;
    private float nextSlamTime;
    private bool healthBarVisible;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        healthSystem = GetComponent<HealthSystem>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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
        nextSlamTime = Time.time + slamCooldown;
    }

    private void Update()
    {
        if (!isActive)
        {
            if (PlayerController.Instance != null)
            {
                float distToPlayer = Vector2.Distance(transform.position, PlayerController.Instance.transform.position);
                if (distToPlayer <= activationRange)
                {
                    Activate();
                }
            }
        }

        if (!isActive || isDead) return;

        UpdateHealthBarVisibility();
        UpdateGroundSlam();
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

    private void UpdateGroundSlam()
    {
        if (PlayerController.Instance == null) return;

        float distToPlayer = Vector2.Distance(transform.position, PlayerController.Instance.transform.position);

        switch (slamPhase)
        {
            case SlamPhase.Ready:
                // Espera el cooldown y que el player esté en rango para empezar el AVISO.
                if (Time.time >= nextSlamTime && distToPlayer <= slamTriggerRange && enemyController.IsGrounded)
                {
                    EnterTelegraph();
                }
                break;

            case SlamPhase.Telegraph:
                // Boss quieto y parpadeando: el jugador tiene tiempo de leer el golpe y alejarse.
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                BlinkTick(telegraphBlinkSpeed);
                phaseTimer -= Time.deltaTime;
                if (phaseTimer <= 0f)
                {
                    PerformSlam();
                }
                break;

            case SlamPhase.Airborne:
                // EnemyController vuelve a controlar el movimiento: el boss salta y cae hacia el player.
                // Detectamos el aterrizaje (volvió a tocar piso tras despegar).
                bool grounded = enemyController.IsGrounded;
                if (!grounded) leftGroundDuringSlam = true;
                if (grounded && leftGroundDuringSlam)
                {
                    OnSlamLand();
                    EnterRecovery();
                }
                break;

            case SlamPhase.Recovery:
                // Aturdido: no se mueve ni ataca. Parpadea para mostrar que es la ventana para pegarle,
                // y más rápido en el tramo final para avisar que se está por cerrar.
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                float recSpeed = phaseTimer <= stunWarningTime
                    ? telegraphBlinkSpeed * stunFastBlinkMultiplier
                    : telegraphBlinkSpeed;
                BlinkTick(recSpeed);
                phaseTimer -= Time.deltaTime;
                if (phaseTimer <= 0f)
                {
                    ExitRecovery();
                }
                break;
        }
    }

    private void EnterTelegraph()
    {
        slamPhase = SlamPhase.Telegraph;
        phaseTimer = slamTelegraphDuration;
        // Congelamos al boss durante el aviso. SetStunned frena movimiento/ataques pero deja el
        // componente activo, así LateUpdate mantiene el lock de rotación y el sprite no gira.
        enemyController.SetStunned(true);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void PerformSlam()
    {
        slamPhase = SlamPhase.Airborne;
        leftGroundDuringSlam = false;
        RestoreColor();
        // Lo soltamos para que el ground-check y el empuje hacia el player funcionen durante el salto.
        enemyController.SetStunned(false);
        rb.AddForce(Vector2.up * slamJumpForce, ForceMode2D.Impulse);
    }

    private void EnterRecovery()
    {
        slamPhase = SlamPhase.Recovery;
        phaseTimer = slamRecoveryDuration;
        // Aturdido: lo congelamos otra vez. El parpadeo (en la fase Recovery) señala que es vulnerable.
        enemyController.SetStunned(true);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void ExitRecovery()
    {
        slamPhase = SlamPhase.Ready;
        nextSlamTime = Time.time + slamCooldown;
        RestoreColor();
        enemyController.SetStunned(false);
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

    private void OnSlamLand()
    {
        // AoE damage
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, slamRadius, playerLayer);
        foreach (Collider2D hit in hits)
        {
            HealthSystem playerHealth = hit.GetComponent<HealthSystem>();
            playerHealth?.TakeDamage(slamDamage);
        }

        // Screen shake
        CameraController.Instance?.Shake(slamCameraDuration, slamCameraMagnitude);
    }

    private void HandleDamaged(int current, int max)
    {
        // Always update (or show) the boss health bar when damaged so hits are reflected in the UI
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

        // Si murió en medio de un aviso/aturdimiento, restauramos para que su animación de muerte
        // (manejada por EnemyController) corra normal y no quede congelado/teñido.
        slamPhase = SlamPhase.Ready;
        RestoreColor();
        if (enemyController != null) enemyController.SetStunned(false);

        GameHudController.Instance?.HideBossHealthBar();
        GameManager.Instance?.TriggerVictory();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, slamRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, healthBarProximity);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, slamTriggerRange);
    }
}
