using UnityEngine;

[CreateAssetMenu(fileName = "HealthConfig", menuName = "Config/Combat/Health")]
public class HealthConfig : ScriptableObject
{
    public int maxHealth = 5;
    public bool destroyOnDeath = true;
    public bool deactivateOnDeath;
    public float destroyDelay;

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        destroyDelay = Mathf.Max(0f, destroyDelay);
    }
}
