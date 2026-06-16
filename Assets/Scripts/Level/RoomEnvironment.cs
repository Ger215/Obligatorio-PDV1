using UnityEngine;

/// <summary>
/// Define la música y el tinte de ambiente de un room. Mismo patrón que RoomParallax:
/// se suscribe a CameraController.RoomChanged y, cuando el room activo es el suyo, aplica
/// su música (crossfade en AudioManager) y su color de ambiente (lerp en RoomAmbience).
/// </summary>
public class RoomEnvironment : MonoBehaviour
{
    [Tooltip("Room asociado. Si está vacío busca el RoomBoundary en este objeto o sus padres.")]
    [SerializeField] private RoomBoundary owningRoom;

    [Header("Música")]
    [Tooltip("Tema de este room. Null = no cambia la música al entrar.")]
    [SerializeField] private AudioClip music;

    [Header("Iluminación / Ambiente")]
    [Tooltip("Tinte de pantalla al entrar al room. Alpha 0 (clear) = sin tinte (superficie). " +
             "Ej. cueva: azul oscuro con alpha ~0.4.")]
    [SerializeField] private Color ambientTint = new Color(0f, 0f, 0f, 0f);

    [Tooltip("Intensidad del Global Light 2D en este room. 1 = normal (superficie). ~0.05 = cueva oscura.")]
    [SerializeField] private float globalLightIntensity = 1f;

    [Tooltip("Si true, enciende el Light2D del player al entrar (efecto linterna en la cueva).")]
    [SerializeField] private bool enablePlayerLight = false;

    [Tooltip("Duración del fade de música, tinte y luz al entrar.")]
    [SerializeField] private float fadeDuration = 0.6f;

    [Header("Tormenta (opcional)")]
    [Tooltip("Si se asigna, al entrar a este room arranca la tormenta de rayos y al salir se corta. " +
             "Dejar vacío en los rooms sin tormenta.")]
    [SerializeField] private LightningController stormLightning;

    private void Awake()
    {
        if (owningRoom == null) owningRoom = GetComponent<RoomBoundary>();
        if (owningRoom == null) owningRoom = GetComponentInParent<RoomBoundary>();
        if (owningRoom == null)
            Debug.LogWarning($"[RoomEnvironment:{name}] No encontré RoomBoundary en este objeto ni en los padres.");
    }

    private void OnEnable() => CameraController.RoomChanged += HandleRoomChanged;
    private void OnDisable() => CameraController.RoomChanged -= HandleRoomChanged;

    private void HandleRoomChanged(RoomBoundary newRoom)
    {
        // La tormenta se maneja antes del early-return: si entramos a este room arranca,
        // y cuando el room activo pasa a ser otro, este mismo RoomEnvironment la corta.
        if (stormLightning != null)
        {
            if (newRoom == owningRoom) stormLightning.BeginStorm();
            else stormLightning.StopStorm();
        }

        if (newRoom != owningRoom) return;

        if (music != null) AudioManager.Instance?.PlayMusic(music);
        RoomAmbience.Instance?.SetAmbient(ambientTint, fadeDuration);
        RoomLightingManager.Instance?.SetLighting(globalLightIntensity, enablePlayerLight, fadeDuration);
    }
}
