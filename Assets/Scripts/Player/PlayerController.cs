using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(AttackSystem))]
public class PlayerController : SingletonBehaviour<PlayerController>
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 12f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.75f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float groundCheckRadius = 0.15f;

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
    private bool jumpQueued;
    private bool isGrounded;
    private bool jumpConsumed;
    private float dashTimeRemaining;
    private float lastDashTime = -Mathf.Infinity;

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
        UpdateJumpState();
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

        Vector2 currentVelocity = rb.linearVelocity;

        if (isDashing)
        {
            rb.linearVelocity = new Vector2(facingDirection * dashSpeed, currentVelocity.y);
            dashTimeRemaining -= Time.fixedDeltaTime;

            if (dashTimeRemaining <= 0f)
            {
                isDashing = false;
            }

            return;
        }

        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, currentVelocity.y);

        if (jumpQueued && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;
            jumpConsumed = true;
            AudioManager.Instance?.PlayJump();
        }

        jumpQueued = false;
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

        float gamepadInput = Gamepad.current != null ? Gamepad.current.leftStick.ReadValue().x : 0f;
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

        if (jumpPressed && !jumpConsumed)
        {
            jumpQueued = true;
        }
    }

    private void UpdateJumpState()
    {
        if (isGrounded && Mathf.Abs(rb.linearVelocity.y) <= 0.05f)
        {
            jumpConsumed = false;
        }
    }

    private void ReadAttackInput()
    {
        bool attackPressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                             (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                             (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

        if (attackPressed)
        {
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
        float newGroundCheckRadius)
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

        attackSystem.AttackPointTransform.localPosition = new Vector3(facingDirection * attackPointDistance, 0f, 0f);
    }

    private void HandleDeath(HealthSystem deadHealthSystem)
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
    }

    private void HandleDamaged(int currentHealth, int maxHealth)
    {
        AudioManager.Instance?.PlayPlayerDamaged();
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
