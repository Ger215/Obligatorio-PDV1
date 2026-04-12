using UnityEngine;

public class AudioManager : SingletonBehaviour<AudioManager>
{
    [SerializeField] private GameAudioConfig audioConfig;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    public void PlayBackgroundMusic()
    {
        if (audioConfig == null || musicSource == null || audioConfig.backgroundMusic == null)
        {
            return;
        }

        if (musicSource.clip == audioConfig.backgroundMusic && musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = audioConfig.backgroundMusic;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void PlayJump() => PlayClip(audioConfig != null ? audioConfig.jump : null);
    public void PlayDoubleJump() => PlayClip(audioConfig != null ? (audioConfig.doubleJump != null ? audioConfig.doubleJump : audioConfig.jump) : null);
    public void PlayAttack(bool critical) => PlayClip(audioConfig != null ? (critical ? audioConfig.attackCritical : audioConfig.attack) : null);
    public void PlayPlayerDamaged() => PlayClip(audioConfig != null ? audioConfig.playerDamaged : null);
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
}
