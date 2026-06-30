using UnityEngine;

public class InteriorParallax : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Arrastrá acá a tu Player desde la Hierarchy")]
    public Transform player;
    
    [Tooltip("Qué tanto se mueve. Usar valores negativos pequeños (ej: -0.1)")]
    public float parallaxEffect = -0.1f;

    private Vector3 startPosition;
    private float startPlayerX;

    void Start()
    {
        startPosition = transform.position;
        
        if (player != null)
        {
            startPlayerX = player.position.x;
        }
    }

    void Update()
    {
        if (player != null)
        {
            // Calculamos la distancia que caminó la guerrera desde el inicio
            float distance = (player.position.x - startPlayerX) * parallaxEffect;
            
            // Movemos la capa del fondo suavemente
            transform.position = new Vector3(startPosition.x + distance, transform.position.y, transform.position.z);
        }
    }
}