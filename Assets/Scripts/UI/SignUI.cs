using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel desplegable compartido para los carteles. Se arma solo en runtime.
/// Agregá este componente a un GameObject de la escena (ej. el HUD) y los
/// SignInteractable lo usan vía SignUI.Instance.
/// </summary>
public class SignUI : SingletonBehaviour<SignUI>
{
    [Header("Estilo")]
    [SerializeField] private Vector2 panelSize = new Vector2(560f, 240f);
    [SerializeField] private Color panelColor = new Color(0.05f, 0.05f, 0.08f, 0.92f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float fontSize = 18f;

    [Header("Pixel Art")]
    [Tooltip("Fuente pixel (ej. PressStart2P-Regular SDF). Si está vacío usa la default.")]
    [SerializeField] private TMP_FontAsset pixelFont;
    [Tooltip("Sprite del panel con borde pixelado (9-sliced). Si está vacío usa un rectángulo sólido.")]
    [SerializeField] private Sprite panelSprite;
    [Tooltip("Escala del borde del sprite 9-sliced. Subilo para que el borde se vea más grande/pixelado.")]
    [SerializeField] private float spriteBorderScale = 2f;

    [Header("Animación")]
    [SerializeField] private float openDuration = 0.18f;

    private Canvas canvas;
    private RectTransform panel;
    private CanvasGroup group;
    private TextMeshProUGUI label;
    private Coroutine anim;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        Build();
    }

    private void Build()
    {
        var go = new GameObject("SignUICanvas");
        go.transform.SetParent(transform, false);

        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(go.transform, false);
        var bg = panelGo.AddComponent<Image>();
        bg.color = panelColor;
        if (panelSprite != null)
        {
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = Mathf.Max(0.01f, spriteBorderScale);
        }

        panel = panelGo.GetComponent<RectTransform>();
        panel.sizeDelta = panelSize;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 1f); // se despliega hacia abajo
        panel.anchoredPosition = new Vector2(0f, panelSize.y * 0.5f);

        group = panelGo.AddComponent<CanvasGroup>();

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(panelGo.transform, false);
        label = textGo.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) label.font = pixelFont;
        label.color = textColor;
        // Auto-size: usa fontSize como tope y achica el texto hasta que entre en el panel, así los
        // mensajes largos no se desbordan ni se encima todo.
        label.enableAutoSizing = true;
        label.fontSizeMax = fontSize;
        label.fontSizeMin = Mathf.Min(10f, fontSize);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Truncate;
        label.alignment = TextAlignmentOptions.Center;

        var lrect = label.rectTransform;
        lrect.anchorMin = Vector2.zero;
        lrect.anchorMax = Vector2.one;
        lrect.offsetMin = new Vector2(28f, 28f);
        lrect.offsetMax = new Vector2(-28f, -28f);

        ApplyOpen(0f);
        canvas.enabled = false;
    }

    public void Show(string message)
    {
        if (label == null) return;
        label.text = message;
        canvas.enabled = true;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Animate(true));
    }

    public void Hide()
    {
        if (canvas == null || !canvas.enabled) return;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Animate(false));
    }

    private IEnumerator Animate(bool opening)
    {
        float from = opening ? 0f : 1f;
        float to   = opening ? 1f : 0f;

        float elapsed = 0f;
        while (elapsed < openDuration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.034f);
            float t = Mathf.SmoothStep(0f, 1f, elapsed / openDuration);
            ApplyOpen(Mathf.Lerp(from, to, t));
            yield return null;
        }

        ApplyOpen(to);
        if (!opening) canvas.enabled = false;
        anim = null;
    }

    private void ApplyOpen(float v)
    {
        panel.localScale = new Vector3(1f, v, 1f);
        group.alpha = v;
    }
}
