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

    private Rigidbody2D rb;
    private AttackSystem attackSystem;
    private HealthSystem healthSystem;
    private ProjectileLauncher projectileLauncher;

    private EnemyType enemyType;
    private Transform playerTarget;
    private bool isDead;
    private bool isGrounded;
    private bool jumpConsumed;
    private int facingDirection = 1;
    private float nextPathRefreshTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        attackSystem = GetComponent<AttackSystem>();
        healthSystem = GetComponent<HealthSystem>();
        projectileLauncher = GetComponent<ProjectileLauncher>();

        if (groundCheck == null)
        {
            groundCheck = transform;
        }
    }

    private void OnEnable()
    {
        healthSystem.Died += HandleDeath;
    }

    private void OnDisable()
    {
        healthSystem.Died -= HandleDeath;
    }

    private void Start()
    {
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

    private void FixedUpdate()
    {
        if (isDead)
        {
            rb.linearVelocity = Vector2.zero;
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

        float horizontalVelocity = horizontalDistance <= attackDistance ? 0f : horizontalDirection * moveSpeed;
        rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);

        bool shouldJump = isGrounded && !jumpConsumed && verticalDistance > jumpTriggerHeight;

        if (shouldJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
            jumpConsumed = true;
        }
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

        attackSystem.AttackPointTransform.localPosition = new Vector3(facingDirection * attackPointDistance, 0f, 0f);
    }

    private void HandleDeath(HealthSystem deadHealthSystem)
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
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
                break;
            case EnemyType.Tank:
                moveSpeed *= 0.5f;
                attackDistance *= 1.4f;
                break;
            case EnemyType.Ranged:
                attackDistance *= 4f;
                verticalAttackTolerance = 10f;
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
