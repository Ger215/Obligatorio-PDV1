using UnityEngine;

/// <summary>
/// Scroll infinito por desplazamiento físico, para pisos hechos con Tilemap (sprites 16x16) o cualquier
/// conjunto de objetos que no se pueda scrollear por UV. Mueve sus segmentos hijos hacia un lado a
/// velocidad constante y, cuando un segmento sale de pantalla por la izquierda, lo recicla al final de
/// la fila. Pensado para la escena "Loading" donde la cámara no se mueve y el personaje corre en el lugar.
///
/// Cómo armarlo:
///  - Pintá un TRAMO de piso con tu Tilemap (suficientemente ancho).
///  - Duplicá ese GameObject 2 o 3 veces y ponelos pegados uno al lado del otro en X (sin huecos ni
///    solapes), cubriendo más que el ancho de la cámara.
///  - Metelos como HIJOS de un objeto vacío (ej: "FloorScroller") y poné este componente en ese padre.
///
/// El ancho de segmento se calcula solo desde el bounds del Renderer del primer hijo (el TilemapRenderer
/// sirve). Si te queda mal, seteá segmentWidth a mano con el ancho en unidades de mundo de un tramo.
/// </summary>
public class TileStripScroller : MonoBehaviour
{
    [Tooltip("Velocidad de scroll en unidades/segundo. Positivo = se mueve a la izquierda (el personaje " +
             "'avanza' a la derecha). Negativo invierte el sentido.")]
    [SerializeField] private float speed = 4f;

    [Tooltip("Si está vacío, usa los hijos directos de este objeto, de izquierda a derecha.")]
    [SerializeField] private Transform[] segments;

    [Tooltip("Ancho de cada segmento en unidades de mundo. 0 = se calcula solo desde el Renderer del primer hijo.")]
    [SerializeField] private float segmentWidth = 0f;

    [Tooltip("Margen extra (en unidades) más allá del borde de cámara antes de reciclar un segmento. " +
             "Subilo si ves que un segmento desaparece estando todavía visible.")]
    [SerializeField] private float recycleMargin = 1f;

    private float leftEdgeX;
    private float wrapDistance;
    private bool initialized;

    private void Start()
    {
        if (segments == null || segments.Length == 0)
        {
            CollectChildren();
        }

        if (segments == null || segments.Length == 0)
        {
            enabled = false;
            return;
        }

        if (segmentWidth <= 0f)
        {
            segmentWidth = MeasureWidth(segments[0]);
        }

        if (segmentWidth <= 0f)
        {
            // No pudimos medir: sin ancho no podemos reciclar bien.
            enabled = false;
            return;
        }

        wrapDistance = segmentWidth * segments.Length;

        Camera cam = Camera.main;
        if (cam != null)
        {
            float camHalfWidth = cam.orthographicSize * cam.aspect;
            leftEdgeX = cam.transform.position.x - camHalfWidth - recycleMargin - segmentWidth;
        }
        else
        {
            leftEdgeX = GetLeftMostX() - segmentWidth;
        }

        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        float step = speed * Time.deltaTime;

        foreach (Transform seg in segments)
        {
            if (seg == null)
            {
                continue;
            }

            seg.position += Vector3.left * step;

            if (seg.position.x < leftEdgeX)
            {
                Vector3 pos = seg.position;
                pos.x += wrapDistance;
                seg.position = pos;
            }
        }
    }

    private void CollectChildren()
    {
        int count = transform.childCount;
        segments = new Transform[count];
        for (int i = 0; i < count; i++)
        {
            segments[i] = transform.GetChild(i);
        }
    }

    private float MeasureWidth(Transform seg)
    {
        if (seg == null)
        {
            return 0f;
        }

        Renderer r = seg.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.size.x : 0f;
    }

    private float GetLeftMostX()
    {
        float min = float.MaxValue;
        foreach (Transform seg in segments)
        {
            if (seg != null)
            {
                min = Mathf.Min(min, seg.position.x);
            }
        }
        return min == float.MaxValue ? 0f : min;
    }
}
