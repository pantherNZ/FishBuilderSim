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
    public int MinDepth = 0;
    public int MaxDepth = 0;

    [Tooltip("The group of species the player fights in this encounter.")]
    public SpeciesGroupSchema EnemyGroup;

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fresh <see cref="Encounter"/> runtime instance from this schema.
    /// The enemy group and all its member species are also freshly instantiated.
    /// </summary>
    public Encounter CreateEncounter()
    {
        return new Encounter(EnemyGroup.CreateSpeciesGroup());
    }
}
