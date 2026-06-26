using UnityEngine;

/// <summary>
/// Parallax para escena 2D con quads (MeshRenderer) o SpriteRenderers como hijos.
/// Detecta automáticamente la property correcta del shader (_BaseMap en URP, _MainTex en built-in).
/// IMPORTANTE: corre con ExecutionOrder alto para garantizar que sigue la cámara DESPUÉS
/// de que CameraController la haya actualizado en el mismo frame.
/// </summary>
[DefaultExecutionOrder(1000)]
public class ParallaxController : MonoBehaviour
{
    [Tooltip("Cámara a seguir. Si está vacío usa Camera.main.")]
    [SerializeField] private Transform cam;

    [Tooltip("Multiplicador global del UV scroll. Empezá en 0.1 y ajustá. Negativo invierte el sentido.")]
    [Range(-1f, 1f)]
    [SerializeField] private float parallaxSpeed = 0.1f;

    [Tooltip("Multiplicador POR CAPA, en orden de hijos. Si está vacío usa backSpeed automático por Z. " +
             "Ej.: [0.0, 0.5, 1.0] => sky anclado a cámara, mid medio, decor world-locked.")]
    [SerializeField] private float[] perLayerOverride;

    [Tooltip("Si true el padre sigue la cámara en X.")]
    [SerializeField] private bool followCameraX = true;

    [Tooltip("Si true también sigue en Y (normalmente false).")]
    [SerializeField] private bool followCameraY = false;

    [Header("Pixel Art")]
    [Tooltip("Si true, snappea el UV offset al pixel exacto de la textura. " +
             "Activalo para pixel art (Filter Mode = Point) para evitar jitter por sub-pixel.")]
    [SerializeField] private bool pixelSnapUV = true;

    [Header("Debug")]
    [Tooltip("Imprime info de setup y por frame. ACTIVALO para diagnosticar.")]
    [SerializeField] private bool debugLogs = true;

    private Transform[] backgrounds;
    private Material[] mats;
    private string[] texProps; // nombre de propiedad de textura por capa
    private Texture[] textures; // referencia a la textura para conocer su ancho en pixeles
    private Vector2[] accumOffset; // offset acumulado en sub-pixel (verdadero), separado del que se aplica snapeado
    private float[] backSpeeds;
    private float farthestBack;
    private Vector3 lastCamPos;
    private int frameCount;
    private bool frozen;

    private bool initialized;

    private void Start() => EnsureInitialized();

    // Setup idempotente. Se llama desde Start (room visible) y desde FreezeForPan (room entrante
    // de un pan), para que el primer pan tenga materiales/UV/velocidades listos igual que los siguientes.
    private void EnsureInitialized()
    {
        if (initialized) return;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (cam == null) return; // sin cámara todavía: reintenta en el próximo llamado

        int count = transform.childCount;
        backgrounds = new Transform[count];
        mats = new Material[count];
        texProps = new string[count];
        textures = new Texture[count];
        backSpeeds = new float[count];
        accumOffset = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            backgrounds[i] = transform.GetChild(i);
            Renderer r = backgrounds[i].GetComponent<Renderer>();
            if (r == null)
            {
                if (debugLogs) Debug.LogWarning($"[Parallax] Hijo {i} '{backgrounds[i].name}' SIN Renderer.");
                continue;
            }

            // CRÍTICO: chequear sharedMaterial vs instancia. Si dos hijos comparten material, comparten offset.
            Material shared = r.sharedMaterial;
            mats[i] = r.material; // fuerza instancia única
            texProps[i] = DetectTextureProperty(mats[i]);
            if (texProps[i] != null) textures[i] = mats[i].GetTexture(texProps[i]);

            if (debugLogs)
            {
                Debug.Log($"[Parallax] hijo[{i}] '{backgrounds[i].name}' shader='{mats[i].shader.name}' " +
                          $"texProp='{texProps[i]}' sharedMat='{(shared != null ? shared.name : "null")}'");
            }
        }

        // Buscar el más lejano
        farthestBack = 0f;
        for (int i = 0; i < count; i++)
        {
            float z = backgrounds[i].position.z - cam.position.z;
            if (z > farthestBack) farthestBack = z;
        }
        if (farthestBack <= 0f) farthestBack = 1f;

