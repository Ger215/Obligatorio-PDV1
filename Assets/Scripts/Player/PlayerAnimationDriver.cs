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
    [SerializeField] private string dashStateName = "Dash";
    [SerializeField] private string hurtStateName = "Hurt";

    private Animator animator;
    private PlayerController playerController;
    private AttackSystem attackSystem;
    private HealthSystem healthSystem;
    private bool wasDashing;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
        attackSystem = GetComponent<AttackSystem>();
        healthSystem = GetComponent<HealthSystem>();

        if (animatorController != null)
        {
            animator.runtimeAnimatorController = animatorController;
        }
    }

    private void OnEnable()
    {
        attackSystem.AttackResolved += HandleAttackResolved;
        healthSystem.Damaged += HandleDamaged;
    }

    private void OnDisable()
    {
        attackSystem.AttackResolved -= HandleAttackResolved;
        healthSystem.Damaged -= HandleDamaged;
    }

    private void Update()
    {
        animator.SetFloat(speedParameter, Mathf.Abs(playerController.HorizontalInput));
        animator.SetBool(groundedParameter, playerController.IsGrounded);
        animator.SetFloat(verticalVelocityParameter, playerController.VerticalVelocity);

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

    private void HandleAttackResolved(bool critical, int damage, int targetsHit)
    {
        animator.SetTrigger(attackTrigger);
    }

    private void HandleDamaged(int currentHealth, int maxHealth)
    {
        if (!string.IsNullOrWhiteSpace(hurtStateName))
        {
            animator.CrossFade(hurtStateName, 0.05f);
        }
    }
}
