using UnityEngine;

/// <summary>
/// Enemigo teletransportador. Cicla: queda visible un rato, se desvanece (TeleportOut), reaparece
/// DETRÁS del jugador (TeleportIn) y lo ataca por la espalda. Maneja el Animator
/// (Enemy_Teleporter.controller) con los triggers "teleportOut"/"teleportIn".
///
/// El ritmo lo marcan ANIMATION EVENTS al final de cada clip (así no hay que adivinar la duración):
///   - Final de TeleportOut -> AnimEvent_TeleportOutComplete(): reubica detrás del jugador y entra a TeleportIn.
///   - Final de TeleportIn  -> AnimEvent_TeleportInComplete():  ataca por la espalda y reinicia el ciclo visible.
/// Estos métodos deben estar en el MISMO GameObject que el Animator (este componente lo está).
///
/// El daño se hace con el <see cref="AttackSystem"/> del propio enemigo. Si hay un
/// <see cref="EnemyController"/> caminador en el mismo objeto, asignalo en
/// <see cref="walkerToDisable"/> para apagarlo (este enemigo se mueve solo por teleport).
/// </summary>
[RequireComponent(typeof(Animator))]
public class TeleporterController : MonoBehaviour
{
    [Header("Ritmo del teleport")]
    [Tooltip("Segundos que se queda visible antes de desvanecerse. (La duración de las animaciones " +
             "la marcan los Animation Events, no este valor.)")]
    [SerializeField] private float visibleTime = 2.5f;

    [Header("Reaparición detrás del jugador")]
    [Tooltip("A qué distancia, por detrás del jugador, reaparece.")]
    [SerializeField] private float behindOffset = 1.2f;
    [Tooltip("Ajuste vertical respecto al jugador al reaparecer.")]
    [SerializeField] private float verticalOffset = 0f;

    [Header("Ataque por la espalda")]
    [Tooltip("Radio del golpe al reaparecer.")]
    [SerializeField] private float attackRadius = 1.2f;
    [Tooltip("Daño del golpe.")]
    [SerializeField] private int attackDamage = 8;
    [Tooltip("Capas a las que pega (seleccioná la capa del Player).")]
    [SerializeField] private LayerMask playerLayers;

    [Header("Refs")]
    [Tooltip("EnemyController caminador a apagar al iniciar (opcional).")]
    [SerializeField] private EnemyController walkerToDisable;

    private Animator animator;
    private AttackSystem attackSystem;
    private HealthSystem healthSystem;
    private SpriteRenderer spriteRenderer;
    private bool isDead;

    private static readonly int AnimTeleportOut = Animator.StringToHash("teleportOut");
    private static readonly int AnimTeleportIn = Animator.StringToHash("teleportIn");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        attackSystem = GetComponent<AttackSystem>();
        healthSystem = GetComponent<HealthSystem>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (walkerToDisable != null)
        {
            walkerToDisable.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (healthSystem != null) healthSystem.Died += HandleDeath;
    }

    private void OnDisable()
    {
        if (healthSystem != null) healthSystem.Died -= HandleDeath;
    }

    private void Start()
    {
        BeginVisiblePhase();
    }

    // Arranca el tiempo visible; al terminar, dispara el desvanecimiento.
    private void BeginVisiblePhase()
    {
        if (isDead) return;
        Invoke(nameof(TriggerTeleportOut), visibleTime);
    }

    private void TriggerTeleportOut()
    {
        if (isDead) return;
        animator.SetTrigger(AnimTeleportOut);
    }

    // --- Animation Events (llamados desde los clips) ---

    // Final de TeleportOut: ya invisible -> reubicar detrás del jugador y reaparecer.
    public void AnimEvent_TeleportOutComplete()
    {
        if (isDead) return;
        RepositionBehindPlayer();
        animator.SetTrigger(AnimTeleportIn);
    }

    // Final de TeleportIn: ya materializado -> golpe por la espalda y reiniciar ciclo.
    public void AnimEvent_TeleportInComplete()
    {
        if (isDead) return;
        AttackFromBehind();
        BeginVisiblePhase();
    }

    private void RepositionBehindPlayer()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        int playerFacing = player.FacingDirection; // 1 der, -1 izq
        Vector3 behind = player.transform.position - new Vector3(playerFacing * behindOffset, -verticalOffset, 0f);
        transform.position = behind;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = playerFacing > 0;
        }
    }

    private void AttackFromBehind()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        if (attackSystem != null)
        {
            attackSystem.DealAreaDamage(player.transform.position, attackRadius, playerLayers, attackDamage, false);
        }
        else
        {
            Debug.LogWarning("[TeleporterController] No hay AttackSystem; no se aplicó daño.", this);
        }
    }

    private void HandleDeath(HealthSystem deadHealthSystem)
    {
        isDead = true;
        CancelInvoke();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.8f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}
