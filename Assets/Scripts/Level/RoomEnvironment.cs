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
        if (newRoom != owningRoom) return;

        if (music != null) AudioManager.Instance?.PlayMusic(music);
        RoomAmbience.Instance?.SetAmbient(ambientTint, fadeDuration);
        RoomLightingManager.Instance?.SetLighting(globalLightIntensity, enablePlayerLight, fadeDuration);
    }
}
