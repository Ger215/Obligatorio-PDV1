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

    private EnemyType enemyType;
    private float knockbackMultiplier = 1f;
    private Transform playerTarget;
    private bool isDead;
    private bool isGrounded;
    private bool jumpConsumed;
    private int facingDirection = 1;
    private float nextPathRefreshTime;
    private float knockbackEndTime;
    private float stuckTimer;
    private Vector3 stuckCheckPosition;
    private float dashEndTime;
    private float nextDashTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        attackSystem = GetComponent<AttackSystem>();
        healthSystem = GetComponent<HealthSystem>();
        projectileLauncher = GetComponent<ProjectileLauncher>();
        spriteRenderer = GetComponent<SpriteRenderer>();

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
        FindPlayerTarget();
        UpdateAttackPointPosition();
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
            return;
        }

        Vector2 deltaToPlayer = playerTarget.position - transform.position;

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
                attackSystem.TryAttack();
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

        // Force movement: under a platform (ceiling+wall) or walking off a ledge toward player below
        bool forceHorizontal = (ceilingAbove && wallAhead) || (playerBelow && !groundAhead);
        float horizontalVelocity = (!forceHorizontal && horizontalDistance <= attackDistance && playerAttackable)
            ? 0f
            : horizontalDirection * moveSpeed;
        rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);

        if (enemyType == EnemyType.Fast)
            TryFastDash(horizontalDistance, horizontalDirection);

        // Only jump if no ceiling is blocking the path
        bool shouldJump = isGrounded && !jumpConsumed && enemyType != EnemyType.Fast
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
                && enemyType != EnemyType.Fast && !ceilingAbove)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                isGrounded = false;
                jumpConsumed = true;
            }
            stuckTimer = 0f;
            stuckCheckPosition = transform.position;
        }

        ApplySeparation();
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
        Vector2 ahead = (Vector2)transform.position + Vector2.right * facingDirection * 0.4f;
        return Physics2D.Raycast(ahead, Vector2.down, groundAheadDistance, groundLayers).collider != null;
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
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = isGrounded ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
