using System.Collections.Generic;
using Schema;
using UnityEngine;

/// <summary>
/// ScriptableObject asset that describes a group of species that fight together.
/// Create assets via: Right-click → Create → FishBuilderSim → Species Group Schema.
///
/// <c>id</c> (inherited from <see cref="BaseDataSchema"/>) is auto-populated
/// from the asset name and used as the runtime group name.
///
/// Call <see cref="CreateSpeciesGroup"/> at runtime to obtain a fresh
/// <see cref="SpeciesGroup"/> simulation instance.
/// </summary>
[CreateAssetMenu(fileName = "NewSpeciesGroup", menuName = "FishBuilderSim/Species Group Schema")]
public class SpeciesGroupSchema : BaseDataSchema
{
    [Header("Members")]
    [Tooltip("The species that make up this group. Order is preserved.")]
    public List<SpeciesSchema> Members = new();

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fresh <see cref="SpeciesGroup"/> runtime instance from this schema.
    /// Each member species is also freshly instantiated.
    /// </summary>
    public SpeciesGroup CreateSpeciesGroup()
    {
        var group = new SpeciesGroup(id);

        foreach (var memberSchema in Members)
        {
            if (memberSchema != null)
                group.Add(memberSchema.CreateSpecies());
        }

        return group;
    }
}
