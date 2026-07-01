using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo teletransportador que COEXISTE con un EnemyController (walker): el walker camina,
/// encara y ataca melee normal; este componente le suma un teleport periódico detrás del jugador.
///
/// El ritmo lo maneja una COROUTINE por tiempo (no Animation Events), así los triggers del walker
/// (attack/hit/isWalking) que interrumpen el Animator NO rompen la secuencia: la reubicación y el
/// unstun siempre se completan. Durante cada teleport se aturde al walker para que no pelee con la
/// reubicación; el facing lo maneja el walker (rotación), no este script.
/// </summary>
[RequireComponent(typeof(Animator))]
public class TeleporterController : MonoBehaviour
{
    [Header("Ritmo del teleport")]
    [Tooltip("Segundos visible antes de desvanecerse.")]
    [SerializeField] private float visibleTime = 2.5f;
    [Tooltip("Duración de la animación TeleportOut (desvanecerse). Ponela igual al largo de tu clip.")]
    [SerializeField] private float teleportOutDuration = 1f;
    [Tooltip("Duración de la animación TeleportIn (aparecer). Ponela igual al largo de tu clip.")]
    [SerializeField] private float teleportInDuration = 1f;

    [Header("Reaparición detrás del jugador")]
    [SerializeField] private float behindOffset = 1.2f;
    [SerializeField] private float verticalOffset = 0f;
    [Tooltip("Solo se usa si NO hay walker asignado (el walker maneja el facing por rotación).")]
    [SerializeField] private bool spriteFacesLeft = false;

    [Header("Ataque por la espalda")]
    [Tooltip("Si está apagado, NO golpea al reaparecer (el ataque lo hace el walker melee).")]
    [SerializeField] private bool attackOnReappear = false;
    [SerializeField] private float attackRadius = 1.2f;
    [SerializeField] private int attackDamage = 8;
    [SerializeField] private LayerMask playerLayers;

    [Header("Activación")]
    [Tooltip("Solo empieza a teleportarse cuando el jugador entra en este radio.")]
    [SerializeField] private float activationRange = 8f;

    [Header("Refs")]
    [Tooltip("EnemyController (walker) que coexiste. Camina/encara/ataca melee normal; se lo aturde " +
             "solo durante cada teleport. Si lo asignás, el facing lo maneja él (rotación).")]
    [SerializeField] private EnemyController walkerToDisable;

    private Animator animator;
    private AttackSystem attackSystem;
    private HealthSystem healthSystem;
    private SpriteRenderer spriteRenderer;
    private bool isDead;
    private bool activated;
    private int lastPlayerFacing = 1;

    private static readonly int AnimTeleportOut = Animator.StringToHash("teleportOut");
    private static readonly int AnimTeleportIn = Animator.StringToHash("teleportIn");
    private static readonly int AnimDie = Animator.StringToHash("die");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        attackSystem = GetComponent<AttackSystem>();
        healthSystem = GetComponent<HealthSystem>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        // El walker NO se apaga: coexiste. Se aturde solo durante cada teleport.
    }

    private void OnEnable()
    {
        if (healthSystem != null) healthSystem.Died += HandleDeath;
    }

    private void OnDisable()
    {
        if (healthSystem != null) healthSystem.Died -= HandleDeath;
    }

    // Espera a que el jugador entre en activationRange y arranca el ciclo de teleport.
    private void Update()
    {
        if (activated || isDead) return;

        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        if (Vector2.Distance(transform.position, player.transform.position) <= activationRange)
        {
            activated = true;
            StartCoroutine(TeleportLoop());
        }
    }

    private IEnumerator TeleportLoop()
    {
        while (!isDead)
        {
            // Fase visible: el walker camina/ataca normal.
            yield return new WaitForSeconds(visibleTime);
            if (isDead) break;

            // Teleport OUT: aturdir al walker y desvanecerse.
            if (walkerToDisable != null) walkerToDisable.SetStunned(true);
            animator.SetTrigger(AnimTeleportOut);
            yield return new WaitForSeconds(teleportOutDuration);
            if (isDead) break;

            // Reubicar detrás del jugador (ya invisible) y aparecer.
            RepositionBehindPlayer();
            animator.SetTrigger(AnimTeleportIn);
            yield return new WaitForSeconds(teleportInDuration);

            // Ya materializado: (opcional) golpe, y SIEMPRE soltar al walker para que retome.
            if (!isDead && attackOnReappear) AttackFromBehind();
            if (walkerToDisable != null) walkerToDisable.SetStunned(false);
        }

        // Seguridad: si murió a mitad de teleport, no dejar al walker aturdido.
        if (walkerToDisable != null) walkerToDisable.SetStunned(false);
    }

    private void RepositionBehindPlayer()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        // FacingDirection puede ser 0 (player quieto): usamos el último facing válido.
        int playerFacing = player.FacingDirection;
        if (playerFacing == 0) playerFacing = lastPlayerFacing;
        else lastPlayerFacing = playerFacing;

        transform.position = player.transform.position
            - new Vector3(playerFacing * behindOffset, -verticalOffset, 0f);

        // Si hay walker, ÉL encara (rotación). Solo tocamos flipX si va solo.
        if (walkerToDisable == null && spriteRenderer != null)
        {
            bool wantFaceRight = playerFacing > 0;
            spriteRenderer.flipX = spriteFacesLeft ? wantFaceRight : !wantFaceRight;
        }
    }

    private void AttackFromBehind()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || attackSystem == null) return;
        attackSystem.DealAreaDamage(player.transform.position, attackRadius, playerLayers, attackDamage, false);
    }

    // Los clips de TeleportIn/Out pueden tener Animation Events apuntando a estos métodos.
    // Los dejamos vacíos a propósito: el ritmo ahora lo maneja la coroutine por tiempo.
    public void AnimEvent_TeleportOutComplete() { }
    public void AnimEvent_TeleportInComplete() { }

    private void HandleDeath(HealthSystem deadHealthSystem)
    {
        isDead = true;
        StopAllCoroutines();
        if (walkerToDisable != null) walkerToDisable.SetStunned(false);
        if (animator != null) animator.SetTrigger(AnimDie);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.8f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, attackRadius);
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, activationRange);
    }
}
