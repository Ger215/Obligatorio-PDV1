using UnityEngine;

/// <summary>
/// Lluvia simple 100% por código: configura un ParticleSystem y se crea su propio material
/// blanco en runtime (no necesita sprites ni assets). Sigue a la cámara para cubrir siempre la
/// pantalla. Pensada para el nivel tormentoso; combinala con el LightningController.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class RainEffect : MonoBehaviour
{
    [Header("Densidad y velocidad")]
    [Tooltip("Gotas por segundo. Más alto = lluvia más intensa.")]
    [SerializeField] private float dropsPerSecond = 250f;
    [Tooltip("Velocidad de caída (unidades/seg).")]
    [SerializeField] private float fallSpeed = 22f;
    [Tooltip("Viento lateral: negativo = cae hacia la izquierda. 0 = vertical.")]
    [SerializeField] private float wind = -4f;

    [Header("Aspecto de la gota")]
    [Tooltip("Color y transparencia de la gota. Alpha bajo = lluvia sutil.")]
    [SerializeField] private Color dropColor = new Color(0.7f, 0.8f, 1f, 0.5f);
    [Tooltip("Grosor de la gota.")]
    [SerializeField] private float dropSize = 0.06f;
    [Tooltip("Cuánto se estira la gota en su dirección (raya de lluvia).")]
    [SerializeField] private float streakLength = 3f;

    [Header("Área")]
    [Tooltip("Margen extra a los lados para que no se vea el borde de la lluvia.")]
    [SerializeField] private float horizontalMargin = 6f;
    [Tooltip("Altura sobre el tope de la cámara desde donde caen las gotas.")]
    [SerializeField] private float spawnHeight = 2f;

    private ParticleSystem ps;
    private ParticleSystem.ShapeModule shape;
    private Camera cam;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        cam = Camera.main;
        Configure();
    }

    private void Configure()
    {
        // Vida suficiente para cruzar toda la pantalla.
        float life = cam != null ? (cam.orthographicSize * 2f + spawnHeight + 2f) / fallSpeed : 1.5f;

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // no arrastra las gotas al mover la cámara
        main.startSpeed = 0f;
        main.startLifetime = life;
        main.startSize = dropSize;
        main.startColor = dropColor;
        main.gravityModifier = 0f;
        main.maxParticles = 2000;

        var emission = ps.emission;
        emission.rateOverTime = dropsPerSecond;

        // Caja chata y ancha desde donde "llueve".
        shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1f, 0.1f, 1f);

        // Velocidad constante: cae con un poco de viento lateral (streak diagonal).
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(wind);
        vel.y = new ParticleSystem.MinMaxCurve(-fallSpeed);

        // Render como raya estirada, con material blanco creado al vuelo (cero assets).
        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = streakLength;
        renderer.velocityScale = 0f;
        renderer.cameraVelocityScale = 0f;
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.sortingOrder = 1000; // por delante de casi todo
    }

    /// <summary>
    /// Prende o corta la lluvia. Al cortar, deja de emitir pero las gotas ya en pantalla terminan
    /// de caer (transición suave, no un corte de golpe). La usan las <c>RainZone</c> por room.
    /// </summary>
    public void SetRaining(bool raining)
    {
        if (ps == null) return;

        if (raining)
        {
            if (!ps.isEmitting) ps.Play();
        }
        else
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Mantener el emisor arriba del tope de la cámara, cubriendo todo el ancho.
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        Vector3 p = cam.transform.position;
        transform.position = new Vector3(p.x, p.y + halfH + spawnHeight, 0f);

        // Ajustar el ancho de la caja al viewport + margen (por si cambia el aspect).
        shape.scale = new Vector3((halfW + horizontalMargin) * 2f, 0.1f, 1f);
    }
}
