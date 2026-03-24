using UnityEngine;

public enum RhythmHitResult
{
    None,
    Perfect,
    Weak
}

public class RhythmChecker : MonoBehaviour
{
    [Header("Rhythm References")]
    [SerializeField] private BeatManager beatManager;

    [Header("Timing Window")]
    [SerializeField] private float perfectWindow = 0.2f;
    [SerializeField] private bool debugLogs = true;

    private void Awake()
    {
        if (beatManager == null)
        {
            beatManager = BeatManager.Instance;
        }
    }

    public RhythmHitResult CheckTiming()
    {
        if (beatManager == null)
        {
            Debug.LogWarning("RhythmChecker needs a BeatManager reference.");
            return RhythmHitResult.Weak;
        }

        float nearestBeatTime = beatManager.GetNearestBeatTime(Time.time);
        float beatDelta = Mathf.Abs(Time.time - nearestBeatTime);
        RhythmHitResult result = beatDelta <= perfectWindow ? RhythmHitResult.Perfect : RhythmHitResult.Weak;

        if (debugLogs)
        {
            string label = result == RhythmHitResult.Perfect ? "Perfect Hit" : "Weak Hit";
            Debug.Log($"{label} | Beat delta: {beatDelta:0.000}s");
        }

        return result;
    }

    public bool IsInsidePerfectWindow()
    {
        return CheckTiming() == RhythmHitResult.Perfect;
    }

    public void Configure(BeatManager newBeatManager, float newPerfectWindow, bool shouldDebugLogs)
    {
        beatManager = newBeatManager;
        perfectWindow = Mathf.Max(0.01f, newPerfectWindow);
        debugLogs = shouldDebugLogs;
    }

    private void OnValidate()
    {
        perfectWindow = Mathf.Max(0.01f, perfectWindow);
    }
}
