using UnityEngine;

/// <summary>
/// Cartelito de la ruleta: apenas el Player pasa por arriba del ícono se despliegan solas las
/// instrucciones (no hay que apretar nada) y se ocultan al alejarse. El texto se arma a partir de
/// las teclas reales de la <see cref="AbilityRoulette"/> para que siempre coincida con la config.
///
/// Uso: poné este componente en un GameObject sobre el ícono de la ruleta con un Collider2D en modo
/// trigger (la zona donde aparece el cartel) y asigná la referencia a la ruleta.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RouletteInfoSign : MonoBehaviour
{
    [Tooltip("Ruleta de la que se leen las teclas y el costo para armar las instrucciones.")]
    [SerializeField] private AbilityRoulette roulette;
    [Tooltip("Si querés un texto fijo en vez del autogenerado, escribilo acá.")]
    [TextArea(2, 5)]
    [SerializeField] private string customMessage;

    private bool playerInRange;

    private void Reset() => GetComponent<Collider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        playerInRange = true;
        SignUI.Instance?.Show(BuildMessage());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        playerInRange = false;
        SignUI.Instance?.Hide();
    }

    private void OnDisable()
    {
        if (playerInRange) SignUI.Instance?.Hide();
        playerInRange = false;
    }

    private string BuildMessage()
    {
        if (!string.IsNullOrWhiteSpace(customMessage)) return customMessage;
        if (roulette == null) return "Ruleta de habilidades.";

        return $"Ruleta de habilidades\n" +
               $"Apretá {roulette.ToggleKey} para abrir/cerrar.\n" +
               $"Apretá {roulette.SpinKey} para girar ({roulette.SpinCost} XP por giro).";
    }
}
