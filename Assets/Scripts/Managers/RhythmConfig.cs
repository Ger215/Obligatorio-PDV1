using UnityEngine;

[CreateAssetMenu(fileName = "RhythmConfig", menuName = "Config/Rhythm")]
public class RhythmConfig : ScriptableObject
{
    public float beatsPerMinute = 120f;
    public float perfectWindow = 0.2f;
    public float warningWindow = 0.35f;
    public bool debugBeatLogs = true;
    public bool debugRhythmLogs = true;

    private void OnValidate()
    {
        beatsPerMinute = Mathf.Max(1f, beatsPerMinute);
        perfectWindow = Mathf.Max(0.01f, perfectWindow);
        warningWindow = Mathf.Max(perfectWindow, warningWindow);
    }
}
