using Schema;
using UnityEngine;

/// <summary>
/// ScriptableObject asset that describes a single encounter on the world map.
/// Create assets via: Right-click → Create → FishBuilderSim → Encounter Schema.
///
/// <c>id</c> (inherited from <see cref="BaseDataSchema"/>) is auto-populated
/// from the asset name.
///
/// Call <see cref="CreateEncounter"/> at runtime to obtain a fresh
/// <see cref="Encounter"/> simulation instance.
/// </summary>
[CreateAssetMenu(fileName = "NewEncounter", menuName = "FishBuilderSim/Encounter Schema")]
public class EncounterSchema : BaseDataSchema
{
    [Header("Encounter")]
    [Tooltip("Number used for ordering and difficulty scaling. 1-based.")]
    public int EncounterNumber = 1;

    [Header("Enemy Group")]
    [Tooltip("The group of species the player fights in this encounter.")]
    public SpeciesGroupSchema EnemyGroup;

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fresh <see cref="Encounter"/> runtime instance from this schema.
    /// The enemy group and all its member species are also freshly instantiated.
    /// </summary>
    public Encounter CreateEncounter()
    {
        if (EnemyGroup == null)
        {
            Debug.LogWarning($"[EncounterSchema] '{name}' has no EnemyGroup assigned. Creating encounter with an empty group.");
            return new Encounter(EncounterNumber, new SpeciesGroup(id));
        }

        return new Encounter(EncounterNumber, EnemyGroup.CreateSpeciesGroup());
    }
}
