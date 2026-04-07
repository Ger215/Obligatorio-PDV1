using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(AttackSystem))]
public class PlayerController : SingletonBehaviour<PlayerController>
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float deceleration = 80f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float fallGravityMultiplier = 2.5f;
    [SerializeField] private float jumpCutGravityMultiplier = 2f;
    [SerializeField] private float maxFallSpeed = 18f;
    [SerializeField] private bool canDoubleJump = true;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.75f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float groundCheckRadius = 0.15f;

    [Header("Wall Check")]
    [SerializeField] private float wallCheckDistance = 0.35f;
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.1f, 0.8f);

    [Header("Combat Facing")]
    [SerializeField] private float attackPointDistance = 0.75f;

    private Rigidbody2D rb;
    private AttackSystem attackSystem;
    private HealthSystem healthSystem;
    private PlayerAbilityController abilityController;

    private float horizontalInput;
    private int facingDirection = 1;
    private bool isDashing;
    private bool isDead;
    private bool isGrounded;
    private bool jumpHeld;
    private bool hasDoubleJumped;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private float dashTimeRemaining;
    private float lastDashTime = -Mathf.Infinity;
    private float defaultGravityScale;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
        {
            return;
        }

        rb = GetComponent<Rigidbody2D>();
        attackSystem = GetComponent<AttackSystem>();
        healthSystem = GetComponent<HealthSystem>();
        abilityController = GetComponent<PlayerAbilityController>();
        defaultGravityScale = rb.gravityScale;

        PhysicsMaterial2D frictionless = new PhysicsMaterial2D("PlayerFrictionless")
        {
            friction = 0f,
            bounciness = 0f
        };
        rb.sharedMaterial = frictionless;
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            if (!col.isTrigger)
            {
                col.sharedMaterial = frictionless;
            }
        }

        if (groundCheck == null)
        {
            groundCheck = transform;
        }

        UpdateAttackPointPosition();
    }

    private void OnEnable()
    {
        if (Instance != this)
        {
            return;
        }

        healthSystem.Died += HandleDeath;
        healthSystem.Damaged += HandleDamaged;
        attackSystem.AttackResolved += HandleAttackResolved;
    }

    private void OnDisable()
    {
        if (Instance != this || healthSystem == null)
        {
            return;
        }

        healthSystem.Died -= HandleDeath;
        healthSystem.Damaged -= HandleDamaged;

        if (attackSystem != null)
        {
            attackSystem.AttackResolved -= HandleAttackResolved;
        }
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        isGrounded = CheckGrounded();
        UpdateTimers();
        ReadMovementInput();
        ReadJumpInput();
        ReadAttackInput();
        ReadDashInput();
        ReadAbilityInput();
    }

    private void FixedUpdate()
    {
        if (isDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isDashing)
        {
            rb.linearVelocity = new Vector2(facingDirection * dashSpeed, 0f);
            rb.gravityScale = 0f;
            dashTimeRemaining -= Time.fixedDeltaTime;

            if (dashTimeRemaining <= 0f)
            {
                isDashing = false;
                rb.gravityScale = defaultGravityScale;
            }

            return;
        }

        ApplyHorizontalMovement();
        ApplyGravityModifiers();
        TryJump();
    }

    private bool IsTouchingWall(int dir)
    {
        float bottomY = groundCheck != null ? groundCheck.position.y : transform.position.y;
        Vector2 origin = new Vector2(
            transform.position.x + dir * (wallCheckDistance * 0.5f),
            bottomY + wallCheckSize.y * 0.5f);
        return Physics2D.OverlapBox(origin, wallCheckSize, 0f, groundLayers) != null;
    }

    private void UpdateTimers()
    {
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
            hasDoubleJumped = false;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    private void ApplyHorizontalMovement()
    {
        float targetSpeed = horizontalInput * moveSpeed;

        if (!isGrounded && Mathf.Abs(targetSpeed) > 0.01f)
        {
            int dir = targetSpeed > 0f ? 1 : -1;
            if (IsTouchingWall(dir))
            {
                targetSpeed = 0f;
            }
        }

        float accelRate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
        float newSpeed = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newSpeed, rb.linearVelocity.y);
    }

    private void ApplyGravityModifiers()
    {
        if (rb.linearVelocity.y < -0.01f)
        {
            rb.gravityScale = defaultGravityScale * fallGravityMultiplier;
            float clampedY = Mathf.Max(rb.linearVelocity.y, -maxFallSpeed);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, clampedY);
        }
        else if (rb.linearVelocity.y > 0.01f && !jumpHeld)
        {
            rb.gravityScale = defaultGravityScale * jumpCutGravityMultiplier;
        }
        else
        {
            rb.gravityScale = defaultGravityScale;
        }
    }

    private void TryJump()
    {
        if (jumpBufferCounter <= 0f)
        {
            return;
        }

        bool canJumpFromGround = coyoteTimeCounter > 0f;
        bool canJumpDouble = canDoubleJump && !hasDoubleJumped && !canJumpFromGround;

        if (canJumpFromGround)
        {
            PerformJump();
            coyoteTimeCounter = 0f;
        }
        else if (canJumpDouble)
        {
            PerformJump();
            hasDoubleJumped = true;
        }
    }

    private void PerformJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        jumpBufferCounter = 0f;
        AudioManager.Instance?.PlayJump();
    }

    private void ReadMovementInput()
    {
        float keyboardInput = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                keyboardInput -= 1f;
            }

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                keyboardInput += 1f;
            }
        }

        float rawGamepad = Gamepad.current != null ? Gamepad.current.leftStick.ReadValue().x : 0f;
        float gamepadInput = Mathf.Abs(rawGamepad) > 0.2f ? rawGamepad : 0f;
        horizontalInput = Mathf.Clamp(keyboardInput + gamepadInput, -1f, 1f);

        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            facingDirection = horizontalInput > 0f ? 1 : -1;
            UpdateAttackPointPosition();
        }
    }

    private void ReadJumpInput()
    {
        bool jumpPressed = (Keyboard.current != null &&
                           (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)) ||
                          (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);

        jumpHeld = (Keyboard.current != null &&
                   (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)) ||
                  (Gamepad.current != null && Gamepad.current.buttonNorth.isPressed);

        if (jumpPressed)
        {
            jumpBufferCounter = jumpBufferTime;
        }
    }

    private void ReadAttackInput()
    {
        bool attackPressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                             (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                             (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

        if (attackPressed)
        {
            ApplyKnockbackToNearbyEnemies();
            attackSystem.TryAttack();
        }
    }

    private void ReadAbilityInput()
    {
        if (abilityController == null || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            abilityController.TryUseAbilitySlot(0);
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            abilityController.TryUseAbilitySlot(1);
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            abilityController.TryUseAbilitySlot(2);
        }
    }

    private void ReadDashInput()
    {
        bool dashPressed = (Keyboard.current != null &&
                           (Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame)) ||
                          (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame);

        if (!dashPressed || Time.time < lastDashTime + dashCooldown)
        {
            return;
        }

        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            facingDirection = horizontalInput > 0f ? 1 : -1;
        }

        lastDashTime = Time.time;
        isDashing = true;
        dashTimeRemaining = dashDuration;
        UpdateAttackPointPosition();
    }

    public void Configure(
        float newMoveSpeed,
        float newJumpForce,
        float newDashSpeed,
        float newDashDuration,
        float newDashCooldown,
        float newAttackPointDistance,
        Transform newGroundCheck,
        LayerMask newGroundLayers,
        float newGroundCheckRadius,
        float newAcceleration,
        float newDeceleration,
        float newCoyoteTime,
        float newJumpBufferTime,
        float newFallGravityMultiplier,
        float newJumpCutGravityMultiplier,
        float newMaxFallSpeed,
        bool newCanDoubleJump)
    {
        moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
        jumpForce = Mathf.Max(0.1f, newJumpForce);
        dashSpeed = Mathf.Max(moveSpeed, newDashSpeed);
        dashDuration = Mathf.Max(0.01f, newDashDuration);
        dashCooldown = Mathf.Max(0.01f, newDashCooldown);
        attackPointDistance = Mathf.Max(0.1f, newAttackPointDistance);
        groundCheck = newGroundCheck != null ? newGroundCheck : transform;
        groundLayers = newGroundLayers;
        groundCheckRadius = Mathf.Max(0.05f, newGroundCheckRadius);
        acceleration = Mathf.Max(0.1f, newAcceleration);
        deceleration = Mathf.Max(0.1f, newDeceleration);
        coyoteTime = Mathf.Max(0f, newCoyoteTime);
        jumpBufferTime = Mathf.Max(0f, newJumpBufferTime);
        fallGravityMultiplier = Mathf.Max(1f, newFallGravityMultiplier);
        jumpCutGravityMultiplier = Mathf.Max(1f, newJumpCutGravityMultiplier);
        maxFallSpeed = Mathf.Max(1f, newMaxFallSpeed);
        canDoubleJump = newCanDoubleJump;
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

    private void UpdateAttackPointPosition()
    {
        if (attackSystem == null || attackSystem.AttackPointTransform == null)
        {
            return;
        }

        float currentY = attackSystem.AttackPointTransform.localPosition.y;
        attackSystem.AttackPointTransform.localPosition = new Vector3(facingDirection * attackPointDistance, currentY, 0f);
    }

    private void HandleDeath(HealthSystem deadHealthSystem)
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = defaultGravityScale;
    }

    private void HandleDamaged(int currentHealth, int maxHealth)
    {
        AudioManager.Instance?.PlayPlayerDamaged();
    }

    private void ApplyKnockbackToNearbyEnemies()
    {
        float knockbackForce = attackSystem != null ? attackSystem.KnockbackForce : 6f;
        Collider2D[] nearby = Physics2D.OverlapCircleAll(attackSystem.AttackPointTransform.position, attackSystem.AttackRange, 1 << 8);

        foreach (Collider2D col in nearby)
        {
            EnemyController enemy = col.GetComponentInParent<EnemyController>();
            if (enemy == null) continue;
            float dir = col.transform.position.x > transform.position.x ? 1f : -1f;
            enemy.ApplyKnockback(dir * knockbackForce);
        }
    }

    private void HandleAttackResolved(bool critical, int damage, int targetsHit)
    {
        if (targetsHit > 0)
        {
            AudioManager.Instance?.PlayAttack(critical);
        }
    }

    public int FacingDirection => facingDirection;
    public Rigidbody2D Rigidbody => rb;
    public AttackSystem AttackSystem => attackSystem;
    public HealthSystem HealthSystem => healthSystem;
    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public float VerticalVelocity => rb != null ? rb.linearVelocity.y : 0f;
    public float HorizontalInput => horizontalInput;

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        jumpForce = Mathf.Max(0.1f, jumpForce);
        dashSpeed = Mathf.Max(moveSpeed, dashSpeed);
        dashDuration = Mathf.Max(0.01f, dashDuration);
        dashCooldown = Mathf.Max(0.01f, dashCooldown);
        attackPointDistance = Mathf.Max(0.1f, attackPointDistance);
        groundCheckRadius = Mathf.Max(0.05f, groundCheckRadius);
        acceleration = Mathf.Max(0.1f, acceleration);
        deceleration = Mathf.Max(0.1f, deceleration);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
        fallGravityMultiplier = Mathf.Max(1f, fallGravityMultiplier);
        jumpCutGravityMultiplier = Mathf.Max(1f, jumpCutGravityMultiplier);
        maxFallSpeed = Mathf.Max(1f, maxFallSpeed);
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
