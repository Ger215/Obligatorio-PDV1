using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ParallaxOffset : MonoBehaviour
{
    [Tooltip("Cuánto se desplaza el offset por unidad de movimiento del player. " +
             "Valores típicos: 0.01 (cielo lejano) a 0.5 (capa cercana).")]
    [Range(0f, 1f)]
    public float parallaxMultiplier = 0.05f;

    [Tooltip("Si está vacío busca el GameObject con tag 'Player'.")]
    [SerializeField] private Transform player;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Material parallaxMaterial;
    private float lastPlayerX;

    private void Start()
    {
        // .material crea una instancia única para este renderer (OK en runtime)
        parallaxMaterial = GetComponent<Renderer>().material;

        if (player == null)
        {
            GameObject pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo != null) player = pgo.transform;
        }

        if (player != null) lastPlayerX = player.position.x;
    }

    private void LateUpdate()
    {
        if (player == null || parallaxMaterial == null) return;

        float deltaX = player.position.x - lastPlayerX;
        parallaxMaterial.mainTextureOffset += new Vector2(deltaX * parallaxMultiplier, 0f);
        lastPlayerX = player.position.x;

        if (debugLogs)
            Debug.Log($"[Parallax:{name}] deltaX={deltaX:F3} offset={parallaxMaterial.mainTextureOffset}");
    }

    private void OnDisable()
    {
        // reset al desactivar para no dejar offset acumulado entre play sessions
        if (parallaxMaterial != null)
            parallaxMaterial.mainTextureOffset = Vector2.zero;
    }
}
