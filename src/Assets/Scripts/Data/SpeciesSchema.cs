using System.Collections.Generic;
using Schema;
using UnityEngine;

/// <summary>
/// ScriptableObject asset that fully describes a <see cref="Species"/>.
/// Create assets via: Right-click → Create → FishBuilderSim → Species Schema.
///
/// <c>id</c> (inherited from <see cref="BaseDataSchema"/>) is auto-populated
/// from the asset name and used as the runtime species name.
///
/// Call <see cref="CreateSpecies"/> at runtime to obtain a fresh simulation
/// instance ready for combat.
/// </summary>
[CreateAssetMenu(fileName = "NewSpecies", menuName = "FishBuilderSim/Species Schema")]
public class SpeciesSchema : BaseDataSchema
{
    [Header("Base Stats")]
    [Tooltip("Starting maximum health points.")]
    public int BaseHealth = 10;

    [Tooltip("Starting size. Affects targeting and certain part effects.")]
    public int BaseSize = 1;

    [Tooltip("Flat attack damage dealt each tick.")]
    public int BaseAttack;

    [Tooltip("Flat damage reduction applied to each incoming hit.")]
    public int BaseDefense;

    [Tooltip("Food gathered each tick, which grows size over time.")]
    public int BaseForage = 1;

    [Header("Behaviour")]
    [Tooltip("Determines which enemy this species prioritises when picking a target.")]
    public AttackBehavior AttackBehavior = AttackBehavior.Largest;

    [Header("Starting Parts")]
    [Tooltip("Parts this species begins combat with. Each entry is instantiated via its CreatePart factory.")]
    public List<PartSchema> StartingParts = new();

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fresh <see cref="Species"/> runtime instance from this schema.
    /// The returned species is fully independent of this asset.
    /// </summary>
    public Species CreateSpecies()
    {
        var species = new Species
        {
            Name = id,
            BaseHealth = BaseHealth,
            BaseSize = BaseSize,
            BaseAttack = BaseAttack,
            BaseDefense = BaseDefense,
            BaseForage = BaseForage,
            AttackBehavior = AttackBehavior,
        };

        foreach (var partSchema in StartingParts)
        {
            if (partSchema != null)
                species.Parts.Add(partSchema.CreatePart());
        }

        return species;
    }
}
