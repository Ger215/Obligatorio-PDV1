using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerAbilityCatalog", menuName = "Config/Abilities/Catalog")]
public class PlayerAbilityCatalog : ScriptableObject
{
    public PlayerAbilityDefinition[] abilities;

    public List<PlayerAbilityDefinition> GetUnlearnedAbilities(IReadOnlyList<PlayerAbilityDefinition> learnedAbilities)
    {
        List<PlayerAbilityDefinition> unlearnedAbilities = new List<PlayerAbilityDefinition>();

        if (abilities == null)
        {
            return unlearnedAbilities;
        }

        for (int i = 0; i < abilities.Length; i++)
        {
            PlayerAbilityDefinition ability = abilities[i];

            if (ability == null || IsLearned(ability, learnedAbilities))
            {
                continue;
            }

            unlearnedAbilities.Add(ability);
        }

        return unlearnedAbilities;
    }

    private static bool IsLearned(PlayerAbilityDefinition ability, IReadOnlyList<PlayerAbilityDefinition> learnedAbilities)
    {
        if (learnedAbilities == null)
        {
            return false;
        }

        for (int i = 0; i < learnedAbilities.Count; i++)
        {
            if (learnedAbilities[i] == ability)
            {
                return true;
            }
        }

        return false;
    }
}
