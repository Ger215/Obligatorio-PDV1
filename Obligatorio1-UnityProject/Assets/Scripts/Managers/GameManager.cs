using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Wave Setup")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int startingEnemyCount = 2;
    [SerializeField] private int additionalEnemiesPerWave = 1;
    [SerializeField] private float timeBetweenWaves = 2f;

    [Header("Fallback Spawn Area")]
    [SerializeField] private float spawnRadius = 6f;

    private readonly List<HealthSystem> aliveEnemies = new List<HealthSystem>();
    private bool waitingForNextWave;

    public int CurrentWave { get; private set; }
    public int AliveEnemyCount => aliveEnemies.Count;

    private void Start()
    {
        StartNextWave();
    }

    private void StartNextWave()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("GameManager needs an enemy prefab reference.");
            return;
        }

        waitingForNextWave = false;
        CurrentWave++;

        int enemiesToSpawn = startingEnemyCount + ((CurrentWave - 1) * additionalEnemiesPerWave);
        Debug.Log($"Starting wave {CurrentWave} with {enemiesToSpawn} enemies.");

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            SpawnEnemy(i);
        }
    }

    private void SpawnEnemy(int enemyIndex)
    {
        Vector3 spawnPosition = GetSpawnPosition(enemyIndex);
        GameObject enemyInstance = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        enemyInstance.name = $"Enemy_{CurrentWave}_{enemyIndex + 1}";
        enemyInstance.SetActive(true);

        HealthSystem enemyHealth = enemyInstance.GetComponent<HealthSystem>();

        if (enemyHealth == null)
        {
            Debug.LogError("Spawned enemy is missing a HealthSystem component.");
            return;
        }

        enemyHealth.Died += HandleEnemyDeath;
        aliveEnemies.Add(enemyHealth);
    }

    private Vector3 GetSpawnPosition(int enemyIndex)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            return spawnPoint.position;
        }

        Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
        return transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);
    }

    private void HandleEnemyDeath(HealthSystem deadEnemy)
    {
        if (deadEnemy == null)
        {
            return;
        }

        deadEnemy.Died -= HandleEnemyDeath;
        aliveEnemies.Remove(deadEnemy);

        if (aliveEnemies.Count == 0 && !waitingForNextWave)
        {
            StartCoroutine(StartNextWaveAfterDelay());
        }
    }

    private IEnumerator StartNextWaveAfterDelay()
    {
        waitingForNextWave = true;
        Debug.Log($"Wave {CurrentWave} cleared. Next wave begins in {timeBetweenWaves:0.0} seconds.");
        yield return new WaitForSeconds(timeBetweenWaves);
        StartNextWave();
    }

    public void Configure(
        GameObject newEnemyPrefab,
        Transform[] newSpawnPoints,
        int newStartingEnemyCount,
        int newAdditionalEnemiesPerWave,
        float newTimeBetweenWaves,
        float newSpawnRadius)
    {
        enemyPrefab = newEnemyPrefab;
        spawnPoints = newSpawnPoints;
        startingEnemyCount = Mathf.Max(1, newStartingEnemyCount);
        additionalEnemiesPerWave = Mathf.Max(0, newAdditionalEnemiesPerWave);
        timeBetweenWaves = Mathf.Max(0f, newTimeBetweenWaves);
        spawnRadius = Mathf.Max(1f, newSpawnRadius);
    }

    private void OnValidate()
    {
        startingEnemyCount = Mathf.Max(1, startingEnemyCount);
        additionalEnemiesPerWave = Mathf.Max(0, additionalEnemiesPerWave);
        timeBetweenWaves = Mathf.Max(0f, timeBetweenWaves);
        spawnRadius = Mathf.Max(1f, spawnRadius);
    }
}
