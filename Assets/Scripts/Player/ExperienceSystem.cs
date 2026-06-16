using System;
using UnityEngine;

public class ExperienceSystem : MonoBehaviour
{
    [SerializeField] private int currentExperience;
    [SerializeField] private int totalExperienceEarned;

    public event Action<int, int> ExperienceChanged;

    public int CurrentExperience => currentExperience;
    public int TotalExperienceEarned => totalExperienceEarned;

    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentExperience += amount;
        totalExperienceEarned += amount;
        ExperienceChanged?.Invoke(currentExperience, totalExperienceEarned);
    }

    /// <summary>
    /// Restaura la XP a valores puntuales (usado al transferir el estado del Player entre niveles).
    /// </summary>
    public void RestoreState(int current, int total)
    {
        currentExperience = Mathf.Max(0, current);
        totalExperienceEarned = Mathf.Max(currentExperience, total);
        ExperienceChanged?.Invoke(currentExperience, totalExperienceEarned);
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0 || currentExperience < amount)
        {
            return false;
        }

        currentExperience -= amount;
        ExperienceChanged?.Invoke(currentExperience, totalExperienceEarned);
        return true;
    }
}
