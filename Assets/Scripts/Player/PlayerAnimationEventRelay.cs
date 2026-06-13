using UnityEngine;

/// <summary>
/// Va en el hijo "Graphics" (donde vive el Animator). Los Animation Events llaman métodos en
/// componentes del MISMO GameObject que el Animator; como el AttackSystem/PlayerAnimationDriver
/// están en el root, este relay reenvía el evento del frame de golpe hacia arriba.
/// </summary>
public class PlayerAnimationEventRelay : MonoBehaviour
{
    private AttackSystem attackSystem;

    private void Awake() => attackSystem = GetComponentInParent<AttackSystem>();

    // Llamado desde el Animation Event "OnAttackHitFrame" en la animación Attack.
    public void OnAttackHitFrame() => attackSystem?.TriggerHit();
}
