using System.Collections;
using UnityEngine;

public class AudioManager : SingletonBehaviour<AudioManager>
{
    [SerializeField] private GameAudioConfig audioConfig;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Música")]
    [Tooltip("Duración del crossfade al cambiar de tema (por room).")]
    [SerializeField] private float musicFadeDuration = 0.6f;

    private float baseMusicVolume = 1f;
    private Coroutine musicFadeRoutine;

    /// <summary>True si hay un tema sonando o en pleno crossfade. Lo usa el GameManager para
    /// no pisar con el backgroundMusic la música que un room ya pidió.</summary>
    public bool IsMusicPlaying => musicSource != null && (musicSource.isPlaying || musicFadeRoutine != null);

    private void Start()
    {
        if (musicSource != null)
        {
            baseMusicVolume = musicSource.volume <= 0.01f ? 1f : musicSource.volume;
        }
    }

    public void PlayBackgroundMusic()
    {
        if (audioConfig == null || musicSource == null || audioConfig.backgroundMusic == null)
        {
            return;
        }

        PlayMusic(audioConfig.backgroundMusic);
    }

    /// <summary>
    /// Cambia el tema de fondo con crossfade. Pensado para música por room.
    /// clip null = no cambia nada. Si ya está sonando ese clip, no hace nada.
    /// </summary>
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
        musicFadeRoutine = StartCoroutine(CrossfadeTo(clip, loop));
    }

    private IEnumerator CrossfadeTo(AudioClip clip, bool loop)
    {
        // Fade out del tema actual (en tiempo no escalado: las transiciones usan timeScale = 0).
        if (musicSource.isPlaying && musicFadeDuration > 0f)
        {
            float t = 0f;
            float startVol = musicSource.volume;
            while (t < musicFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVol, 0f, t / musicFadeDuration);
                yield return null;
            }
        }

        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.volume = 0f;
        musicSource.Play();

        // Fade in del tema nuevo.
        if (musicFadeDuration > 0f)
        {
            float t = 0f;
            while (t < musicFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(0f, baseMusicVolume, t / musicFadeDuration);
                yield return null;
            }
        }

        musicSource.volume = baseMusicVolume;
        musicFadeRoutine = null;
    }

    /// <summary>
    /// Hace un fade out de la música hasta el silencio y la frena. Usado en las pantallas de
    /// Nivel Completado y Fin de Demo, donde no debe sonar nada. Corre en tiempo no escalado
    /// porque esas pantallas pausan el juego (timeScale = 0).
    /// </summary>
    public void FadeOutMusic(float duration = 1f)
    {
        if (musicSource == null) return;

        if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
        musicFadeRoutine = StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float startVol = musicSource.volume;

        if (duration > 0f && musicSource.isPlaying)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
                yield return null;
            }
        }

        musicSource.Stop();
        musicSource.volume = baseMusicVolume; // restaurar para el próximo tema
        musicFadeRoutine = null;
    }

    public void PlayJump() => PlayClip(audioConfig != null ? audioConfig.jump : null);
    public void PlayDoubleJump() => PlayClip(audioConfig != null ? (audioConfig.doubleJump != null ? audioConfig.doubleJump : audioConfig.jump) : null);
    public void PlayAttack(bool critical) => PlayClip(audioConfig != null ? (critical ? audioConfig.attackCritical : audioConfig.attack) : null);
    public void PlayPlayerDamaged() => PlayClip(audioConfig != null ? audioConfig.playerDamaged : null);
    public void PlayDash() => PlayClip(audioConfig != null ? audioConfig.dash : null);
    public void PlayEnemyAttack() => PlayClip(audioConfig != null ? audioConfig.enemyAttack : null);
    public void PlayEnemyDamaged() => PlayClip(audioConfig != null ? audioConfig.enemyDamaged : null);
    public void PlayEnemyDeath() => PlayClip(audioConfig != null ? audioConfig.enemyDeath : null);
    public void PlayButton(bool special) => PlayClip(audioConfig != null ? (special ? audioConfig.uiSpecialButton : audioConfig.uiButton) : null);

    public void PlayAbility(PlayerAbilityType abilityType)
    {
        if (audioConfig == null)
        {
            return;
        }

        switch (abilityType)
        {
            case PlayerAbilityType.DashStrike:
                PlayClip(audioConfig.dashStrike);
                break;
            case PlayerAbilityType.HealPulse:
                PlayClip(audioConfig.healPulse);
                break;
            case PlayerAbilityType.Shockwave:
                PlayClip(audioConfig.shockwave);
                break;
            case PlayerAbilityType.Berserk:
                PlayClip(audioConfig.berserk);
                break;
            case PlayerAbilityType.GuardianAura:
                PlayClip(audioConfig.guardianAura);
                break;
            case PlayerAbilityType.BladeStorm:
                PlayClip(audioConfig.bladeStorm);
                break;
        }
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip);
    }

    /// <summary>Reproduce un SFX arbitrario en 2D (sin atenuación por distancia). Útil para
    /// sonidos ambientales como el trueno, que deben sonar igual estés donde estés.</summary>
    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }
}
