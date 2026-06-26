using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zona segura: un rango rectangular donde los enemigos normales (EnemyController) no pueden entrar.
/// Pensada para proteger un área, ej: la entrada a la tienda. NO afecta al player (puede entrar y salir)
/// ni a los jefes (usan sus propios controllers).
///
/// Cómo funciona: el EnemyController consulta las zonas activas y, si su próximo paso lo metería dentro,
/// le corta la velocidad horizontal, quedando frenado en el borde. Solo aplica si el enemigo está a la
/// altura de la zona (entre Bottom y Top), así enemigos en otro nivel de plataforma no se ven afectados.
///
/// Armado: poné este componente en un objeto vacío, ubicalo donde querés la zona y ajustá Size. El
/// gizmo verde muestra el área. No necesita collider.
/// </summary>
public class SafeZone : MonoBehaviour
{
    public static readonly List<SafeZone> All = new List<SafeZone>();

    [Tooltip("Tamaño del área protegida, en unidades de mundo.")]
    [SerializeField] private Vector2 size = new Vector2(4f, 4f);
    [Tooltip("Desplazamiento del centro del área respecto a la posición del objeto.")]
    [SerializeField] private Vector2 offset = Vector2.zero;

    public float Left => transform.position.x + offset.x - size.x * 0.5f;
    public float Right => transform.position.x + offset.x + size.x * 0.5f;
    public float Bottom => transform.position.y + offset.y - size.y * 0.5f;
    public float Top => transform.position.y + offset.y + size.y * 0.5f;

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    /// <summary>
    /// Devuelve la velocidad horizontal ajustada para que un enemigo en <paramref name="position"/> no
    /// entre a ninguna zona segura. Si su próximo paso cruzaría el borde, la corta a 0.
    /// </summary>
    /// <param name="buffer">Margen para frenarlo un poco antes del borde (ej: medio ancho del enemigo).</param>
    public static float RestrictHorizontalVelocity(Vector2 position, float velocityX, float buffer = 0.3f)
    {
        if (Mathf.Approximately(velocityX, 0f) || All.Count == 0)
        {
            return velocityX;
        }

        float dt = Time.fixedDeltaTime;
        float nextX = position.x + velocityX * dt;

        for (int i = 0; i < All.Count; i++)
        {
            SafeZone z = All[i];
            if (position.y < z.Bottom || position.y > z.Top)
            {
                continue; // el enemigo no está a la altura de la zona
            }

            // Viene desde la izquierda y entraría por el borde izquierdo.
            if (velocityX > 0f && position.x < z.Left && nextX > z.Left - buffer)
            {
                return 0f;
            }

            // Viene desde la derecha y entraría por el borde derecho.
            if (velocityX < 0f && position.x > z.Right && nextX < z.Right + buffer)
            {
                return 0f;
            }
        }

        return velocityX;
    }

    private void OnDrawGizmos()
    {
        Vector3 center = transform.position + (Vector3)offset;
        Vector3 boxSize = new Vector3(size.x, size.y, 0.1f);

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.18f);
        Gizmos.DrawCube(center, boxSize);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireCube(center, boxSize);
    }
}
