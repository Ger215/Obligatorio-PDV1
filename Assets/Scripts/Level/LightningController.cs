using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Relámpagos ambientales con iluminación dinámica. Usa un Light2D DEDICADO (no el Global Light
/// del room): en reposo está en intensidad 0 y cada rayo lo sube de golpe y lo devuelve a 0, con
/// un par de parpadeos para que se sienta real. Como parte de 0 y vuelve a 0, se SUMA a la
/// iluminación actual sin pisar la del RoomLightingManager.
///
/// No tormenta solo: o tildás playOnStart, o lo encendés por código (BeginStorm/StopStorm),
/// p.ej. desde un RoomTrigger para que llueva solo en ciertos rooms.
/// </summary>
public class LightningController : MonoBehaviour
{
    [Header("Luz del rayo (DEDICADA, no el Global del room)")]
    [Tooltip("Un Light2D aparte (Global o Point) que en reposo dejás en Intensity 0. El rayo lo sube.")]
    [SerializeField] private Light2D lightningLight;
    [Tooltip("Intensidad pico durante el relámpago.")]
    [SerializeField] private float flashIntensity = 1.4f;
    [Tooltip("Color del flash. Azulado frío queda muy de tormenta.")]
    [SerializeField] private Color flashColor = new Color(0.8f, 0.85f, 1f);

    [Header("¿Arranca tormentando?")]
    [Tooltip("Si está tildado, empieza a tronar al iniciar. Si no, esperá a BeginStorm().")]
    [SerializeField] private bool playOnStart = false;

    [Header("Timing entre rayos")]
    [SerializeField] private float minInterval = 6f;
    [SerializeField] private float maxInterval = 14f;

    [Header("Forma del rayo")]
    [Tooltip("Cuántos parpadeos tiene cada rayo (un rayo real titila 2-3 veces).")]
    [SerializeField] private Vector2Int flickerCount = new Vector2Int(2, 4);
    [Tooltip("Cuánto dura encendido cada parpadeo.")]
    [SerializeField] private float flickerUpTime = 0.05f;
    [Tooltip("Cuánto dura apagado entre parpadeos.")]
    [SerializeField] private float flickerDownTime = 0.08f;

    [Header("Trueno (audio)")]
    [SerializeField] private AudioClip thunderClip;
    [Tooltip("Delay entre el flash y el trueno: la luz viaja más rápido que el sonido.")]
    [SerializeField] private float thunderDelay = 0.7f;
    [SerializeField, Range(0f, 1f)] private float thunderVolume = 0.8f;

    private Coroutine stormRoutine;

    private void Awake()
    {
        // En reposo apagada: la luz del rayo no aporta nada hasta que cae un rayo.
        if (lightningLight != null)
        {
            lightningLight.color = flashColor;
            lightningLight.intensity = 0f;
        }
    }

    private void Start()
    {
        if (playOnStart) BeginStorm();
    }

    /// <summary>Empieza la tormenta: rayos en intervalos aleatorios.</summary>
    public void BeginStorm()
    {
        if (lightningLight == null || stormRoutine != null) return;
        stormRoutine = StartCoroutine(StormLoop());
    }

    /// <summary>Corta la tormenta y deja la luz del rayo apagada.</summary>
    public void StopStorm()
    {
        if (stormRoutine != null) StopCoroutine(stormRoutine);
        stormRoutine = null;
        if (lightningLight != null) lightningLight.intensity = 0f;
    }

    /// <summary>Dispara un único rayo ahora mismo (sin esperar el intervalo).</summary>
    public void StrikeNow()
    {
        if (lightningLight != null) StartCoroutine(Strike());
    }

    private void OnDisable() => StopStorm();

    private IEnumerator StormLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
            yield return Strike();
        }
    }

    private IEnumerator Strike()
    {
        if (thunderClip != null) StartCoroutine(PlayThunder());

        int flickers = Random.Range(flickerCount.x, flickerCount.y + 1);
        for (int i = 0; i < flickers; i++)
        {
            lightningLight.intensity = flashIntensity;
            yield return new WaitForSeconds(flickerUpTime);
            lightningLight.intensity = 0f;
            yield return new WaitForSeconds(flickerDownTime);
        }

        lightningLight.intensity = 0f;
    }

    private IEnumerator PlayThunder()
    {
        yield return new WaitForSeconds(thunderDelay);
        Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(thunderClip, pos, thunderVolume);
    }
}
