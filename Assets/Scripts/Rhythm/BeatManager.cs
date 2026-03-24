using UnityEngine;

public class BeatManager : MonoBehaviour
{
    [Header("Beat Settings")]
    [SerializeField] private float beatsPerMinute = 120f;
    [SerializeField] private float beatOffset;
    [SerializeField] private bool debugBeatLogs = true;

    private float songStartTime;
    private int lastLoggedBeatIndex = -1;

    public float BeatsPerMinute => beatsPerMinute;
    public float SecondsPerBeat => 60f / beatsPerMinute;
    public float SongStartTime => songStartTime;

    private void Awake()
    {
        songStartTime = Time.time + beatOffset;
    }

    private void Update()
    {
        if (!debugBeatLogs || Time.time < songStartTime)
        {
            return;
        }

        while (Time.time >= songStartTime + ((lastLoggedBeatIndex + 1) * SecondsPerBeat))
        {
            lastLoggedBeatIndex++;
            Debug.Log($"Beat {lastLoggedBeatIndex} at {songStartTime + (lastLoggedBeatIndex * SecondsPerBeat):0.00}s");
        }
    }

    public float GetNearestBeatTime(float currentTime)
    {
        if (currentTime <= songStartTime)
        {
            return songStartTime;
        }

        float beatsSinceStart = (currentTime - songStartTime) / SecondsPerBeat;
        float nearestBeatIndex = Mathf.Round(beatsSinceStart);
        return songStartTime + (nearestBeatIndex * SecondsPerBeat);
    }

    public float GetAbsoluteBeatDelta(float currentTime)
    {
        return Mathf.Abs(currentTime - GetNearestBeatTime(currentTime));
    }

    public float GetTimeToNextBeat(float currentTime)
    {
        if (currentTime <= songStartTime)
        {
            return songStartTime - currentTime;
        }

        float beatsSinceStart = (currentTime - songStartTime) / SecondsPerBeat;
        float nextBeatIndex = Mathf.Ceil(beatsSinceStart);
        float nextBeatTime = songStartTime + (nextBeatIndex * SecondsPerBeat);
        return nextBeatTime - currentTime;
    }

    public void Configure(float newBeatsPerMinute, float newBeatOffset, bool shouldDebugBeatLogs)
    {
        beatsPerMinute = Mathf.Max(1f, newBeatsPerMinute);
        beatOffset = newBeatOffset;
        debugBeatLogs = shouldDebugBeatLogs;
        songStartTime = Time.time + beatOffset;
        lastLoggedBeatIndex = -1;
    }

    private void OnValidate()
    {
        beatsPerMinute = Mathf.Max(1f, beatsPerMinute);
    }
}
