using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class RoomBoundary : MonoBehaviour
{
    [Header("Room Settings")]
    [SerializeField] private string roomName = "Room";
    [SerializeField] private Vector2 roomSize = new Vector2(24f, 14f);

    [Header("Gizmos")]
    [SerializeField] private Color gizmoColor = new Color(0f, 1f, 0.5f, 0.3f);

    public string RoomName => roomName;

    public Bounds Bounds => new Bounds(transform.position, new Vector3(roomSize.x, roomSize.y, 1f));

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            col.compositeOperation = Collider2D.CompositeOperation.None;
        }

        UpdateColliderSize();
    }

    public void UpdateColliderSize()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.size = roomSize;
            box.offset = Vector2.zero;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Bounds b = Bounds;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(b.center, b.size);

        Color fillColor = gizmoColor;
        fillColor.a *= 0.15f;
        Gizmos.color = fillColor;
        Gizmos.DrawCube(b.center, b.size);
    }

    private void OnValidate()
    {
        roomSize.x = Mathf.Max(4f, roomSize.x);
        roomSize.y = Mathf.Max(4f, roomSize.y);
        UpdateColliderSize();
    }
}