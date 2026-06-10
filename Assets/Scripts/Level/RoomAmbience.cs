using System.Collections;
using UnityEngine;

/// <summary>
/// Tinte de ambiente por room. Construye en runtime un quad de sprite blanco que sigue a la
/// cámara y cubre el viewport, renderizado por encima del gameplay pero por debajo del HUD
/// (que es un Canvas ScreenSpaceOverlay). RoomEnvironment llama a SetAmbient al cambiar de room.
/// </summary>
public class RoomAmbience : SingletonBehaviour<RoomAmbience>
{
    [Tooltip("Orden de render del tinte. Alto para tapar el gameplay, pero el HUD (ScreenSpaceOverlay) queda por encima igual.")]
    [SerializeField] private int sortingOrder = 1000;

    [Tooltip("Distancia en Z delante de la cámara a la que se coloca el quad de tinte.")]
    [SerializeField] private float distanceFromCamera = 1f;

    [Tooltip("Margen extra sobre el tamaño del viewport para evitar bordes sin cubrir.")]
    [SerializeField] private float coverPadding = 2f;

    private Transform cam;
    private SpriteRenderer tint;
    private Color currentColor = new Color(0f, 0f, 0f, 0f);
    private Coroutine fadeRoutine;

    private void Start()
    {
        BuildOverlay();
    }

    private void BuildOverlay()
    {
        if (Camera.main != null) cam = Camera.main.transform;

        var go = new GameObject("RoomAmbienceTint");
        go.transform.SetParent(transform, false);

        tint = go.AddComponent<SpriteRenderer>();
        tint.sprite = BuildWhiteSprite();
        tint.color = currentColor;
        tint.sortingOrder = sortingOrder;
        tint.drawMode = SpriteDrawMode.Sliced; // permite escalar con size sin estirar bordes

        ResizeToViewport();
    }

    private static Sprite BuildWhiteSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0,
            SpriteMeshType.FullRect, new Vector4(1f, 1f, 1f, 1f));
    }

    private void LateUpdate()
    {
        if (tint == null) return;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (cam == null) return;

        Vector3 p = cam.position + cam.forward * distanceFromCamera;
        tint.transform.position = new Vector3(p.x, p.y, cam.position.z + distanceFromCamera);
        ResizeToViewport();
    }

    private void ResizeToViewport()
    {
        if (tint == null) return;
        Camera c = Camera.main;
        if (c == null || !c.orthographic) return;

        float h = c.orthographicSize * 2f + coverPadding;
        float w = h * c.aspect + coverPadding;
        tint.size = new Vector2(w, h);
    }

    /// <summary>
    /// Lerpea el tinte de pantalla hacia <paramref name="target"/> en tiempo no escalado
    /// (las transiciones de room corren con timeScale = 0). Color clear/alpha 0 = sin tinte.
    /// </summary>
    public void SetAmbient(Color target, float duration)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        if (tint == null || duration <= 0f)
        {
            currentColor = target;
            if (tint != null) tint.color = target;
            return;
        }

        fadeRoutine = StartCoroutine(FadeTo(target, duration));
    }

    private IEnumerator FadeTo(Color target, float duration)
    {
        Color from = currentColor;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            currentColor = Color.Lerp(from, target, t / duration);
            tint.color = currentColor;
            yield return null;
        }

        currentColor = target;
        tint.color = target;
        fadeRoutine = null;
    }
}
