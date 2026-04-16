using UnityEngine;

[CreateAssetMenu(fileName = "GameAudioConfig", menuName = "Config/Audio/Game Audio")]
public class GameAudioConfig : ScriptableObject
{
    public AudioClip backgroundMusic;
    public AudioClip jump;
    public AudioClip doubleJump;
    public AudioClip attack;
    public AudioClip attackCritical;
    public AudioClip playerDamaged;
    public AudioClip dash;
    public AudioClip enemyAttack;
    public AudioClip enemyDamaged;
    public AudioClip enemyDeath;
    public AudioClip uiButton;
    public AudioClip uiSpecialButton;
    public AudioClip dashStrike;
    public AudioClip healPulse;
    public AudioClip shockwave;
    public AudioClip berserk;
    public AudioClip guardianAura;
    public AudioClip bladeStorm;
}
