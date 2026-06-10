using System;
using System.Collections;
using UnityEngine;

/// <summary>Estilo de transición entre rooms.</summary>
public enum TransitionStyle
{
    /// Corte con fundido a negro (lugares distintos: paisaje ↔ cueva).
    FadeCut,
    /// Paneo suave estilo Metroid (pantallas adyacentes del mismo bioma).
    Pan
}

public class CameraController : SingletonBehaviour<CameraController>
{
    [Header("Follow")]
    [SerializeField] private bool smoothFollow = true;
    [SerializeField] private float followSmoothTime = 0.12f;

    [Header("Transition")]
    [SerializeField] private RoomBoundary startingRoom;
    [Tooltip("FadeCut: tiempo que la pantalla queda cubierta por el fundido antes de saltar. Debe ser >= Fade Out Duration del ScreenFader.")]
    [SerializeField] private float coverDuration = 0.25f;
    [Tooltip("Pan (Metroid): duración del paneo suave entre rooms adyacentes.")]
    [SerializeField] private float panDuration = 0.9f;

    private Camera cam;
    private RoomBoundary currentRoom;
    private bool isTransitioning;
    private Vector3 followVelocity;

    public bool IsTransitioning => isTransitioning;
    public RoomBoundary CurrentRoom => currentRoom;

    /// <summary>
    /// Se dispara cada vez que el room activo cambia (entrada inicial, snap, o fin de transición).
    /// Los suscriptores reciben el room NUEVO. Útil para parallax por room, audio, iluminación, etc.
    /// </summary>
    public static event Action<RoomBoundary> RoomChanged;
    public static event Action<RoomBoundary> TransitionStarted;

    /// <summary>
    /// Se dispara al comenzar un paneo (Pan), antes de mover la cámara. Entrega el room destino
    /// y la posición de reposo de la cámara al final del paneo. El parallax lo usa para congelar
    /// los fondos en el mundo y revelar el room entrante. No se dispara en FadeCut.
    /// </summary>
    public static event Action<RoomBoundary, Vector3> PanBegan;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
    }

    private void Start()
    {
        cam = Camera.main;
        if (startingRoom != null)
        {
            SetInitialRoom(startingRoom);
        }
    }

    private void LateUpdate()
    {
        if (isTransitioning || cam == null || currentRoom == null) return;
        if (PlayerController.Instance == null) return;

        Vector3 target = ComputeRestingPosition(currentRoom, cam.transform.position.z);

        if (smoothFollow && followSmoothTime > 0f)
        {
            cam.transform.position = Vector3.SmoothDamp(
                cam.transform.position, target, ref followVelocity, followSmoothTime);
        }
        else
        {
            cam.transform.position = target;
            followVelocity = Vector3.zero;
        }
    }

    public void SetInitialRoom(RoomBoundary room)
    {
        if (room == null) return;
        currentRoom = room;
        if (cam == null) cam = Camera.main;
        if (cam != null && PlayerController.Instance != null)
        {
            cam.transform.position = ComputeRestingPosition(room, cam.transform.position.z);
            followVelocity = Vector3.zero;
        }
        RoomChanged?.Invoke(currentRoom);
    }

    public void TransitionToRoom(RoomBoundary newRoom) => TransitionToRoom(newRoom, TransitionStyle.FadeCut);

    public void TransitionToRoom(RoomBoundary newRoom, TransitionStyle style)
    {
        if (newRoom == null || isTransitioning || newRoom == currentRoom) return;
        StartCoroutine(style == TransitionStyle.Pan ? PanRoutine(newRoom) : TransitionRoutine(newRoom));
    }

    public void SnapToRoom(RoomBoundary room)
    {
        if (room == null) return;
        currentRoom = room;
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = ComputeRestingPosition(room, cam.transform.position.z);
            followVelocity = Vector3.zero;
        }
        RoomChanged?.Invoke(currentRoom);
    }

    private IEnumerator TransitionRoutine(RoomBoundary newRoom)
    {
        isTransitioning = true;
        Time.timeScale = 0f;

        // El ScreenFader arranca el fade out al recibir esto.
        TransitionStarted?.Invoke(newRoom);

        // Esperar a que el fundido cubra la pantalla por completo.
        float elapsed = 0f;
        while (elapsed < coverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Bajo el negro: salto de cámara + cambio de room (el parallax swapea acá).
        if (cam == null) cam = Camera.main;
        currentRoom = newRoom;
        if (cam != null)
        {
            cam.transform.position = ComputeRestingPosition(newRoom, cam.transform.position.z);
            followVelocity = Vector3.zero;
        }
        RoomChanged?.Invoke(currentRoom);

        Time.timeScale = 1f;
        isTransitioning = false;
    }

    // Paneo suave estilo Metroid: la cámara se desliza del room viejo al nuevo, sin negro.
    private IEnumerator PanRoutine(RoomBoundary newRoom)
    {
        isTransitioning = true;
        Time.timeScale = 0f;

        if (cam == null) cam = Camera.main;
        Vector3 startPos = cam.transform.position;
        Vector3 endPos = ComputeRestingPosition(newRoom, startPos.z);

        // Avisar al parallax antes de mover la cámara: congela el fondo saliente en su lugar
        // y muestra el entrante world-locked en endPos, para que el paneo lo revele sin duplicar.
        PanBegan?.Invoke(newRoom, endPos);

        float elapsed = 0f;
        while (elapsed < panDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / panDuration));
            cam.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        cam.transform.position = endPos;

        currentRoom = newRoom;
        followVelocity = Vector3.zero;
        RoomChanged?.Invoke(currentRoom);

        Time.timeScale = 1f;
        isTransitioning = false;
    }

    private Vector3 ComputeRestingPosition(RoomBoundary room, float zDepth)
    {
        Vector3 playerPos = PlayerController.Instance != null
            ? PlayerController.Instance.transform.position
            : room.transform.position;

        Bounds b = room.Bounds;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        float minX = b.min.x + halfW;
        float maxX = b.max.x - halfW;
        float minY = b.min.y + halfH;
        float maxY = b.max.y - halfH;

        float x = minX > maxX ? b.center.x : Mathf.Clamp(playerPos.x, minX, maxX);
        float y = minY > maxY ? b.center.y : Mathf.Clamp(playerPos.y, minY, maxY);

        return new Vector3(x, y, zDepth);
    }

    public void Shake(float duration, float magnitude)
    {
        StopCoroutine("ShakeRoutine");
        StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        if (cam == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float strength = Mathf.Lerp(magnitude, 0f, elapsed / duration);
            Vector2 offset = UnityEngine.Random.insideUnitCircle * strength;
            cam.transform.position += new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }
    }

    private void OnValidate()
    {
        coverDuration = Mathf.Max(0f, coverDuration);
        panDuration = Mathf.Max(0.05f, panDuration);
        followSmoothTime = Mathf.Max(0f, followSmoothTime);
    }
}