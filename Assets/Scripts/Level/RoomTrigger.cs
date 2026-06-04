using UnityEngine;

public class RoomTrigger : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private RoomBoundary targetRoom;
    [SerializeField] private float transitionDuration = 0.35f;

    [Header("One Way")]
    [SerializeField] private bool oneWay;

    [Header("Direction Override")]
    [Tooltip("Si está tildado, usa ManualDirection en vez de calcularla automáticamente.")]
    [SerializeField] private bool overrideDirection;
    [SerializeField] private Direction manualDirection = Direction.Down;

    private bool triggered;

    private enum Direction
    {
        Right,
        Left,
        Up,
        Down
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            Debug.Log($"[RoomTrigger:{name}] OnTriggerEnter2D — colisionó '{other.name}' pero NO tiene PlayerController.", this);
            return;
        }

        if (targetRoom == null)
        {
            Debug.LogWarning($"[RoomTrigger:{name}] targetRoom NO está asignado en el Inspector.", this);
            return;
        }

        if (triggered)
        {
            Debug.Log($"[RoomTrigger:{name}] Ya fue disparado (oneWay), ignorando.", this);
            return;
        }

        Direction requiredDirection = overrideDirection ? manualDirection : GetDirectionToTargetRoom();
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        Vector2 vel = rb != null ? rb.linearVelocity : Vector2.zero;
        bool movingCorrectly = IsPlayerMovingInDirection(player, requiredDirection);

        Debug.Log($"[RoomTrigger:{name}] Player entró. targetRoom={targetRoom.name}, requiredDir={requiredDirection}, velocity={vel}, movingCorrectly={movingCorrectly}, CameraController={(CameraController.Instance != null ? "OK" : "NULL")}, currentRoom={(CameraController.Instance?.CurrentRoom != null ? CameraController.Instance.CurrentRoom.name : "NULL")}", this);

        if (!movingCorrectly)
        {
            Debug.Log($"[RoomTrigger:{name}] Transición bloqueada — el player no se mueve en dirección {requiredDirection} (vel={vel}).", this);
            return;
        }

        CameraController camera = CameraController.Instance;
        if (camera != null && camera.CurrentRoom != targetRoom)
        {
            Debug.Log($"[RoomTrigger:{name}] Iniciando TransitionToRoom → {targetRoom.name}", this);
            camera.TransitionToRoom(targetRoom);
        }
        else
        {
            Debug.LogWarning($"[RoomTrigger:{name}] No se inicia transición — camera={(camera != null ? "OK" : "NULL")}, currentRoom ya es targetRoom={camera?.CurrentRoom == targetRoom}", this);
        }

        if (oneWay)
        {
            triggered = true;
        }
    }

    private Direction GetDirectionToTargetRoom()
    {
        Vector3 delta = targetRoom.transform.position - transform.position;
        float absX = Mathf.Abs(delta.x);
        float absY = Mathf.Abs(delta.y);

        if (absX >= absY)
        {
            return delta.x >= 0f ? Direction.Right : Direction.Left;
        }
        else
        {
            return delta.y >= 0f ? Direction.Up : Direction.Down;
        }
    }

    private bool IsPlayerMovingInDirection(PlayerController player, Direction direction)
    {
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        float velocityThreshold = 0.5f;

        Vector2 velocity = rb != null ? rb.linearVelocity : Vector2.zero;

        switch (direction)
        {
            case Direction.Right: return velocity.x > velocityThreshold;
            case Direction.Left: return velocity.x < -velocityThreshold;
            case Direction.Up: return velocity.y > velocityThreshold;
            case Direction.Down: return velocity.y < -velocityThreshold;
            default: return true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (targetRoom != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, targetRoom.transform.position);

            Direction dir = GetDirectionToTargetRoom();
            Vector3 arrowPos = transform.position;
            float arrowSize = 0.5f;

            Gizmos.color = Color.green;
            switch (dir)
            {
                case Direction.Right:
                    Gizmos.DrawRay(arrowPos, Vector3.right * arrowSize);
                    break;
                case Direction.Left:
                    Gizmos.DrawRay(arrowPos, Vector3.left * arrowSize);
                    break;
                case Direction.Up:
                    Gizmos.DrawRay(arrowPos, Vector3.up * arrowSize);
                    break;
                case Direction.Down:
                    Gizmos.DrawRay(arrowPos, Vector3.down * arrowSize);
                    break;
            }
        }
    }

    private void OnValidate()
    {
        transitionDuration = Mathf.Max(0.05f, transitionDuration);
    }
}