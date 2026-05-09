using System;
using System.Collections.Generic;
using Schema;
using UnityEngine;

/// <summary>
/// ScriptableObject asset that fully describes a <see cref="Part"/>.
/// Create assets via: Right-click → Create → FishBuilderSim → Part Schema.
///
/// <c>id</c> (inherited from <see cref="BaseDataSchema"/>) is auto-populated
/// from the asset name and used as a stable fallback identity.
/// <c>displayName</c> is used for the runtime/UI-visible part name.
///
/// At runtime call <see cref="CreatePart"/> to obtain a fresh <see cref="Part"/>
/// instance ready for the simulation layer.
/// </summary>
[CreateAssetMenu(fileName = "NewPart", menuName = "FishBuilderSim/Part Schema")]
public class PartSchema : BaseDataSchema
{
    public string displayName;

    [Header("Rarity")]
    [Tooltip("Rarity tier — controls reward weighting and card border colour.")]
    public PartRarity Rarity = PartRarity.Common;

    [Tooltip("Mutation-point cost to unequip this part mid-run.")]
    public int MutationCost;

    [Header("Stats")]
    [Tooltip("Flat attack bonus added to the species attack stat.")]
    public int Attack;

    [Tooltip("Flat defense bonus (reduces incoming damage by this amount).")]
    public int Defense;

    [Tooltip("Flat foraging bonus (increases food gathered each tick).")]
    public int Forage;

    [Tooltip("Flat health bonus added to max HP.")]
    public int Health;

    [Tooltip("Flat size bonus added to the species size stat.")]
    public int Size;

    [Header("Behaviors")]
    [Tooltip("Special combat behaviours active while this part is equipped.")]
    [SerializeReference, SerializeReferenceDropdown]
    public List<PartBehaviorBase> Behaviors = new();

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fresh <see cref="Part"/> runtime instance from this schema.
    /// The returned part is independent of this asset — safe to mutate at runtime.
    /// </summary>
    public Part CreatePart()
    {
        return new Part
        {
            Schema = this,
            Name = string.IsNullOrWhiteSpace(displayName) ? id : displayName,
            Rarity = Rarity,
            MutationCost = MutationCost,
            BaseAttack = Attack,
            BaseDefense = Defense,
            BaseForage = Forage,
            BaseHealth = Health,
            BaseSize = Size,
            // Deep-copy each behavior so runtime instances are independent of the asset.
            Behaviors = Behaviors.ConvertAll(b => b.Clone()),
        };
    }


    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}
