using UnityEngine;

/// <summary>
/// Capa de teletransporte que se monta ENCIMA de un <see cref="EnemyController"/> caminador.
/// No hace daño por sí mismo: cada cierto tiempo reubica al enemigo DETRÁS del jugador para que
/// el walker lo ataque de melee desde la espalda. Los dos sistemas conviven:
///   - Fase visible: el <see cref="EnemyController"/> persigue, encara y pega melee normalmente.
///   - Teleport: se congela el walker (<see cref="EnemyController.SetStunned"/>), se reproduce
///     TeleportOut, se reubica detrás del jugador, se reproduce TeleportIn y se descongela.
///
/// El ritmo lo marcan ANIMATION EVENTS al final de cada clip (no hay que adivinar duraciones):
///   - Final de TeleportOut -> AnimEvent_TeleportOutComplete(): reubica detrás del jugador y entra a TeleportIn.
///   - Final de TeleportIn  -> AnimEvent_TeleportInComplete():  descongela al walker y reinicia el ciclo.
/// Estos métodos deben estar en el MISMO GameObject que el Animator (este componente lo está).
///
/// El facing NO se toca acá: lo maneja el <see cref="EnemyController"/> por rotación del root,
/// así que al reaparecer detrás del jugador encara solo, sin invertir el sprite.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(EnemyController))]
public class TeleporterController : MonoBehaviour
{
    [Header("Ritmo del teleport")]
    [Tooltip("Segundos que actúa como walker normal antes de reubicarse detrás del jugador. " +
             "(La duración de las animaciones la marcan los Animation Events, no este valor.)")]
    [SerializeField] private float visibleTime = 2.5f;

    [Tooltip("Distancia máxima al jugador para permitir el teleport. Si está más lejos, NO se " +
             "teletransporta (seguiría como walker normal) y reintenta más tarde. Evita que " +
             "aparezca detrás tuyo desde el otro lado del mapa.")]
    [SerializeField] private float teleportRange = 8f;

    [Header("Reaparición detrás del jugador")]
    [Tooltip("A qué distancia, por detrás del jugador, reaparece.")]
    [SerializeField] private float behindOffset = 1.2f;
    [Tooltip("Ajuste vertical respecto al jugador al reaparecer.")]
    [SerializeField] private float verticalOffset = 0f;

    private Animator animator;
    private EnemyController walker;
    private AttackSystem attackSystem;
    private HealthSystem healthSystem;
    private bool isDead;
    private bool teleporting;

    private static readonly int AnimTeleportOut = Animator.StringToHash("teleportOut");
    private static readonly int AnimTeleportIn = Animator.StringToHash("teleportIn");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        walker = GetComponent<EnemyController>();
        attackSystem = GetComponent<AttackSystem>();
        healthSystem = GetComponent<HealthSystem>();
    }

    private void OnEnable()
    {
        if (healthSystem != null)
        {
            healthSystem.Died += HandleDeath;
            healthSystem.Damaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (healthSystem != null)
        {
            healthSystem.Died -= HandleDeath;
            healthSystem.Damaged -= HandleDamaged;
        }
    }

    private void Start()
    {
        BeginVisiblePhase();
    }

    // Arranca el tiempo actuando como walker; al terminar, dispara el desvanecimiento.
    private void BeginVisiblePhase()
    {
        if (isDead) return;
        Invoke(nameof(TriggerTeleportOut), visibleTime);
    }

    private void TriggerTeleportOut()
    {
        if (isDead) return;

        PlayerController player = PlayerController.Instance;

        // Solo se teletransporta si el jugador está dentro de rango. Sin jugador o demasiado lejos,
        // no warpea (sigue como walker) y reintenta más tarde. Evita apariciones desde el otro lado del mapa.
        if (player == null ||
            Vector2.Distance(transform.position, player.transform.position) > teleportRange)
        {
            BeginVisiblePhase();
            return;
        }

        teleporting = true;
        // Cancelar cualquier ataque en curso: el teleportOut corta la animación de Attack y, si el
        // swing estaba antes de su frame de TriggerHit, la corrutina del AttackSystem quedaría colgada
        // en WindUp (IsAttacking = true para siempre) y no volvería a atacar nunca.
        attackSystem?.CancelAttack();
        walker.SetStunned(true); // congela movimiento/ataque mientras dura el teleport
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

    // Final de TeleportIn: ya materializado -> devolver el control al walker para que pegue melee.
    public void AnimEvent_TeleportInComplete()
    {
        if (isDead) return;
        teleporting = false;
        walker.SetStunned(false);
        BeginVisiblePhase();
    }

    // Si lo golpean en medio del teleport, el trigger "hit" (AnyState->Hurt) corta la animación
    // y los Animation Events no llegarían a disparar, dejando al walker congelado. Abortamos limpio:
    // descongelamos, limpiamos triggers y reiniciamos el ciclo visible.
    private void HandleDamaged(int current, int max)
    {
        if (isDead || !teleporting) return;
        teleporting = false;
        animator.ResetTrigger(AnimTeleportOut);
        animator.ResetTrigger(AnimTeleportIn);
        walker.SetStunned(false);
        CancelInvoke();
        BeginVisiblePhase();
    }

    private void RepositionBehindPlayer()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        int playerFacing = player.FacingDirection; // 1 der, -1 izq
        // Detrás = del lado opuesto al que mira el jugador.
        Vector3 behind = player.transform.position - new Vector3(playerFacing * behindOffset, -verticalOffset, 0f);
        transform.position = behind;

        // Encarar al jugador ya mismo. Sin esto, como el walker está stunned durante el TeleportIn,
        // su facing no se actualiza y el sprite se materializa mirando para el lado contrario.
        // No tocamos flipX: el EnemyController encara por rotación del root.
        walker.FaceTowards(player.transform.position.x);
    }

    private void HandleDeath(HealthSystem deadHealthSystem)
    {
        isDead = true;
        CancelInvoke();
    }
}
