using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(AttackSystem))]
[RequireComponent(typeof(HealthSystem))]
public class PlayerAnimationDriver : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string groundedParameter = "Grounded";
    [SerializeField] private string verticalVelocityParameter = "VerticalVelocity";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string attackStateName = "Attack";
    [SerializeField] private string dashStateName = "Dash";
    [SerializeField] private string hurtStateName = "Hurt";

    [Header("Hit Flash")]
    [SerializeField] private float hitFlashDuration = 0.15f;
    [SerializeField] private float hurtStateDuration = 0.4f;
    [SerializeField] private string recoveryStateName = "Idle";

    private Animator animator;
    private PlayerController playerController;
    private AttackSystem attackSystem;
    private HealthSystem healthSystem;
    private SpriteRenderer spriteRenderer;
    private bool wasDashing;
    private bool wasGrounded;
    private Coroutine hurtExitCoroutine;
    private Coroutine blinkCoroutine;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
        attackSystem = GetComponent<AttackSystem>();
        healthSystem = GetComponent<HealthSystem>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (animatorController != null)
        {
            animator.runtimeAnimatorController = animatorController;
        }
    }

    private void OnEnable()
    {
        attackSystem.AttackStarted += HandleAttackStarted;
        attackSystem.AttackResolved += HandleAttackResolved;
        healthSystem.Damaged += HandleDamaged;
    }

    private void OnDisable()
    {
        attackSystem.AttackStarted -= HandleAttackStarted;
        attackSystem.AttackResolved -= HandleAttackResolved;
        healthSystem.Damaged -= HandleDamaged;
    }

    private void Update()
    {
        animator.SetFloat(speedParameter, Mathf.Abs(playerController.HorizontalInput));
        animator.SetBool(groundedParameter, playerController.IsGrounded);
        animator.SetFloat(verticalVelocityParameter, playerController.VerticalVelocity);

        bool grounded = playerController.IsGrounded;
        if (grounded && !wasGrounded)
        {
            animator.ResetTrigger(attackTrigger);
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
            {
                animator.CrossFade(recoveryStateName, 0.1f);
                attackSystem.CancelAttack();
            }
        }
        wasGrounded = grounded;

        if (!grounded && attackSystem.IsAttacking && !animator.IsInTransition(0))
        {
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
            {
                attackSystem.CancelAttack();
            }
        }

        if (playerController.IsDashing && !wasDashing)
        {
            animator.CrossFade(dashStateName, 0.05f);
        }

        wasDashing = playerController.IsDashing;

        if (playerController.FacingDirection != 0)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * playerController.FacingDirection;
            transform.localScale = scale;
        }
    }

    private void HandleAttackStarted()
    {
        animator.ResetTrigger(attackTrigger);
        animator.SetTrigger(attackTrigger);
    }

    // Llamado desde Animation Event en el frame del golpe
    public void OnAttackHitFrame()
    {
        attackSystem.TriggerHit();
    }

    private void HandleAttackResolved(bool critical, int damage, int targetsHit)
    {
    }

    private void HandleDamaged(int currentHealth, int maxHealth)
    {
        attackSystem.CancelAttack();

        if (!string.IsNullOrWhiteSpace(hurtStateName))
        {
            animator.CrossFade(hurtStateName, 0.05f);

            if (hurtExitCoroutine != null) StopCoroutine(hurtExitCoroutine);
            hurtExitCoroutine = StartCoroutine(ExitHurtState());
        }

        if (spriteRenderer != null)
        {
            if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
            blinkCoroutine = StartCoroutine(InvincibilityBlink());
        }
    }

    private System.Collections.IEnumerator ExitHurtState()
    {
        yield return new WaitForSeconds(hurtStateDuration);
        animator.CrossFade(recoveryStateName, 0.15f);
    }

    private System.Collections.IEnumerator InvincibilityBlink()
    {
        spriteRenderer.color = new Color(1f, 0.2f, 0.2f, 1f);
        yield return new WaitForSeconds(hitFlashDuration);

        bool showGhost = true;

        while (healthSystem.IsInvincible)
        {
            spriteRenderer.color = showGhost ? new Color(1f, 1f, 1f, 0f) : Color.white;
            showGhost = !showGhost;
            yield return new WaitForSeconds(0.2f);
        }

        spriteRenderer.color = Color.white;
        blinkCoroutine = null;
    }
}