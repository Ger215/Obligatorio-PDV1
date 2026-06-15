using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Hace titilar un Light2D (point) para simular el fuego de una antorcha: oscila la intensidad
/// suavemente alrededor de un valor base con un poco de ruido. Poné este componente en el mismo
/// objeto que el Light2D de la torch (o asignalo a mano).
/// </summary>
[RequireComponent(typeof(Light2D))]
public class TorchLightFlicker : MonoBehaviour
{
    [Tooltip("Intensidad media de la antorcha.")]
    [SerializeField] private float baseIntensity = 1f;
    [Tooltip("Cuánto sube/baja la intensidad al titilar.")]
    [SerializeField] private float intensityVariation = 0.25f;
    [Tooltip("Velocidad del titileo. Más alto = más nervioso.")]
    [SerializeField] private float flickerSpeed = 8f;

    private Light2D light2D;
    private float noiseSeed;

    private void Awake()
    {
        light2D = GetComponent<Light2D>();
        noiseSeed = Random.value * 100f;
    }

    private void Update()
    {
        float noise = Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed);
        light2D.intensity = baseIntensity + (noise - 0.5f) * 2f * intensityVariation;
    }
}
