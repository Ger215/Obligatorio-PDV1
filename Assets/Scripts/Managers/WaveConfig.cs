using UnityEngine;

[System.Serializable]
public class WaveDefinition
{
    public EnemyType[] allowedTypes = { EnemyType.Chaser };
}

[CreateAssetMenu(fileName = "WaveConfig", menuName = "Config/Waves")]
public class WaveConfig : ScriptableObject
{
    [Header("Spawn")]
    public int startingEnemyCount = 2;
    public int additionalEnemiesPerWave = 1;
    public int maxEnemiesPerWave = 6;
    public float timeBetweenWaves = 2f;

    [Header("Scaling")]
    [Tooltip("La escala de dificultad empieza a aplicar desde esta oleada (las anteriores son tutorial)")]
    public int scalingStartWave = 4;
    public float enemyHealthMultiplierPerWave = 0.10f;
    public float enemyDamageMultiplierPerWave = 0.07f;
    public float enemyMoveSpeedMultiplierPerWave = 0.03f;

    [Header("Rewards")]
    public int bonusExperiencePerWave = 1;
    public int offeredAbilitiesCount = 3;
    [Tooltip("HP que recupera el jugador al terminar cada oleada")]
    public int hpRewardPerWave = 2;

    [Header("Wave Definitions")]
    [Tooltip("Composicion explicita de cada oleada por indice. Las oleadas sin definicion usan fallbackTypes")]
    public WaveDefinition[] waveDefinitions;
    [Tooltip("Pool de tipos de enemigo para oleadas sin definicion explicita (late game)")]
    public EnemyType[] fallbackTypes = { EnemyType.Chaser, EnemyType.Fast, EnemyType.Ranged, EnemyType.Tank };

    private void OnValidate()
    {
        startingEnemyCount = Mathf.Max(1, startingEnemyCount);
        additionalEnemiesPerWave = Mathf.Max(0, additionalEnemiesPerWave);
        maxEnemiesPerWave = Mathf.Max(1, maxEnemiesPerWave);
        timeBetweenWaves = Mathf.Max(0f, timeBetweenWaves);
        scalingStartWave = Mathf.Max(1, scalingStartWave);
        enemyHealthMultiplierPerWave = Mathf.Max(0f, enemyHealthMultiplierPerWave);
        enemyDamageMultiplierPerWave = Mathf.Max(0f, enemyDamageMultiplierPerWave);
        enemyMoveSpeedMultiplierPerWave = Mathf.Max(0f, enemyMoveSpeedMultiplierPerWave);
        bonusExperiencePerWave = Mathf.Max(0, bonusExperiencePerWave);
        offeredAbilitiesCount = Mathf.Clamp(offeredAbilitiesCount, 1, 3);
        hpRewardPerWave = Mathf.Max(0, hpRewardPerWave);
    }
}
