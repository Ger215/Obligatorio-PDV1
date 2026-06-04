using UnityEngine;

/// <summary>
/// Asocia un ParallaxController a un RoomBoundary específico.
/// El swap de fondo ocurre en CameraController.RoomChanged, que sucede bajo el negro del fundido.
/// </summary>
[RequireComponent(typeof(ParallaxController))]
public class RoomParallax : MonoBehaviour
{
    [Tooltip("Room asociado. Si está vacío busca el RoomBoundary en los padres.")]
    [SerializeField] private RoomBoundary owningRoom;

    private ParallaxController controller;
    private SpriteRenderer[] sprites;
    private bool isOwned;

    private void Awake()
    {
        controller = GetComponent<ParallaxController>();
        sprites = GetComponentsInChildren<SpriteRenderer>(true);

        if (owningRoom == null) owningRoom = GetComponentInParent<RoomBoundary>();
        if (owningRoom == null)
        {
            Debug.LogWarning($"[RoomParallax:{name}] No encontré RoomBoundary en los padres.");
            return;
        }

        SetVisible(false);
    }

    private void OnEnable()
    {
        CameraController.RoomChanged += HandleRoomChanged;
    }

    private void OnDisable()
    {
        CameraController.RoomChanged -= HandleRoomChanged;
    }

    // RoomChanged ocurre bajo el negro (o en el arranque): swap instantáneo, invisible para el jugador.
    private void HandleRoomChanged(RoomBoundary newRoom)
    {
        bool shouldBeActive = newRoom == owningRoom;
        if (shouldBeActive == isOwned) return;
        isOwned = shouldBeActive;
        SetVisible(isOwned);
    }

    private void SetVisible(bool active)
    {
        foreach (SpriteRenderer sr in sprites)
            sr.enabled = active;
        controller.enabled = active;
    }
}
