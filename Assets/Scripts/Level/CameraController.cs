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
    [SerializeField] private float transitionDuration = 0.4f;
    [SerializeField] private float pauseBeforePan = 0.05f;
    [SerializeField] private float pauseAfterPan = 0.05f;

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
        yield return null;

        isTransitioning = true;
        if (cam == null) cam = Camera.main;

        Vector3 startPos = cam.transform.position;
        Vector3 endPos = ComputeRestingPosition(newRoom, startPos.z);

        Time.timeScale = 0f;

        float elapsed = 0f;
        while (elapsed < pauseBeforePan)
        {
            cam.transform.position = startPos;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transitionDuration));
            cam.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        cam.transform.position = endPos;

        elapsed = 0f;
        while (elapsed < pauseAfterPan)
        {
            cam.transform.position = endPos;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        currentRoom = newRoom;
        followVelocity = Vector3.zero;
        Time.timeScale = 1f;
        isTransitioning = false;
        RoomChanged?.Invoke(currentRoom);
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
        transitionDuration = Mathf.Max(0.1f, transitionDuration);
        pauseBeforePan = Mathf.Max(0f, pauseBeforePan);
        pauseAfterPan = Mathf.Max(0f, pauseAfterPan);
        followSmoothTime = Mathf.Max(0f, followSmoothTime);
    }
}