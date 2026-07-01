using UnityEngine;

/// <summary>
/// Zona por room que prende o corta la lluvia global (<see cref="RainEffect"/>) según dónde esté
/// el jugador. Poné un Collider2D con <c>Is Trigger</c> cubriendo el room y asigná el objeto Rain.
///
/// - Para un room SECO en un nivel que por defecto llueve: dejá <see cref="rainWhileInside"/> en
///   false. Al entrar corta la lluvia, al salir la vuelve a prender.
/// - Para un room CON lluvia en un nivel por defecto seco: poné <see cref="rainWhileInside"/> en
///   true (y arrancá el objeto Rain con la lluvia cortada).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RainZone : MonoBehaviour
{
    [Tooltip("La lluvia global de la escena (objeto 'Rain' con el componente RainEffect).")]
    [SerializeField] private RainEffect rain;

    [Tooltip("Si llueve mientras el jugador está DENTRO de la zona. Al salir se aplica lo contrario. " +
             "false = room seco (default llueve). true = room con lluvia (default seco).")]
    [SerializeField] private bool rainWhileInside = false;

    private void Reset()
    {
        // Al agregar el componente en el editor, dejar el collider como trigger.
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (rain == null || other.GetComponentInParent<PlayerController>() == null) return;
        rain.SetRaining(rainWhileInside);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (rain == null || other.GetComponentInParent<PlayerController>() == null) return;
        rain.SetRaining(!rainWhileInside);
    }
}
