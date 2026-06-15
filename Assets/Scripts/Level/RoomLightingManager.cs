using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Maneja la iluminación global y el light del player por room.
/// - globalLight2D: el Global Light 2D de la escena (oscurece todo en la cueva).
/// - playerLight2D: un Light2D hijo del player (ilumina solo el entorno cercano).
/// RoomEnvironment llama a SetLighting() al cambiar de room.
/// </summary>
public class RoomLightingManager : SingletonBehaviour<RoomLightingManager>
{
    [Tooltip("El Global Light 2D de la escena (arrastralo desde la jerarquía).")]
    [SerializeField] private Light2D globalLight2D;

    [Tooltip("El Light2D hijo del Player (Point light que lo sigue).")]
    [SerializeField] private Light2D playerLight2D;

    [SerializeField] private float fadeDuration = 0.6f;

    private Coroutine fadeRoutine;

    // El apagado por defecto va en Awake (no en Start): Awake corre SIEMPRE antes que cualquier
    // Start, así ocurre antes de que CameraController.Start dispare RoomChanged y el room encienda
    // la player light. Si esto estuviera en Start, podía correr DESPUÉS y volver a apagarla.
    protected override void Awake()
    {
        base.Awake();
        if (playerLight2D != null)
            playerLight2D.enabled = false;
    }

    /// <param name="globalIntensity">Intensidad del Global Light (1 = normal, ~0.05 = cueva oscura).</param>
    /// <param name="enablePlayerLight">Si true, enciende el light del player (efecto linterna).</param>
    /// <param name="duration">Duración del fade. Usa unscaledTime.</param>
    public void SetLighting(float globalIntensity, bool enablePlayerLight, float duration)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeLighting(globalIntensity, enablePlayerLight, duration));
    }

    private IEnumerator FadeLighting(float targetIntensity, bool enablePlayerLight, float duration)
    {
        float startIntensity = globalLight2D != null ? globalLight2D.intensity : 1f;

        if (enablePlayerLight && playerLight2D != null)
            playerLight2D.enabled = true;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            if (globalLight2D != null)
                globalLight2D.intensity = Mathf.Lerp(startIntensity, targetIntensity, t / duration);
            yield return null;
        }

        if (globalLight2D != null)
            globalLight2D.intensity = targetIntensity;

        if (!enablePlayerLight && playerLight2D != null)
            playerLight2D.enabled = false;

        fadeRoutine = null;
    }
}
