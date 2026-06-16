using UnityEngine;

/// <summary>
/// Herramienta de debug temporal: escanea los clips de un Animator (o de TODOS los Animators de la
/// escena) y loguea qué Animation Events tienen el campo Function vacío o mal escrito. Sirve para
/// cazar el warning "AnimationEvent has no function name specified!". Borralo cuando lo resuelvas.
/// </summary>
public class AnimationEventDebugger : MonoBehaviour
{
    [Tooltip("Si está activo, escanea todos los Animators de la escena. Si no, solo el de este objeto.")]
    [SerializeField] private bool scanWholeScene = true;

    private void Start()
    {
        if (scanWholeScene)
        {
            Animator[] animators = FindObjectsByType<Animator>(FindObjectsSortMode.None);
            foreach (Animator animator in animators)
            {
                ScanAnimator(animator);
            }
        }
        else
        {
            ScanAnimator(GetComponent<Animator>());
        }
    }

    private void ScanAnimator(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null)
            {
                continue;
            }

            AnimationEvent[] events = clip.events;
            for (int i = 0; i < events.Length; i++)
            {
                AnimationEvent evt = events[i];
                bool emptyFunction = string.IsNullOrWhiteSpace(evt.functionName);

                if (emptyFunction)
                {
                    Debug.LogError(
                        $"[AnimEventDebugger] VACÍO → GameObject '{animator.gameObject.name}', " +
                        $"clip '{clip.name}', evento #{i} en t={evt.time:0.000}s (frame ~{Mathf.RoundToInt(evt.time * clip.frameRate)}). " +
                        $"Este es el que tira el warning.",
                        animator.gameObject);
                }
                else
                {
                    Debug.Log(
                        $"[AnimEventDebugger] ok → '{animator.gameObject.name}' / clip '{clip.name}' / " +
                        $"evento #{i} t={evt.time:0.000}s → {evt.functionName}()",
                        animator.gameObject);
                }
            }
        }
    }
}
