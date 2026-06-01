using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(AttackSystem))]
public class EnemyController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float attackDistance = 1.2f;
    [SerializeField] private float verticalAttackTolerance = 1f;
    [SerializeField] private float jumpTriggerHeight = 1.25f;
    [SerializeField] private float repathDelay = 0.5f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float groundCheckRadius = 0.15f;

    [Header("Combat Facing")]
    [SerializeField] private float attackPointDistance = 0.6f;

    [Header("Aggro")]
    [Tooltip("Radio dentro del cual el enemigo detecta y persigue al player. Fuera de este radio queda idle/wander.")]
    [SerializeField] private float aggroRadius = 8f;
    [Tooltip("Si true, deambula al azar cuando no detecta al player. Si false, se queda quieto.")]
    [SerializeField] private bool wanderWhenIdle = false;
    [Tooltip("Distancia máxima que se aleja del punto de spawn al deambular.")]
    [SerializeField] private float wanderRange = 3f;
    [Tooltip("Segundos entre cambios de dirección al deambular.")]
    [SerializeField] private float wanderChangeInterval = 2f;
    [Tooltip("Multiplicador de velocidad al deambular (0.5 = mitad de moveSpeed).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float wanderSpeedMultiplier = 0.5f;

    [Header("Navigation")]
    [SerializeField] private float wallCheckDistance = 0.35f;
    [SerializeField] private float ceilingCheckDistance = 1.5f;
    [SerializeField] private float groundAheadDistance = 0.6f;
    [SerializeField] private float stuckCheckInterval = 0.5f;
    [SerializeField] private float stuckMoveThreshold = 0.12f;
    [SerializeField] private float separationRadius = 1f;
    [SerializeField] private float separationForce = 4f;

    private Rigidbody2D rb;
    private AttackSystem attackSystem;
    private HealthSystem healthSystem;
    private ProjectileLauncher projectileLauncher;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private static readonly int AnimIsWalking = Animator.StringToHash("isWalking");
    private static readonly int AnimAttack    = Animator.StringToHash("attack");
    private static readonly int AnimHit       = Animator.StringToHash("hit");
    private static readonly int AnimDie       = Animator.StringToHash("die");

    private EnemyType enemyType;
    private float knockbackMultiplier = 1f;
    private Transform playerTarget;
    private bool isDead;
    private bool isGrounded;

    public bool IsGrounded => isGrounded;
    private bool jumpConsumed;
    private int facingDirection = 1;
    private float nextPathRefreshTime;
    private float knockbackEndTime;
    private float stuckTimer;
    private Vector3 stuckCheckPosition;
    private float dashEndTime;
    private float nextDashTime;
    private Vector3 spawnPosition;
    private int wanderDirection;
    private float nextWanderChangeTime;
    private bool isAggroed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        attackSystem = GetComponent<AttackSystem>();
        healthSystem = GetComponent<HealthSystem>();
        projectileLauncher = GetComponent<ProjectileLauncher>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        if (groundCheck == null)
        {
            groundCheck = transform;
        }
    }

    private void OnEnable()
    {
        healthSystem.Died += HandleDeath;
        healthSystem.Damaged += HandleDamaged;
        attackSystem.AttackResolved += HandleAttackResolved;
    }

    private void OnDisable()
    {
        healthSystem.Died -= HandleDeath;
        healthSystem.Damaged -= HandleDamaged;
        attackSystem.AttackResolved -= HandleAttackResolved;
    }

    private void Start()
    {
        attackDistance += Random.Range(-0.3f, 0.3f);
        stuckCheckPosition = transform.position;
        spawnPosition = transform.position;
        FindPlayerTarget();
        UpdateAttackPointPosition();
    }

    private bool IsPlayerInAggroRange(Vector2 deltaToPlayer)
    {
        return deltaToPlayer.sqrMagnitude <= aggroRadius * aggroRadius;
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        isGrounded = CheckGrounded();
        UpdateJumpState();

        if (playerTarget == null && Time.time >= nextPathRefreshTime)
        {
            FindPlayerTarget();
        }

        if (playerTarget == null)
        {
            isAggroed = false;
            return;
        }

        Vector2 deltaToPlayer = playerTarget.position - transform.position;
        isAggroed = IsPlayerInAggroRange(deltaToPlayer);

        if (!isAggroed)
        {
            // Fuera de rango: no actualizamos facing/attack según el player.
            return;
        }

        if (Mathf.Abs(deltaToPlayer.x) > 0.05f)
        {
            facingDirection = deltaToPlayer.x > 0f ? 1 : -1;
            UpdateAttackPointPosition();

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = facingDirection == -1;
            }
        }

        if (Mathf.Abs(deltaToPlayer.x) <= attackDistance && Mathf.Abs(deltaToPlayer.y) <= verticalAttackTolerance)
        {
            if (enemyType == EnemyType.Ranged && projectileLauncher != null)
            {
                projectileLauncher.TryLaunch(deltaToPlayer);
            }
            else
            {
                if (attackSystem.TryAttack())
                    animator?.SetTrigger(AnimAttack);
            }
        }
    }

    public void ApplyKnockback(float forceX)
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        rb.AddForce(new Vector2(forceX * knockbackMultiplier, 0f), ForceMode2D.Impulse);
        knockbackEndTime = Time.time + 0.3f;
    }

    private void FixedUpdate()
    {
        if (isDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (Time.time < knockbackEndTime)
        {
            return;
        }

        if (playerTarget == null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        Vector2 deltaToPlayer = playerTarget.position - transform.position;

        if (!IsPlayerInAggroRange(deltaToPlayer))
        {
            ApplyIdleOrWander();
            animator?.SetBool(AnimIsWalking, Mathf.Abs(rb.linearVelocity.x) > 0.1f);
            return;
        }

        float horizontalDistance = Mathf.Abs(deltaToPlayer.x);
        float verticalDistance = deltaToPlayer.y;
        float horizontalDirection = Mathf.Sign(deltaToPlayer.x);

        if (horizontalDistance > 0.05f)
        {
            facingDirection = horizontalDirection >= 0f ? 1 : -1;
        }

        if (enemyType == EnemyType.Ranged)
        {
            bool inRange = horizontalDistance <= attackDistance && Mathf.Abs(deltaToPlayer.y) <= verticalAttackTolerance;
            float horizontalFly = inRange ? 0f : horizontalDirection * moveSpeed;
            float verticalFly = Mathf.Sin(Time.time * 4f) * 2f;
            rb.linearVelocity = new Vector2(horizontalFly, verticalFly);
            return;
        }

        bool wallAhead = enemyType != EnemyType.Fast && CheckWallAhead();
        bool ceilingAbove = enemyType != EnemyType.Fast && CheckCeilingAbove();
        bool groundAhead = CheckGroundAhead();
        bool playerBelow = verticalDistance < -verticalAttackTolerance;
        bool playerAttackable = Mathf.Abs(deltaToPlayer.y) <= verticalAttackTolerance;
        bool isFalling = !isGrounded && rb.linearVelocity.y < 0f;
        bool forceHorizontal = (ceilingAbove && wallAhead) || (playerBelow && !groundAhead);

        if (!isFalling)
        {
            // Force movement: under a platform (ceiling+wall) or walking off a ledge toward player below
            float horizontalVelocity = (!forceHorizontal && horizontalDistance <= attackDistance && playerAttackable)
                ? 0f
                : horizontalDirection * moveSpeed;
            rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);

            if (enemyType == EnemyType.Fast)
                TryFastDash(horizontalDistance, horizontalDirection);
        }

        // Only jump if no ceiling is blocking the path
        bool shouldJump = isGrounded && !jumpConsumed && enemyType != EnemyType.Fast && enemyType != EnemyType.Walker
            && !ceilingAbove
            && (verticalDistance > jumpTriggerHeight || wallAhead);

        if (shouldJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
            jumpConsumed = true;
        }

        bool tryingToMove = horizontalDistance > attackDistance || forceHorizontal;
        stuckTimer += Time.fixedDeltaTime;
        if (stuckTimer >= stuckCheckInterval)
        {
            float moved = Vector3.Distance(transform.position, stuckCheckPosition);
            if (tryingToMove && moved < stuckMoveThreshold && isGrounded && !jumpConsumed
                && enemyType != EnemyType.Fast && enemyType != EnemyType.Walker && !ceilingAbove)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                isGrounded = false;
                jumpConsumed = true;
            }
            stuckTimer = 0f;
            stuckCheckPosition = transform.position;
        }

        ApplySeparation();
        animator?.SetBool(AnimIsWalking, Mathf.Abs(rb.linearVelocity.x) > 0.1f);
    }

    private void ApplyIdleOrWander()
    {
        // Ranged flota; idle = quieto (corta el sin wave y la velocidad).
        if (enemyType == EnemyType.Ranged)
        {
            rb.linearVelocity = new Vector2(0f, 0f);
            return;
        }

        if (!wanderWhenIdle)
        {
            // Idle puro: cortar horizontal, dejar gravedad
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // Wander: cambiar dirección cada wanderChangeInterval. 30% chance de pausar.
        if (Time.time >= nextWanderChangeTime)
        {
            float r = Random.value;
            if (r < 0.3f) wanderDirection = 0;
            else wanderDirection = Random.value < 0.5f ? -1 : 1;
            nextWanderChangeTime = Time.time + wanderChangeInterval;
        }

        // Cortar si nos alejamos del spawn
        float distFromSpawn = transform.position.x - spawnPosition.x;
        if (distFromSpawn > wanderRange && wanderDirection > 0) wanderDirection = -1;
        else if (distFromSpawn < -wanderRange && wanderDirection < 0) wanderDirection = 1;

        if (wanderDirection != 0)
        {
            facingDirection = wanderDirection;
            // Evitar caer al vacío o chocar contra pared
            if (!CheckGroundAhead() || CheckWallAhead())
            {
                wanderDirection = -wanderDirection;
                facingDirection = wanderDirection;
            }

            if (spriteRenderer != null) spriteRenderer.flipX = facingDirection == -1;
        }

        rb.linearVelocity = new Vector2(wanderDirection * moveSpeed * wanderSpeedMultiplier, rb.linearVelocity.y);
    }

    private void TryFastDash(float horizontalDistance, float horizontalDirection)
    {
        float dashTriggerRange = attackDistance * 3.5f;
        bool canDash = Time.time >= nextDashTime
            && horizontalDistance > attackDistance
            && horizontalDistance <= dashTriggerRange;

        if (canDash)
        {
            dashEndTime = Time.time + 0.18f;
            nextDashTime = Time.time + 1.8f;
        }

        if (Time.time < dashEndTime)
            rb.linearVelocity = new Vector2(horizontalDirection * moveSpeed * 2.5f, rb.linearVelocity.y);
    }

    private void FindPlayerTarget()
    {
        nextPathRefreshTime = Time.time + repathDelay;
        playerTarget = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
    }

    public void Configure(
        float newMoveSpeed,
        float newJumpForce,
        float newAttackDistance,
        float newVerticalAttackTolerance,
        float newJumpTriggerHeight,
        float newRepathDelay,
        float newAttackPointDistance,
        Transform newGroundCheck,
        LayerMask newGroundLayers,
        float newGroundCheckRadius)
    {
        moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
        jumpForce = Mathf.Max(0.1f, newJumpForce);
        attackDistance = Mathf.Max(0.1f, newAttackDistance);
        verticalAttackTolerance = Mathf.Max(0.1f, newVerticalAttackTolerance);
        jumpTriggerHeight = Mathf.Max(0.1f, newJumpTriggerHeight);
        repathDelay = Mathf.Max(0.1f, newRepathDelay);
        attackPointDistance = Mathf.Max(0.1f, newAttackPointDistance);
        groundCheck = newGroundCheck != null ? newGroundCheck : transform;
        groundLayers = newGroundLayers;
        groundCheckRadius = Mathf.Max(0.05f, newGroundCheckRadius);
        UpdateAttackPointPosition();
    }

    private bool CheckWallAhead()
    {
        if (groundLayers == 0) return false;
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.1f;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * facingDirection, wallCheckDistance, groundLayers);
        return hit.collider != null;
    }

    private bool CheckCeilingAbove()
    {
        if (groundLayers == 0) return false;
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.2f;
        return Physics2D.Raycast(origin, Vector2.up, ceilingCheckDistance, groundLayers).collider != null;
    }

    private bool CheckGroundAhead()
    {
        if (groundLayers == 0) return true;
        // Disparamos desde los pies (groundCheck) un poco arriba para no nacer dentro del suelo.
        Vector2 footPos = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
        Vector2 ahead = footPos + Vector2.right * facingDirection * 0.4f + Vector2.up * 0.1f;
        return Physics2D.Raycast(ahead, Vector2.down, groundAheadDistance + 0.2f, groundLayers).collider != null;
    }

    private void ApplySeparation()
    {
        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, separationRadius, 1 << 8);
        foreach (Collider2D col in nearby)
        {
            if (col.gameObject == gameObject) continue;
            Vector2 away = (Vector2)(transform.position - col.transform.position);
            if (away.sqrMagnitude < 0.001f) continue;
            float strength = 1f - (away.magnitude / separationRadius);
            rb.AddForce(away.normalized * separationForce * strength, ForceMode2D.Force);
        }
    }

    private bool CheckGrounded()
    {
        if (groundCheck == null)
        {
            return false;
        }

        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayers) != null;
    }

    private void UpdateJumpState()
    {
        if (isGrounded && Mathf.Abs(rb.linearVelocity.y) <= 0.05f)
        {
            jumpConsumed = false;
        }
    }

    private void UpdateAttackPointPosition()
    {
        if (attackSystem == null || attackSystem.AttackPointTransform == null)
        {
            return;
        }

        float scale = Mathf.Abs(transform.localScale.x);
        float localDist = scale > 0.001f ? attackPointDistance / scale : attackPointDistance;
        attackSystem.AttackPointTransform.localPosition = new Vector3(facingDirection * localDist, 0f, 0f);
    }

    private void HandleAttackResolved(bool critical, int damage, int targetsHit)
    {
        AudioManager.Instance?.PlayEnemyAttack();
    }

    private void HandleDamaged(int current, int max)
    {
        AudioManager.Instance?.PlayEnemyDamaged();
        animator?.SetTrigger(AnimHit);
        if (spriteRenderer != null)
        {
            StartCoroutine(HitFlash());
        }
    }

    private System.Collections.IEnumerator HitFlash()
    {
        spriteRenderer.color = new Color(1f, 0.2f, 0.2f, 1f);
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = Color.white;
    }

    private void HandleDeath(HealthSystem deadHealthSystem)
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        AudioManager.Instance?.PlayEnemyDeath();
        animator?.SetTrigger(AnimDie);
    }

    public void ApplyMovementConfig(float newMoveSpeed, float newJumpForce, float newAttackDistance,
        float newVerticalAttackTolerance, float newJumpTriggerHeight, float newRepathDelay, float newAttackPointDistance)
    {
        moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
        jumpForce = Mathf.Max(0.1f, newJumpForce);
        attackDistance = Mathf.Max(0.1f, newAttackDistance);
        verticalAttackTolerance = Mathf.Max(0.1f, newVerticalAttackTolerance);
        jumpTriggerHeight = Mathf.Max(0.1f, newJumpTriggerHeight);
        repathDelay = Mathf.Max(0.1f, newRepathDelay);
        attackPointDistance = Mathf.Max(0.1f, newAttackPointDistance);
        UpdateAttackPointPosition();
    }

    public void ApplyDifficultyMultiplier(float moveSpeedMultiplier)
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed * moveSpeedMultiplier);
    }

    public void SetEnemyType(EnemyType type)
    {
        enemyType = type;

        switch (type)
        {
            case EnemyType.Fast:
                moveSpeed *= 1.8f;
                attackDistance *= 0.85f;
                knockbackMultiplier = 1.8f;
                break;
            case EnemyType.Tank:
                moveSpeed *= 0.5f;
                attackDistance *= 1.4f;
                knockbackMultiplier = 0.4f;
                break;
            case EnemyType.Ranged:
                attackDistance *= 4f;
                verticalAttackTolerance = 10f;
                knockbackMultiplier = 1.2f;
                break;
            case EnemyType.Chaser:
                knockbackMultiplier = 1f;
                break;
            case EnemyType.Walker:
                wanderWhenIdle = true;   // siempre patrulla
                knockbackMultiplier = 0.5f;
                break;
        }

        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        attackDistance = Mathf.Max(0.1f, attackDistance);
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        jumpForce = Mathf.Max(0.1f, jumpForce);
        attackDistance = Mathf.Max(0.1f, attackDistance);
        verticalAttackTolerance = Mathf.Max(0.1f, verticalAttackTolerance);
        jumpTriggerHeight = Mathf.Max(0.1f, jumpTriggerHeight);
        repathDelay = Mathf.Max(0.1f, repathDelay);
        attackPointDistance = Mathf.Max(0.1f, attackPointDistance);
        groundCheckRadius = Mathf.Max(0.05f, groundCheckRadius);
    }

    private void OnDrawGizmosSelected()
    {
        // Aggro radius
        Gizmos.color = isAggroed ? Color.red : new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, aggroRadius);

        // Wander range (solo si está activado)
        if (wanderWhenIdle)
        {
            Gizmos.color = Color.cyan;
            Vector3 spawn = Application.isPlaying ? spawnPosition : transform.position;
            Gizmos.DrawLine(spawn + Vector3.left * wanderRange, spawn + Vector3.right * wanderRange);
        }

        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        // Walker: dirección de movimiento
        if (enemyType == EnemyType.Walker)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawRay(transform.position, Vector3.right * facingDirection * 0.8f);
        }
    }
}
