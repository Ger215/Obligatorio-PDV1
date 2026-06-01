using UnityEngine;

[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(Rigidbody2D))]
public class BossController : MonoBehaviour
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

    private EnemyController enemyController;
    private HealthSystem healthSystem;
    private Rigidbody2D rb;

    private bool isActive;
    private bool isDead;
    private bool isSlamming;
    private bool wasGroundedBeforeSlam;
    private float nextSlamTime;
    private bool healthBarVisible;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        healthSystem = GetComponent<HealthSystem>();
        rb = GetComponent<Rigidbody2D>();
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

        if (isSlamming)
        {
            // Detect landing after the slam jump
            bool isGroundedNow = enemyController.IsGrounded;
            if (isGroundedNow && !wasGroundedBeforeSlam)
            {
                OnSlamLand();
                isSlamming = false;
                nextSlamTime = Time.time + slamCooldown;
            }
            wasGroundedBeforeSlam = isGroundedNow;
            return;
        }

        if (Time.time >= nextSlamTime && distToPlayer <= slamTriggerRange && enemyController.IsGrounded)
        {
            PerformSlam();
        }
    }

    private void PerformSlam()
    {
        isSlamming = true;
        wasGroundedBeforeSlam = true;
        rb.AddForce(Vector2.up * slamJumpForce, ForceMode2D.Impulse);
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
        if (healthBarVisible)
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
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, slamRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, healthBarProximity);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, slamTriggerRange);
    }
}
