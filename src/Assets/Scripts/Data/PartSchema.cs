using System;
using System.Collections.Generic;
using Runtime.Game;
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
    [TextArea]
    public string Description;

    [Header("Rarity")]
    [Tooltip("Rarity tier — controls reward weighting and card border colour.")]
    public PartRarity Rarity = PartRarity.Common;

    [Tooltip("Part archetype used by synergy behaviors.")]
    public PartArchetype Archetype = PartArchetype.Utility;

    [Header("Skill")]
    [Tooltip("Name shown for the skill granted by this part.")]
    public string ActionName;

    [Tooltip("Icon shown for the skill granted by this part.")]
    public Sprite ActionIcon;

    [Tooltip("Artwork displayed for this part in the inventory and species editor.")]
    public Sprite PartSprite;
    public PartSize CircleSize = PartSize.Medium;

    [Tooltip("Code-defined action represented by this part's skill.")]
    public SpeciesActionType ActionType = SpeciesActionType.None;

    [Tooltip("Passive skills are shown in the battle skill bar but cannot be selected.")]
    public bool IsPassive;

    [Min(0), Tooltip("Energy spent when this active skill is used. Each fish has 5 energy per turn.")]
    public int EnergyCost = 1;

    [Min(0), Tooltip("0 allows reuse this turn; 1 allows reuse next turn; 2 requires waiting two turn starts.")]
    public int CooldownTurns;

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

    public bool MonsterOnly;

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
            Description = Description,
            Rarity = Rarity,
            Archetype = Archetype,
            ActionName = ActionName,
            ActionIcon = ActionIcon,
            PartSprite = PartSprite,
            Radius = Mathf.Clamp(
                GlobalConstantsHandler.Constants.partRadii[CircleSize],
                0.01f,
                0.95f),
            ActionType = ActionType,
            IsPassive = IsPassive,
            EnergyCost = Math.Max(0, EnergyCost),
            CooldownTurns = Math.Max(0, CooldownTurns),
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
