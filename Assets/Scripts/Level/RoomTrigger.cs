using UnityEngine;

public class RoomTrigger : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private RoomBoundary targetRoom;
    [Tooltip("FadeCut: corte con fundido a negro (lugares distintos). Pan: paneo suave estilo Metroid (mismo bioma).")]
    [SerializeField] private TransitionStyle transitionStyle = TransitionStyle.FadeCut;

    [Header("Bidireccional (opcional)")]
    [Tooltip("Si se asigna, al moverse en la dirección OPUESTA el trigger manda a este room. " +
             "Útil para escaleras/pasajes que se cruzan en ambos sentidos. Dejar vacío para trigger de una sola dirección.")]
    [SerializeField] private RoomBoundary reverseRoom;

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
        EvaluateTransition(other);
    }

    // Se reevalúa cada frame mientras el player está DENTRO del trigger. Necesario para
    // escaleras/pasajes donde el player cambia de sentido (sube y baja) sin salir del collider:
    // OnTriggerEnter2D solo se dispara al entrar, así que no detectaría el cambio de dirección.
    private void OnTriggerStay2D(Collider2D other)
    {
        EvaluateTransition(other);
    }

    private void EvaluateTransition(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || targetRoom == null || triggered)
        {
            return;
        }

        Direction requiredDirection = overrideDirection ? manualDirection : GetDirectionToTargetRoom();

        // Resolver hacia qué room ir según el sentido del movimiento.
        RoomBoundary destination = null;
        if (IsPlayerMovingInDirection(player, requiredDirection))
        {
            destination = targetRoom;
        }
        else if (reverseRoom != null && IsPlayerMovingInDirection(player, Opposite(requiredDirection)))
        {
            destination = reverseRoom;
        }

        if (destination == null)
        {
            return;
        }

        CameraController camera = CameraController.Instance;
        if (camera == null || camera.CurrentRoom == destination)
        {
            return;
        }

        Debug.Log($"[RoomTrigger:{name}] TransitionToRoom → {destination.name} ({transitionStyle})", this);
        camera.TransitionToRoom(destination, transitionStyle);

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

    private static Direction Opposite(Direction direction)
    {
        switch (direction)
        {
            case Direction.Right: return Direction.Left;
            case Direction.Left: return Direction.Right;
            case Direction.Up: return Direction.Down;
            default: return Direction.Up;
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

}