using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BeatIndicator : MonoBehaviour
{
    [Header("Rhythm References")]
    [SerializeField] private BeatManager beatManager;

    [Header("Timing Windows")]
    [SerializeField] private float perfectWindow = 0.2f;
    [SerializeField] private float warningWindow = 0.35f;

    [Header("Visuals")]
    [SerializeField] private Color perfectColor = Color.green;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color offBeatColor = Color.red;
    [SerializeField] private Vector3 minScale = new Vector3(0.75f, 0.75f, 1f);
    [SerializeField] private Vector3 maxScale = new Vector3(1.2f, 1.2f, 1f);

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (beatManager == null)
        {
            beatManager = BeatManager.Instance;
        }
    }

    private void Update()
    {
        if (beatManager == null)
        {
            return;
        }

        float beatDelta = beatManager.GetAbsoluteBeatDelta(Time.time);
        float normalizedCloseness = 1f - Mathf.Clamp01(beatDelta / warningWindow);

        if (beatDelta <= perfectWindow)
        {
            spriteRenderer.color = perfectColor;
        }
        else if (beatDelta <= warningWindow)
        {
            spriteRenderer.color = warningColor;
        }
        else
        {
            spriteRenderer.color = offBeatColor;
        }

        transform.localScale = Vector3.Lerp(minScale, maxScale, normalizedCloseness);
    }

    public void Configure(
        BeatManager newBeatManager,
        float newPerfectWindow,
        float newWarningWindow,
        Color newPerfectColor,
        Color newWarningColor,
        Color newOffBeatColor,
        Vector3 newMinScale,
        Vector3 newMaxScale)
    {
        beatManager = newBeatManager;
        perfectWindow = Mathf.Max(0.01f, newPerfectWindow);
        warningWindow = Mathf.Max(perfectWindow, newWarningWindow);
        perfectColor = newPerfectColor;
        warningColor = newWarningColor;
        offBeatColor = newOffBeatColor;
        minScale = newMinScale;
        maxScale = newMaxScale;
    }

    private void OnValidate()
    {
        perfectWindow = Mathf.Max(0.01f, perfectWindow);
        warningWindow = Mathf.Max(perfectWindow, warningWindow);
    }
}
