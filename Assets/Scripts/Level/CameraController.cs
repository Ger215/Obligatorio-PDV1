using System;
using System.Collections;
using UnityEngine;

public class CameraController : SingletonBehaviour<CameraController>
{
    [Header("Follow")]
    [SerializeField] private bool smoothFollow = true;
    [SerializeField] private float followSmoothTime = 0.12f;

    [Header("Transition")]
    [SerializeField] private RoomBoundary startingRoom;
    [Tooltip("Tiempo que la pantalla queda cubierta por el fundido antes de saltar al nuevo room. Debe ser >= Fade Out Duration del ScreenFader.")]
    [SerializeField] private float coverDuration = 0.25f;

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

    public void TransitionToRoom(RoomBoundary newRoom)
    {
        if (newRoom == null || isTransitioning || newRoom == currentRoom) return;
        StartCoroutine(TransitionRoutine(newRoom));
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
        followSmoothTime = Mathf.Max(0f, followSmoothTime);
    }
}