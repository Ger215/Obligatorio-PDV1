using System.Collections.Generic;

/// <summary>
/// Almacén estático del estado del Player entre escenas. No es un MonoBehaviour: vive en memoria
/// mientras dure la sesión de juego, así sobrevive a los cambios de escena sin necesitar
/// DontDestroyOnLoad (cada escena tiene su propio Player, GameManager y HUD).
///
/// Flujo: el GameManager del nivel que termina llama a Capture() justo antes de cargar el próximo
/// nivel; el GameManager del nivel que arranca llama a las propiedades + Clear() para volcar el
/// estado sobre su propio Player. Es de un solo uso (se consume al restaurar) para evitar arrastrar
/// estado viejo si después se reinicia el nivel.
/// </summary>
public static class PlayerStateStore
{
    public static bool HasState { get; private set; }
    public static int CurrentHealth { get; private set; }
    public static int CurrentExperience { get; private set; }
    public static int TotalExperienceEarned { get; private set; }
    public static readonly List<PlayerAbilityDefinition> LearnedAbilities = new List<PlayerAbilityDefinition>();

    public static void Capture(int currentHealth, int currentExperience, int totalExperienceEarned, IReadOnlyList<PlayerAbilityDefinition> learnedAbilities)
    {
        CurrentHealth = currentHealth;
        CurrentExperience = currentExperience;
        TotalExperienceEarned = totalExperienceEarned;

        LearnedAbilities.Clear();
        if (learnedAbilities != null)
        {
            foreach (PlayerAbilityDefinition ability in learnedAbilities)
            {
                if (ability != null)
                {
                    LearnedAbilities.Add(ability);
                }
            }
        }

        HasState = true;
    }

    public static void Clear()
    {
        HasState = false;
        CurrentHealth = 0;
        CurrentExperience = 0;
        TotalExperienceEarned = 0;
        LearnedAbilities.Clear();
    }
}
