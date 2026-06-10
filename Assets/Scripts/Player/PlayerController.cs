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

    [Header("Slope Sliding")]
    [Tooltip("Ángulo mínimo (grados) de la pendiente para que el player empiece a deslizarse.")]
    [SerializeField] private float minSlideAngle = 10f;
    [Tooltip("Ángulo máximo. Por encima se considera pared, no piso.")]
    [SerializeField] private float maxSlideAngle = 60f;
    [Tooltip("Aceleración del slide en m/s². El slide se va acelerando solo.")]
    [SerializeField] private float slideAcceleration = 10f;
    [Tooltip("Velocidad tope del slide.")]
    [SerializeField] private float maxSlideSpeed = 14f;
    [Tooltip("Distancia del raycast para detectar la normal de la pendiente.")]
    [SerializeField] private float slideRayDistance = 0.6f;
    [Tooltip("Segundos que tarda en empezar a deslizar después de soltar input estando quieto en pendiente.")]
    [SerializeField] private float slideStartDelay = 0.4f;
    [Tooltip("Ancho horizontal del check de pendiente: lanza 3 raycasts a -W, 0, +W desde groundCheck. " +
             "Más ancho = más permisivo con colliders irregulares.")]
    [SerializeField] private float slideCheckWidth = 0.25f;
    [Tooltip("Tiempo de gracia: una vez deslizando, sigue deslizando aunque pierda detección de pendiente. " +
             "Evita cortes feos por colliders irregulares.")]
    [SerializeField] private float slideExitGrace = 0.15f;

    [Header("Wall Check")]
    [SerializeField] private float wallCheckDistance = 0.35f;
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.1f, 0.8f);

    [Header("Ladder")]
    [SerializeField] private float climbSpeed = 3f;
    [SerializeField] private LayerMask ladderLayers;

    [Header("Combat Facing")]
    [SerializeField] private float attackPointDistance = 0.75f;

    private Rigidbody2D rb;
    private AttackSystem attackSystem;
    private HealthSystem healthSystem;
    private PlayerAbilityController abilityController;

    private float horizontalInput;
    private float verticalInput;
    private bool isInLadderZone;
    private bool isOnLadder;
    private int facingDirection = 1;
    private bool isDashing;
    private bool isDead;
    private bool isGrounded;
    private bool jumpHeld;
    private bool hasDoubleJumped;
    private bool usedAirAttack;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private float dashTimeRemaining;
    private float lastDashTime = -Mathf.Infinity;
    private float defaultGravityScale;
    private bool isOnSlope;
    private bool isSliding;
    private Vector2 slopeNormal = Vector2.up;
    private float currentSlideSpeed;
    private float slideDelayTimer;
    private float slideGraceTimer;

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
        attackSystem.AttackStarted += HandleAttackStarted;
        attackSystem.AttackResolved += HandleAttackHit;
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
            attackSystem.AttackStarted -= HandleAttackStarted;
            attackSystem.AttackResolved -= HandleAttackHit;
        }
    }

    private void Update()
    {
        if (isDead || (GameManager.Instance != null && (GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver)))
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
        UpdateLadder();
    }

    private void FixedUpdate()
    {
        if (isDead || (GameManager.Instance != null && (GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver)))
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

        if (isOnLadder)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(0f, verticalInput * climbSpeed);
            return;
        }

        CheckSlope();
        UpdateSlideDelay();

        if (ShouldSlide())
        {
            ApplySlide();
        }
        else
        {
            isSliding = false;
            currentSlideSpeed = 0f;
            ApplyHorizontalMovement();
        }

        ApplyGravityModifiers();
        TryJump();
    }

    private void UpdateSlideDelay()
    {
        // Condiciones para que el delay corra (sin disparar slide todavía)
        bool waiting = isGrounded && isOnSlope && Mathf.Abs(horizontalInput) < 0.01f;

        if (waiting)
        {
            slideDelayTimer += Time.fixedDeltaTime;
        }
        else
        {
            slideDelayTimer = 0f;
        }
    }

    private void CheckSlope()
    {
        Vector2 basePos = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;

        // 3 raycasts: izquierda, centro, derecha. Nos quedamos con el de mayor ángulo (la rampa real).
        Vector2[] origins = {
            basePos + Vector2.left * slideCheckWidth,
            basePos,
            basePos + Vector2.right * slideCheckWidth
        };

        float bestAngle = 0f;
        Vector2 bestNormal = Vector2.up;
        bool anyHit = false;

        for (int i = 0; i < origins.Length; i++)
        {
            RaycastHit2D hit = Physics2D.Raycast(origins[i], Vector2.down, slideRayDistance, groundLayers);
            if (hit.collider == null) continue;

            anyHit = true;
            float angle = Vector2.Angle(hit.normal, Vector2.up);
            if (angle > bestAngle && angle < maxSlideAngle)
            {
                bestAngle = angle;
                bestNormal = hit.normal;
            }
        }

        if (anyHit && bestAngle > minSlideAngle)
        {
            slopeNormal = bestNormal;
            isOnSlope = true;
            slideGraceTimer = slideExitGrace; // recargar el grace cada vez que SÍ detectamos pendiente
        }
        else
        {
            // No detecta pendiente, pero si veníamos deslizando damos un margen
            if (slideGraceTimer > 0f)
            {
                slideGraceTimer -= Time.fixedDeltaTime;
                // mantenemos isOnSlope y slopeNormal del último frame válido
            }
            else
            {
                isOnSlope = false;
                slopeNormal = Vector2.up;
            }
        }
    }

    private bool ShouldSlide()
    {
        return isGrounded && isOnSlope && Mathf.Abs(horizontalInput) < 0.01f && slideDelayTimer >= slideStartDelay;
    }

    private void ApplySlide()
    {
        isSliding = true;

        // Vector tangente a la pendiente apuntando cuesta abajo
        Vector2 slopeDir = new Vector2(slopeNormal.y, -slopeNormal.x);
        if (slopeDir.y > 0f) slopeDir = -slopeDir;

        // Forzar al jugador a mirar hacia donde se desliza (cuesta abajo)
        if (Mathf.Abs(slopeDir.x) > 0.01f)
        {
            int slideFacing = slopeDir.x > 0f ? 1 : -1;
            if (facingDirection != slideFacing)
            {
                facingDirection = slideFacing;
                UpdateAttackPointPosition();
            }
        }

        currentSlideSpeed = Mathf.Min(currentSlideSpeed + slideAcceleration * Time.fixedDeltaTime, maxSlideSpeed);
        rb.linearVelocity = slopeDir * currentSlideSpeed;
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
            usedAirAttack = false;
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
            PerformJump(false);
            coyoteTimeCounter = 0f;
        }
        else if (canJumpDouble)
        {
            PerformJump(true);
            hasDoubleJumped = true;
        }
    }

    private void PerformJump(bool isDoubleJump)
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        jumpBufferCounter = 0f;

        if (isDoubleJump)
        {
            AudioManager.Instance?.PlayDoubleJump();
        }
        else
        {
            AudioManager.Instance?.PlayJump();
        }
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
        float combinedHorizontal = Mathf.Clamp(keyboardInput + gamepadInput, -1f, 1f);

        // Desmontarse de la escalera dando un paso al costado (única forma de salir,
        // ya que el salto está bloqueado en la escalera). Sirve para bajarse al llegar abajo.
        if (isOnLadder && Mathf.Abs(combinedHorizontal) > 0.5f)
        {
            ExitLadder();
        }

        horizontalInput = isOnLadder ? 0f : combinedHorizontal;

        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            facingDirection = horizontalInput > 0f ? 1 : -1;
            UpdateAttackPointPosition();
        }

        float verticalKeyboard = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)   verticalKeyboard += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) verticalKeyboard -= 1f;
        }
        float verticalGamepad = Gamepad.current != null ? Gamepad.current.leftStick.ReadValue().y : 0f;
        verticalInput = Mathf.Clamp(verticalKeyboard + (Mathf.Abs(verticalGamepad) > 0.2f ? verticalGamepad : 0f), -1f, 1f);
    }

    private void ReadJumpInput()
    {
        bool jumpPressed = (Keyboard.current != null &&
                           (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)) ||
                          (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);

        jumpHeld = (Keyboard.current != null &&
                   (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)) ||
                  (Gamepad.current != null && Gamepad.current.buttonNorth.isPressed);

        // No se puede saltar desde la escalera ni dentro de su zona (la tecla de salto
        // coincide con la de trepar y causa conflictos). El salto se ignora por completo.
        if (isOnLadder || isInLadderZone)
        {
            return;
        }

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

        if (attackPressed && (isGrounded || !usedAirAttack))
        {
            if (attackSystem.TryAttack() && !isGrounded)
                usedAirAttack = true;
        }
    }

    private void HandleAttackStarted()
    {
    }

    private void HandleAttackHit(bool critical, int damage, int targetsHit)
    {
        AudioManager.Instance?.PlayAttack(critical);
        ApplyKnockbackToNearbyEnemies();
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

    private void UpdateLadder()
    {
        if (!isInLadderZone)
        {
            if (isOnLadder) ExitLadder();
            return;
        }

        // Entrar a la escalera presionando arriba o abajo (pero no si se está dando
        // un paso al costado para desmontarse, así no se re-engancha al instante).
        if (!isOnLadder && Mathf.Abs(verticalInput) > 0.1f && Mathf.Abs(horizontalInput) < 0.5f)
        {
            isOnLadder = true;
            rb.gravityScale = 0f;
            jumpBufferCounter = 0f;
        }
    }

    private void ExitLadder()
    {
        isOnLadder = false;
        rb.gravityScale = defaultGravityScale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & ladderLayers) != 0)
            isInLadderZone = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & ladderLayers) != 0)
        {
            isInLadderZone = false;
            ExitLadder();
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
        AudioManager.Instance?.PlayDash();
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
        attackSystem.AttackPointTransform.localPosition = new Vector3(attackPointDistance, currentY, 0f);
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

    public bool IsOnLadder => isOnLadder;
    public float VerticalInput => verticalInput;
    public int FacingDirection => facingDirection;
    public Rigidbody2D Rigidbody => rb;
    public AttackSystem AttackSystem => attackSystem;
    public HealthSystem HealthSystem => healthSystem;
    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public bool IsSliding => isSliding;
    public float CurrentSlideSpeed => currentSlideSpeed;
    public float VerticalVelocity => rb != null ? rb.linearVelocity.y : 0f;
    public bool IsAttacking => attackSystem != null && attackSystem.IsAttacking;
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