        for (int i = 0; i < count; i++)
        {
            float z = backgrounds[i].position.z - cam.position.z;
            backSpeeds[i] = 1f - (z / farthestBack);
        }

        if (debugLogs)
        {
            for (int i = 0; i < count; i++)
            {
                float used = (perLayerOverride != null && i < perLayerOverride.Length) ? perLayerOverride[i] : backSpeeds[i];
                Debug.Log($"[Parallax] hijo[{i}] z={backgrounds[i].position.z:F2} backSpeed={backSpeeds[i]:F2} usado={used:F2}");
            }
        }

        lastCamPos = cam.position;
        initialized = true;
    }

    private string DetectTextureProperty(Material m)
    {
        // URP shaders usan _BaseMap; built-in usa _MainTex; sprites usan _MainTex también.
        if (m.HasProperty("_BaseMap")) return "_BaseMap";
        if (m.HasProperty("_MainTex")) return "_MainTex";
        return null;
    }

    /// <summary>
    /// Congela el parallax para un paneo (Pan): deja de seguir la cámara y de scrollear UV,
    /// quedando fijo en el mundo para que la cámara lo revele al deslizarse. No afecta FadeCut.
    /// </summary>
    public void FreezeForPan()
    {
        EnsureInitialized();
        frozen = true;
    }

    /// <summary>
    /// Igual que FreezeForPan() pero reposiciona el fondo EXACTAMENTE donde lo dejaría LateUpdate
    /// al reanudar (aplicando followCameraX/followCameraY sobre la pos de cámara destino) y resetea
    /// el UV. Así el room entrante se revela ya en su posición final y no pega salto al terminar el
    /// pan — clave cuando los rooms tienen alturas distintas y followCameraY está activo.
    /// </summary>
    public void FreezeForPan(Vector3 cameraPos)
    {
        EnsureInitialized();
        frozen = true;

        float x = followCameraX ? cameraPos.x : transform.position.x;
        float y = followCameraY ? cameraPos.y : transform.position.y;
        transform.position = new Vector3(x, y, transform.position.z);

        if (mats == null) return;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null || texProps[i] == null) continue;
            accumOffset[i] = Vector2.zero;
            mats[i].SetTextureOffset(texProps[i], Vector2.zero);
        }
    }

    /// <summary>Vuelve al parallax normal tras el paneo, sin saltos (re-ancla lastCamPos).</summary>
    public void ResumeAfterPan()
    {
        frozen = false;
        if (cam != null) lastCamPos = cam.position;
    }

    private void LateUpdate()
    {
        EnsureInitialized();
        if (cam == null || frozen) return;

        if (followCameraX || followCameraY)
        {
            float x = followCameraX ? cam.position.x : transform.position.x;
            float y = followCameraY ? cam.position.y : transform.position.y;
            transform.position = new Vector3(x, y, transform.position.z);
        }

        Vector3 deltaCam = cam.position - lastCamPos;

        if (mats != null)
        {
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || texProps[i] == null) continue;
                float layerFactor = (perLayerOverride != null && i < perLayerOverride.Length)
                    ? perLayerOverride[i]
                    : backSpeeds[i];
                // Acumular en sub-pixel (NUNCA leer del material porque puede estar snapeado)
                accumOffset[i].x += deltaCam.x * parallaxSpeed * layerFactor;

                Vector2 toApply = accumOffset[i];
                if (pixelSnapUV && textures[i] != null && textures[i].width > 0)
                {
                    float w = textures[i].width;
                    toApply.x = Mathf.Round(accumOffset[i].x * w) / w;
                }
                mats[i].SetTextureOffset(texProps[i], toApply);

                if (debugLogs && frameCount % 30 == 0 && Mathf.Abs(deltaCam.x) > 0.001f)
                {
                    Debug.Log($"[Parallax] hijo[{i}] '{backgrounds[i].name}' factor={layerFactor:F2} " +
                              $"deltaCam={deltaCam.x:F3} accum={accumOffset[i].x:F4} applied={toApply.x:F4}");
                }
            }
        }

        lastCamPos = cam.position;
        frameCount++;
    }

    private void OnDisable()
    {
        if (mats == null) return;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] != null && texProps != null && i < texProps.Length && texProps[i] != null)
                mats[i].SetTextureOffset(texProps[i], Vector2.zero);
        }
    }
}
