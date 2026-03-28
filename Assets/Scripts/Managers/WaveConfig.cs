using UnityEngine;

[CreateAssetMenu(fileName = "WaveConfig", menuName = "Config/Waves")]
public class WaveConfig : ScriptableObject
{
    public int startingEnemyCount = 2;
    public int additionalEnemiesPerWave = 1;
    public float timeBetweenWaves = 2f;
    public float enemyHealthMultiplierPerWave = 0.18f;
    public float enemyDamageMultiplierPerWave = 0.12f;
    public float enemyMoveSpeedMultiplierPerWave = 0.05f;
    public int bonusExperiencePerWave = 1;
    public int offeredAbilitiesCount = 3;

    private void OnValidate()
    {
        startingEnemyCount = Mathf.Max(1, startingEnemyCount);
        additionalEnemiesPerWave = Mathf.Max(0, additionalEnemiesPerWave);
        timeBetweenWaves = Mathf.Max(0f, timeBetweenWaves);
        enemyHealthMultiplierPerWave = Mathf.Max(0f, enemyHealthMultiplierPerWave);
        enemyDamageMultiplierPerWave = Mathf.Max(0f, enemyDamageMultiplierPerWave);
        enemyMoveSpeedMultiplierPerWave = Mathf.Max(0f, enemyMoveSpeedMultiplierPerWave);
        bonusExperiencePerWave = Mathf.Max(0, bonusExperiencePerWave);
        offeredAbilitiesCount = Mathf.Clamp(offeredAbilitiesCount, 1, 3);
    }
}
