using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrameAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 12f;
    [SerializeField] private bool playOnce = false;

    private SpriteRenderer spriteRenderer;
    private float timer;
    private int currentFrame;
    private bool finished;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (frames != null && frames.Length > 0)
            spriteRenderer.sprite = frames[0];
    }

    private void Update()
    {
        if (finished || frames == null || frames.Length < 2) return;

        timer += Time.deltaTime;
        float frameDuration = 1f / fps;
        if (timer >= frameDuration)
        {
            timer -= frameDuration;
            int nextFrame = currentFrame + 1;

            if (playOnce && nextFrame >= frames.Length)
            {
                finished = true;
                return;
            }

            currentFrame = nextFrame % frames.Length;
            spriteRenderer.sprite = frames[currentFrame];
        }
    }
}
