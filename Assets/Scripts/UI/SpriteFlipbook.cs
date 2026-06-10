using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Anima una secuencia de sprites alternándolos a X frames por segundo.
/// Funciona sobre un SpriteRenderer (world-space) o un Image (UI).
/// Se reinicia cada vez que el GameObject se activa.
/// </summary>
public class SpriteFlipbook : MonoBehaviour
{
    [Header("Frames")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 3f;

    [Tooltip("Usar tiempo no escalado (sigue animando aunque el juego esté pausado).")]
    [SerializeField] private bool unscaledTime = true;

    private SpriteRenderer spriteRenderer;
    private Image image;
    private int index;
    private float timer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        index = 0;
        timer = 0f;
        Apply();
    }

    private void Update()
    {
        if (frames == null || frames.Length < 2 || framesPerSecond <= 0f) return;

        timer += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float interval = 1f / framesPerSecond;
        if (timer < interval) return;

        timer -= interval;
        index = (index + 1) % frames.Length;
        Apply();
    }

    private void Apply()
    {
        Sprite s = frames[index];
        if (spriteRenderer != null) spriteRenderer.sprite = s;
        if (image != null) image.sprite = s;
    }
}
