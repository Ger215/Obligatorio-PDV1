using UnityEngine;

/// <summary>
/// Activa/desactiva un grupo de enemigos pre-colocados según el room activo.
/// Coloca este componente en un GameObject hijo del RoomBoundary, con los enemigos
/// como hijos (o asignados manualmente en la lista). Se suscribe a CameraController.RoomChanged.
/// </summary>
public class EnemyRoomActivator : MonoBehaviour
{
    [Tooltip("Room asociado. Si está vacío busca el RoomBoundary en los padres.")]
    [SerializeField] private RoomBoundary owningRoom;

    [Tooltip("Lista de enemigos a activar/desactivar. Si está vacía, usa todos los hijos directos.")]
    [SerializeField] private GameObject[] enemies;

    [Tooltip("Si true, los enemigos arrancan desactivados (recomendado).")]
    [SerializeField] private bool startInactive = true;

    private void Awake()
    {
        if (owningRoom == null) owningRoom = GetComponentInParent<RoomBoundary>();

        if (owningRoom == null)
        {
            Debug.LogWarning($"[EnemyRoomActivator:{name}] No encontré RoomBoundary en los padres.");
            return;
        }

        // Si no se asignó nada en el inspector, usar los hijos directos
        if (enemies == null || enemies.Length == 0)
        {
            int count = transform.childCount;
            enemies = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                enemies[i] = transform.GetChild(i).gameObject;
            }
        }

        if (startInactive)
        {
            SetEnemiesActive(false);
        }
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
        SetEnemiesActive(newRoom == owningRoom);
    }

    private void SetEnemiesActive(bool active)
    {
        if (enemies == null) return;

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null) enemies[i].SetActive(active);
        }
    }
}
