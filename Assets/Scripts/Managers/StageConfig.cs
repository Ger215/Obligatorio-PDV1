using UnityEngine;

[CreateAssetMenu(fileName = "StageConfig", menuName = "Config/Stage")]
public class StageConfig : ScriptableObject
{
    public Vector2 arenaSize = new Vector2(24f, 14f);
    public float platformThickness = 0.8f;
    public float wallThickness = 1f;

    private void OnValidate()
    {
        arenaSize.x = Mathf.Max(12f, arenaSize.x);
        arenaSize.y = Mathf.Max(8f, arenaSize.y);
        platformThickness = Mathf.Max(0.25f, platformThickness);
        wallThickness = Mathf.Max(0.25f, wallThickness);
    }
}
