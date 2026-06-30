using UnityEngine;

/// <summary>
/// Vuelca el estado guardado en <see cref="PlayerStateStore"/> (vida, XP y habilidades) sobre el
/// Player de esta escena al arrancar, y lo consume. Sirve para escenas que NO tienen GameManager
/// (como la Shop), donde nadie más se encarga de restaurar el estado que viene del nivel anterior.
///
/// No usar en escenas con GameManager: ahí ya lo hace el propio GameManager y restaurar dos veces
/// duplicaría las habilidades.
/// </summary>
public class PlayerStateRestorer : MonoBehaviour
{
    [Tooltip("Player sobre el que volcar el estado. Si lo dejás vacío, usa PlayerController.Instance.")]
    [SerializeField] private PlayerController player;

    private void Start()
    {
        if (!PlayerStateStore.HasState) return;

        if (player == null) player = PlayerController.Instance;
        if (player == null) return;

        HealthSystem health = player.HealthSystem;
        ExperienceSystem experience = player.GetComponent<ExperienceSystem>();
        PlayerAbilityController abilities = player.GetComponent<PlayerAbilityController>();

        health?.RestoreHealth(PlayerStateStore.CurrentHealth);
        experience?.RestoreState(PlayerStateStore.CurrentExperience, PlayerStateStore.TotalExperienceEarned);

        if (abilities != null)
        {
            foreach (PlayerAbilityDefinition ability in PlayerStateStore.LearnedAbilities)
            {
                if (ability != null)
                {
                    abilities.LearnAbility(ability);
                }
            }
        }

        PlayerStateStore.Clear();
    }
}
