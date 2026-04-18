using System.Collections;
using TMPro;
using UnityEngine;

public class FloatingText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private float floatSpeed = 80f;
    [SerializeField] private float duration = 1f;

    public void Play(string text, Color color)
    {
        if (label != null)
        {
            label.text = text;
            label.color = color;
        }

        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float elapsed = 0f;
        Vector2 startPos = ((RectTransform)transform).anchoredPosition;
        Color startColor = label != null ? label.color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            ((RectTransform)transform).anchoredPosition = startPos + Vector2.up * (floatSpeed * progress);

            if (label != null)
            {
                Color c = startColor;
                c.a = 1f - progress;
                label.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
