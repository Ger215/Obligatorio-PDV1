using UnityEngine;

/// <summary>
/// Asocia un ParallaxController a un RoomBoundary específico.
/// Se activa cuando el player entra al room y se desactiva al salir.
/// Poner en el GameObject del WorldBackground hijo de cada RoomBoundary.
/// </summary>
[RequireComponent(typeof(ParallaxController))]
public class RoomParallax : MonoBehaviour
{
    [Tooltip("Room asociado. Si está vacío busca el RoomBoundary en los padres.")]
    [SerializeField] private RoomBoundary owningRoom;

    private ParallaxController controller;

    private void Awake()
    {
        controller = GetComponent<ParallaxController>();
        if (owningRoom == null) owningRoom = GetComponentInParent<RoomBoundary>();
        if (owningRoom == null)
        {
            Debug.LogWarning($"[RoomParallax:{name}] No encontré RoomBoundary en los padres.");
            return;
        }
        // Arranca apagado; se activa cuando el CameraController lo pide
        SetActive(false);
    }

    private void OnEnable()
    {
        CameraController.RoomChanged += HandleRoomChanged;
    }

    private void OnDisable()
    {
        CameraController.RoomChanged -= HandleRoomChanged;
    }

    private void HandleRoomChanged(RoomBoundary newRoom)
    {
        SetActive(newRoom == owningRoom);
    }

    private void SetActive(bool active)
    {
        controller.enabled = active;
        // También apagamos los renderers para que no se vean cuando no es el room activo
        Renderer[] rs = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++) rs[i].enabled = active;
    }
}
