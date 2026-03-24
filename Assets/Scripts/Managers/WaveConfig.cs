using UnityEngine;

[CreateAssetMenu(fileName = "WaveConfig", menuName = "Config/Waves")]
public class WaveConfig : ScriptableObject
{
    public int startingEnemyCount = 2;
    public int additionalEnemiesPerWave = 1;
    public float timeBetweenWaves = 2f;

    private void OnValidate()
    {
        startingEnemyCount = Mathf.Max(1, startingEnemyCount);
        additionalEnemiesPerWave = Mathf.Max(0, additionalEnemiesPerWave);
        timeBetweenWaves = Mathf.Max(0f, timeBetweenWaves);
    }
}
