using System.Collections;
using UnityEngine;

/// <summary>
/// Punto de aparición identificado por <see cref="spawnId"/>. Al cargar una escena, si una
/// <see cref="SceneTransitionDoor"/> pidió aparecer en este id, el Player se reubica acá (en vez de
/// quedar en la posición por defecto de la escena). Sirve, por ejemplo, para volver de la Shop y
/// aparecer al lado de la puerta de la casita en vez de en el inicio del nivel.
///
/// Como la cámara es por rooms (<see cref="CameraController"/>), también hay que indicarle a qué
/// room saltar; si no, la cámara queda clavada en el room inicial aunque el Player esté en otro.
///
/// Uso: poné un GameObject vacío donde querés que aparezca el Player, agregale este componente,
/// ponele un <see cref="spawnId"/> (ej. "FromShop") y asignale el <see cref="room"/> que lo contiene.
/// En la puerta de salida configurá el mismo id en su campo targetSpawnId.
/// </summary>
public class SceneSpawnPoint : MonoBehaviour
{
    // Lo setea la puerta antes de cargar la escena destino; sobrevive al LoadScene por ser estático.
    public static string RequestedId;

    [Tooltip("Identificador de este punto. La puerta pide aparecer acá usando este mismo texto.")]
    [SerializeField] private string spawnId;
    [Tooltip("Room que contiene este punto. La cámara salta acá al aparecer. Si está vacío, la " +
             "cámara queda donde la deje su startingRoom.")]
    [SerializeField] private RoomBoundary room;

    private void Start()
    {
        if (string.IsNullOrEmpty(RequestedId) || RequestedId != spawnId) return;
        RequestedId = null; // se consume: que no reubique de nuevo en la próxima carga

        StartCoroutine(PlaceRoutine());
    }

    private IEnumerator PlaceRoutine()
    {
        // Inmediato para que no se vea el inicio del nivel...
        Place();
        // ...y de nuevo el frame siguiente, por si CameraController.Start corrió después y pisó el
        // room con su startingRoom (el orden de los Start no está garantizado).
        yield return null;
        Place();
    }

    private void Place()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null) return;

        player.transform.position = transform.position;

        if (room != null && CameraController.Instance != null)
        {
            CameraController.Instance.SnapToRoom(room);
        }
    }
}
