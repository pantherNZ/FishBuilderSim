using System;
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
    public int Weight = 1;

    [Tooltip("If enabled, this encounter is reserved for starting encounter sequences and excluded from normal random map generation.")]
    public bool StartEncounter = false;

    [Tooltip("The group of species the player fights in this encounter.")]
    public SpeciesGroupSchema EnemyGroup;

    [Tooltip("Optional single-species enemy. Used when EnemyGroup is empty.")]
    public SpeciesSchema EnemySpecies;

    public bool HasEnemyDefinition => EnemyGroup != null || EnemySpecies != null;

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fresh <see cref="Encounter"/> runtime instance from this schema.
    /// Supports either a full enemy group or a single enemy species.
    /// </summary>
    public Encounter CreateEncounter()
    {
        if (EnemyGroup != null)
            return new Encounter(EnemyGroup.CreateSpeciesGroup());

        if (EnemySpecies != null)
        {
            var group = new SpeciesGroup(id);
            group.Add(EnemySpecies.CreateSpecies());
            return new Encounter(group);
        }

        throw new InvalidOperationException($"EncounterSchema '{name}' has no EnemyGroup or EnemySpecies configured.");
    }
}
