using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PixelArtScroller : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Velocidad a la que se mueve esta capa.")]
    public float scrollSpeed = 1f;

    private float singleTextureWidth;
    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
        // Calcula automáticamente el ancho exacto de una sola repetición del dibujo
        singleTextureWidth = GetComponent<SpriteRenderer>().sprite.bounds.size.x;
    }

    private void Update()
    {
        // Movemos el objeto hacia la izquierda
        transform.Translate(Vector3.left * scrollSpeed * Time.deltaTime);

        // Si ya se desplazó el ancho de un dibujo entero, lo volvemos a la posición inicial (bucle invisible)
        if (transform.position.x <= startPosition.x - singleTextureWidth)
        {
            transform.position = startPosition;
        }
    }
}