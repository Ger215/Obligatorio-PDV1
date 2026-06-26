using UnityEngine;

/// <summary>
/// Versión AUTOMÁTICA del parallax: en vez de scrollear el UV según el movimiento de la cámara
/// (como ParallaxController), lo desplaza solo con el tiempo. Pensado para escenas donde la cámara
/// NO se mueve y queremos sensación de avance, ej: la escena "Loading" con el personaje corriendo
/// en el lugar mientras el fondo se desplaza.
///
/// Mismo armado que ParallaxController: ponelo en el PADRE (ej: WorldBackground_Start) y maneja a
/// cada hijo (BackgroundSky, BackgroundTrees, BackgroundCloud...) como una capa. Cada capa scrollea
/// a su propia velocidad → parallax: cielo lento, árboles rápido.
///
/// Detecta solo la property correcta del shader (_BaseMap en URP, _MainTex en built-in) y usa una
/// instancia única de material por hijo, así no toca el asset compartido.
/// IMPORTANTE: la textura de cada capa debe tener Wrap Mode = Repeat, si no se estira en vez de repetir.
/// </summary>
public class AutoParallaxScroller : MonoBehaviour
{
    [Tooltip("Velocidad base de scroll del UV (unidades UV/segundo). Positivo = la textura se mueve a " +
             "la izquierda, el personaje parece avanzar a la derecha. Negativo invierte el sentido.")]
    [SerializeField] private float scrollSpeed = 0.1f;

    [Tooltip("Multiplicador POR CAPA, en orden de hijos. Ej.: [0.2, 0.6, 1.0] => cielo lento, " +
             "árboles medio, lo más cercano rápido. Si está vacío usa velocidad por Z (más cerca = más rápido).")]
    [SerializeField] private float[] perLayerSpeed;

    [Tooltip("Scroll vertical en vez de horizontal (raro, pero por si lo necesitás).")]
    [SerializeField] private bool scrollVertical = false;

    [Header("Pixel Art")]
    [Tooltip("Si true, snappea el UV offset al pixel exacto de la textura. Activalo para pixel art " +
             "(Filter Mode = Point) para evitar jitter por sub-pixel.")]
    [SerializeField] private bool pixelSnapUV = true;

    private Transform[] layers;
    private Material[] mats;
    private string[] texProps;
    private Texture[] textures;
    private float[] layerFactors;
    private Vector2[] accumOffset;
    private bool initialized;

    private void Start() => EnsureInitialized();

    private void EnsureInitialized()
    {
        if (initialized) return;

        int count = transform.childCount;
        layers = new Transform[count];
        mats = new Material[count];
        texProps = new string[count];
        textures = new Texture[count];
        layerFactors = new float[count];
        accumOffset = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            layers[i] = transform.GetChild(i);
            Renderer r = layers[i].GetComponent<Renderer>();
            if (r == null) continue;

            mats[i] = r.material; // instancia única
            texProps[i] = DetectTextureProperty(mats[i]);
            if (texProps[i] != null) textures[i] = mats[i].GetTexture(texProps[i]);
        }

        // Factor por capa: override manual, o automático por Z (más cerca de la cámara = más rápido).
        float farthest = 0f;
        Camera mainCam = Camera.main;
        float camZ = mainCam != null ? mainCam.transform.position.z : 0f;
        for (int i = 0; i < count; i++)
        {
            float z = layers[i].position.z - camZ;
            if (z > farthest) farthest = z;
        }
        if (farthest <= 0f) farthest = 1f;

        for (int i = 0; i < count; i++)
        {
            if (perLayerSpeed != null && i < perLayerSpeed.Length)
            {
                layerFactors[i] = perLayerSpeed[i];
            }
            else
            {
                float z = layers[i].position.z - camZ;
                layerFactors[i] = 1f - (z / farthest);
            }
        }

        initialized = true;
    }

    private string DetectTextureProperty(Material m)
    {
        if (m.HasProperty("_BaseMap")) return "_BaseMap";
        if (m.HasProperty("_MainTex")) return "_MainTex";
        return null;
    }

    private void Update()
    {
        EnsureInitialized();
        if (mats == null) return;

        float step = scrollSpeed * Time.deltaTime;

        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null || texProps[i] == null) continue;

            float delta = step * layerFactors[i];
            if (scrollVertical) accumOffset[i].y += delta;
            else accumOffset[i].x += delta;

            // Mantener acotado para no perder precisión con el tiempo.
            accumOffset[i].x %= 1f;
            accumOffset[i].y %= 1f;

            Vector2 toApply = accumOffset[i];
            if (pixelSnapUV && textures[i] != null && textures[i].width > 0)
            {
                float w = textures[i].width;
                toApply.x = Mathf.Round(accumOffset[i].x * w) / w;
                toApply.y = Mathf.Round(accumOffset[i].y * w) / w;
            }

            mats[i].SetTextureOffset(texProps[i], toApply);
        }
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
