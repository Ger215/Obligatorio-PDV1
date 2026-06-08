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
        CameraController.PanBegan += HandlePanBegan;
    }

    private void OnDisable()
    {
        CameraController.RoomChanged -= HandleRoomChanged;
        CameraController.PanBegan -= HandlePanBegan;
    }

    // RoomChanged ocurre bajo el negro (FadeCut), al final del paneo (Pan) o en el arranque.
    private void HandleRoomChanged(RoomBoundary newRoom)
    {
        isOwned = newRoom == owningRoom;
        controller.ResumeAfterPan(); // limpia cualquier freeze de paneo y re-ancla sin saltos
        SetVisible(isOwned);
    }

    // Inicio de un paneo: el fondo entrante se muestra congelado en el destino y el saliente
    // se congela en su lugar, así la cámara los revela/oculta deslizándose, sin duplicar.
    private void HandlePanBegan(RoomBoundary destRoom, Vector3 destCameraPos)
    {
        if (owningRoom == destRoom)
        {
            SetVisible(true);
            controller.FreezeForPan(destCameraPos);
        }
        else if (isOwned)
        {
            controller.FreezeForPan();
        }
    }

    private void SetVisible(bool active)
    {
        foreach (SpriteRenderer sr in sprites)
            sr.enabled = active;
        controller.enabled = active;
    }
}
