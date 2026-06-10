using UnityEngine;

/// <summary>
/// Prop destructible (cajas, barriles, etc.) que otorga XP al ser destruido por el arma del jugador.
/// Se espera que el sistema de combate llame al método público <see cref="TakeDamage"/> cuando
/// el collider del arma haga contacto con el trigger de este prop.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DestructibleProp : MonoBehaviour
{
    [Header("Recompensa")]
    [Tooltip("Cantidad de XP que otorga este prop al ser destruido.")]
    [SerializeField] private int experienceReward = 10;

    [Header("Daño")]
    [Tooltip("Puntos de vida del prop. Se destruye cuando llega a 0.")]
    [SerializeField] private int hitPoints = 1;

    [Header("Gamefeel - Audio")]
    [Tooltip("Clip que se reproducirá al destruir el prop. Se usa PlayClipAtPoint para que no se corte.")]
    [SerializeField] private AudioClip destructionClip;

    [Tooltip("Volumen del clip de destrucción.")]
    [Range(0f, 1f)]
    [SerializeField] private float destructionVolume = 1f;

    [Header("Gamefeel - VFX")]
    [Tooltip("Prefab de partículas que se instancia en la posición del prop al destruirse.")]
    [SerializeField] private GameObject destructionVFXPrefab;

    [Tooltip("Tiempo (en segundos) tras el cual se destruirá el VFX instanciado.")]
    [SerializeField] private float vfxLifetime = 2f;

    // Evita que se ejecute la rutina de destrucción más de una vez en el mismo frame.
    private bool isBeingDestroyed;

    /// <summary>
    /// Aplica daño al prop. Debe ser llamado por el sistema de combate del jugador.
    /// </summary>
    /// <param name="damage">Cantidad de daño recibida.</param>
    public void TakeDamage(int damage = 1)
    {
        if (isBeingDestroyed) return;

        hitPoints -= damage;

        if (hitPoints <= 0)
        {
            DestroyProp();
        }
    }

    /// <summary>
    /// Ejecuta la rutina completa de destrucción: VFX, audio, recompensa de XP y destrucción del GameObject.
    /// </summary>
    private void DestroyProp()
    {
        isBeingDestroyed = true;

        // 1. Instanciamos el VFX en la posición actual.
        if (destructionVFXPrefab != null)
        {
            GameObject vfxInstance = Instantiate(destructionVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfxInstance, vfxLifetime);
        }

        // 2. Reproducimos el sonido con PlayClipAtPoint para que sobreviva a la destrucción del GameObject.
        if (destructionClip != null)
        {
            AudioSource.PlayClipAtPoint(destructionClip, transform.position, destructionVolume);
        }

        // 3. Otorgamos la XP al jugador.
        // TODO: Conectar con el ExperienceSystem real cuando esté disponible el GameManager.
        Debug.Log($"Add {experienceReward} XP");

        // 4. Destruimos el GameObject del prop.
        Destroy(gameObject);
    }
}
