using UnityEngine;

/// <summary>
/// Trigger one-way de arena de boss. Cuando el Player entra: enciende un muro invisible (para que
/// no pueda volver) y tiñe la pantalla de rojo vía RoomAmbience. Pensado para colocarse en la
/// entrada de la sala del boss, junto al trigger que lo activa. Se dispara una sola vez.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossArenaLock : MonoBehaviour
{
    [Header("Muro de bloqueo")]
    [Tooltip("Muro invisible (un GameObject con Collider2D sólido en la capa del piso/paredes) " +
             "que se ENCIENDE al entrar para que el Player no pueda volver. Empieza desactivado.")]
    [SerializeField] private GameObject lockWall;

    [Header("Tinte rojo")]
    [Tooltip("Color del tinte al entrar a la arena. Rojo translúcido, ej. (0.5, 0, 0, 0.25). " +
             "Alpha = cuánto tapa. Reemplaza el tinte actual del room.")]
    [SerializeField] private Color redTint = new Color(0.5f, 0f, 0f, 0.25f);

    [Tooltip("Duración del fundido del tinte.")]
    [SerializeField] private float tintFadeDuration = 0.8f;

    private bool triggered;

    private void Awake()
    {
        // El muro arranca apagado; el player pasa de largo y recién al cruzar el trigger se cierra.
        if (lockWall != null) lockWall.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        // Detecta al Player por componente (no por tag), igual que RoomTrigger, para no depender
        // de que el GameObject esté tageado "Player".
        if (other.GetComponentInParent<PlayerController>() == null) return;

        triggered = true;

        if (lockWall != null) lockWall.SetActive(true);
        RoomAmbience.Instance?.SetAmbient(redTint, tintFadeDuration);

        // Ya cumplió su función; se apaga para no re-evaluar.
        gameObject.SetActive(false);
    }
}
