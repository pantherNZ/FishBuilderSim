using System;
using System.Collections.Generic;
using System.Linq;

// =============================================================================
// PartBehaviorBase — abstract base for all part behaviours.
// Each subclass is [Serializable] so Unity's [SerializeReference] can store
// polymorphic instances in a PartDefinition asset.
// =============================================================================

/// Base class for a combat behaviour attached to a <see cref="Part"/>.
/// Subclass this to add new behaviours with their own data fields;
/// override only the hooks you need.
[Serializable]
public abstract class PartBehaviorBase
{
    [NonSerialized] Part _owningPart;

    public int Attack = 0;
    public int Defense = 0;
    public int Forage = 0;
    public int Health = 0;
    public int Size = 0;

    /// <summary>Called once at the start of a combat encounter.</summary>
    public virtual void OnEncounterStart(Species self, SpeciesGroup enemy) { }

    /// <summary>Called every tick before actions are resolved.</summary>
    public virtual void OnTickStart(Species self) { }
    public virtual void OnStartAttackAction(Species self) { }
    public virtual void OnEndAttackAction(Species self) { }
    public virtual void OnStartForageAction(Species self) { }
    public virtual void OnEndForageAction(Species self) { }
    public virtual void OnDefendAction(Species self) { }

    /// <summary>Called when the owning species attacks. Modify <paramref name="damage"/> in-place.</summary>
    public virtual void OnAttack(Species self, Species enemy, ref int damage) { }

    /// <summary>Called after the owning species attack deals damage.</summary>
    public virtual void OnAttackHit(Species self, Species enemy, int damage, Part sourcePart) { }

    /// <summary>Called when the owning species is hit. Modify <paramref name="damage"/> in-place.</summary>
    public virtual void OnDefend(Species self, Species attacker, ref int damage) { }

    /// <summary>Called after all defense hooks when an incoming attack is fully blocked.</summary>
    public virtual void OnSuccessfulDefense(Species self, Species attacker) { }

    /// <summary>Called when the owning species is foraging. Modify <paramref name="forageAmount"/> in-place.</summary>
    public virtual void OnForage(Species self, ref int forageAmount) { }

    /// <summary>Called when an opposing species completes a forage action.</summary>
    public virtual void OnEnemyForaged(Species self, Species enemy) { }

    /// <summary>Called when the owning species takes environmental damage.</summary>
    public virtual void OnEnvironmentDamage(Species self, ref int damage) { }

    public virtual bool ProvidesSpecialAction(SpeciesActionType action) => false;
    public virtual void OnSpecialAction(Species self, Species target, SpeciesActionType action) { }

    /// <summary>Called after each tick resolves.</summary>
    public virtual void OnTickEnd(Species self) { }

    /// <summary>Returns a shallow copy of this behaviour instance.</summary>
    public PartBehaviorBase Clone() => (PartBehaviorBase)MemberwiseClone();

    internal void AttachTo(Part part) => _owningPart = part;

    protected Part OwningPart => _owningPart;

    protected static IEnumerable<Species> LivingNeighbours(Species self)
    {
        if (self?.Group?.Members == null)
            yield break;

        foreach (var member in self.Group.Members)
        {
            if (member != null && member != self && member.IsAlive)
                yield return member;
        }
    }

    protected static Species LargestLivingMember(Species self)
    {
        Species largest = self?.IsAlive == true ? self : null;
        if (self?.Group?.Members == null)
            return largest;

        foreach (var member in self.Group.Members)
        {
            if (member != null && member.IsAlive && (largest == null || member.Size > largest.Size))
                largest = member;
        }

        return largest;
    }

    protected IEnumerable<Part> AdjacentParts(Species self)
    {
        if (self?.Parts == null || OwningPart == null)
            yield break;

        int index = self.Parts.IndexOf(OwningPart);
        if (index < 0)
            yield break;

        if (index > 0 && self.Parts[index - 1] != null)
            yield return self.Parts[index - 1];
        if (index + 1 < self.Parts.Count && self.Parts[index + 1] != null)
            yield return self.Parts[index + 1];
    }

    protected int MatchingArchetypeCount(Species self)
    {
        if (self?.Parts == null || OwningPart == null)
            return 0;

        return self.Parts.Count(part => part != null
            && part != OwningPart
            && part.Archetype == OwningPart.Archetype);
    }
}

/// Reflects a flat amount of damage back to the attacker whenever the owner
/// is hit. Equivalent to the old SpikedBody subclass.
[Serializable]
public class ReflectBehavior : PartBehaviorBase
{
    /// <summary>Amount of damage reflected back to the attacker per hit.</summary>
    public int AmountToReflect = 1;

    public override void OnDefend(Species self, Species attacker, ref int damage)
    {
        attacker.CurrentHealth -= AmountToReflect;
    }
}

/// Grants bonus attack damage while the owner's HP is below a percentage
/// threshold. Equivalent to the old Frenzy subclass.
[Serializable]
public class FrenzyBehavior : PartBehaviorBase
{
    /// <summary>Extra damage added to each attack while below the threshold.</summary>
    public int BonusDamage = 2;

    /// <summary>HP percentage (0–1) below which the frenzy bonus activates.</summary>
    public float HealthThresholdPercent = 0.5f;

    public override void OnAttack(Species self, Species enemy, ref int damage)
    {
        if (self.CurrentHealth < self.MaxHealth * HealthThresholdPercent)
            damage += BonusDamage;
    }
}


/// Grants bonus defense when the defend action is taken 
[Serializable]
public class DefendActionBehavior : PartBehaviorBase
{
    bool isBoosted = false;
    public int BonusDefense = 2;

    public override void OnTickStart(Species self)
    {
        if (!isBoosted)
            return;
        Defense -= BonusDefense;
        isBoosted = false;
    }

    public override void OnDefendAction(Species self)
    {
        if (isBoosted)
            return;
        Defense += BonusDefense;
        isBoosted = true;
    }
}

[Serializable]
public class LeechBehavior : PartBehaviorBase
{
    public int MinSizeStolen = 1;
    public int MaxSizeStolen = 2;

    public override void OnAttack(Species self, Species enemy, ref int damage)
    {
        if (enemy == null || !enemy.IsAlive || enemy.CurrentSize <= 0)
            return;

        int minimum = Math.Max(0, MinSizeStolen);
        int maximum = Math.Max(minimum, MaxSizeStolen);
        int stolen = UnityEngine.Random.Range(minimum, maximum + 1);
        stolen = Math.Min(stolen, enemy.CurrentSize);

        enemy.CurrentSize -= stolen;
        self.CurrentSize += stolen;
    }
}

[Serializable]
public class ComboAttackBehavior : PartBehaviorBase
{
    public int BonusDamagePerCombo = 1;
    public int MaxCombo = 3;

    Species _lastTarget;
    int _comboCount;

    public override void OnEncounterStart(Species self, SpeciesGroup enemy)
    {
        _lastTarget = null;
        _comboCount = 0;
    }

    public override void OnAttack(Species self, Species enemy, ref int damage)
    {
        if (enemy == null)
            return;

        _comboCount = enemy == _lastTarget ? _comboCount + 1 : 1;
        _comboCount = Math.Min(Math.Max(1, MaxCombo), _comboCount);
        _lastTarget = enemy;
        damage += Math.Max(0, _comboCount - 1) * Math.Max(0, BonusDamagePerCombo);
    }
}

[Serializable]
public class PackHunterBehavior : PartBehaviorBase
{
    public int BonusDamagePerAlly = 1;

    public override void OnAttack(Species self, Species enemy, ref int damage)
    {
        int livingAllies = 0;
        foreach (var ally in LivingNeighbours(self))
            livingAllies++;

        damage += livingAllies * Math.Max(0, BonusDamagePerAlly);
    }
}

[Serializable]
public class AmbushBehavior : PartBehaviorBase
{
    public int BonusDamage = 3;

    bool _ready = true;

    public override void OnEncounterStart(Species self, SpeciesGroup enemy)
    {
        _ready = true;
    }

    public override void OnAttack(Species self, Species enemy, ref int damage)
    {
        if (!_ready)
            return;

        damage += Math.Max(0, BonusDamage);
        _ready = false;
    }
}

[Serializable]
public class BleedOnHitBehavior : PartBehaviorBase
{
    public int Duration = BleedStatusEffect.Duration;

    public override void OnAttackHit(Species self, Species enemy, int damage, Part sourcePart)
    {
        if (enemy == null || damage <= 0 || (sourcePart != null && sourcePart != OwningPart))
            return;

        enemy.ApplyStatusEffect(new BleedStatusEffect(Duration, OwningPart?.ActionIcon));
    }
}

[Serializable]
public class SchoolingGrowthBehavior : PartBehaviorBase
{
    public int SizeGain = 1;

    public override void OnDefend(Species self, Species attacker, ref int damage)
    {
        if (attacker == null)
            return;

        int gain = Math.Max(0, SizeGain);
        self.CurrentSize += gain;
        foreach (var ally in LivingNeighbours(self))
            ally.CurrentSize += gain;
    }
}

[Serializable]
public class SharkCleanerBehavior : PartBehaviorBase
{
    public int DamageReduction = 1;

    public override void OnDefend(Species self, Species attacker, ref int damage)
    {
        var host = LargestLivingMember(self);
        if (attacker == null || host == null || host == self || host.Size <= self.Size)
            return;

        damage = Math.Max(0, damage - Math.Max(0, DamageReduction));
    }
}

[Serializable]
public class MucusCoatBehavior : PartBehaviorBase
{
    public int ShieldAmount = 3;

    bool _shieldReady = true;

    public override void OnEncounterStart(Species self, SpeciesGroup enemy)
    {
        _shieldReady = true;
    }

    public override void OnDefend(Species self, Species attacker, ref int damage)
    {
        if (!_shieldReady || attacker == null)
            return;

        damage = Math.Max(0, damage - Math.Max(0, ShieldAmount));
        _shieldReady = false;
    }
}

[Serializable]
public class WhaleCleanerBehavior : PartBehaviorBase
{
    public int SizeGain = 1;

    public override void OnEndForageAction(Species self)
    {
        var whale = LargestLivingMember(self);
        if (whale != null)
            whale.CurrentSize += Math.Max(0, SizeGain);
    }
}

[Serializable]
public class PlanktonBloomBehavior : PartBehaviorBase
{
    public int ForagesPerBloom = 2;
    public int BonusSize = 2;

    int _forageCount;

    public override void OnEncounterStart(Species self, SpeciesGroup enemy)
    {
        _forageCount = 0;
    }

    public override void OnForage(Species self, ref int forageAmount)
    {
        int interval = Math.Max(1, ForagesPerBloom);
        _forageCount++;
        if (_forageCount % interval == 0)
            forageAmount += Math.Max(0, BonusSize);
    }
}

[Serializable]
public class AdjacentBonusBehavior : PartBehaviorBase
{
    public int BonusAttack;
    public int BonusForage;

    public override void OnAttack(Species self, Species enemy, ref int damage)
    {
        int adjacentCount = AdjacentParts(self).Count();
        damage += adjacentCount * BonusAttack;
    }

    public override void OnForage(Species self, ref int forageAmount)
    {
        int adjacentCount = AdjacentParts(self).Count();
        forageAmount += adjacentCount * BonusForage;
    }
}

[Serializable]
public class ArchetypeResonanceBehavior : PartBehaviorBase
{
    public int BonusAttackPerMatch;
    public int BonusForagePerMatch;

    public override void OnAttack(Species self, Species enemy, ref int damage)
    {
        damage += MatchingArchetypeCount(self) * BonusAttackPerMatch;
    }

    public override void OnForage(Species self, ref int forageAmount)
    {
        forageAmount += MatchingArchetypeCount(self) * BonusForagePerMatch;
    }
}

[Serializable]
public class ReactiveForageBehavior : PartBehaviorBase
{
    public override void OnDefend(Species self, Species attacker, ref int damage)
    {
        if (attacker != null && self.IsAlive)
            self.ForageAction();
    }
}

[Serializable]
public class EnemyForageLeechBehavior : PartBehaviorBase
{
    public int SizeToSteal = 1;

    public override void OnEnemyForaged(Species self, Species enemy)
    {
        if (enemy == null || !enemy.IsAlive || enemy.CurrentSize <= 0)
            return;

        int stolen = Math.Min(Math.Max(0, SizeToSteal), enemy.CurrentSize);
        enemy.CurrentSize -= stolen;
        self.CurrentSize += stolen;
    }
}

[Serializable]
public class DefensiveGrowthBehavior : PartBehaviorBase
{
    public int SizeGain = 1;

    public override void OnSuccessfulDefense(Species self, Species attacker)
    {
        if (attacker != null)
            self.CurrentSize += Math.Max(0, SizeGain);
    }
}

[Serializable]
public class RandomAttackBehavior : PartBehaviorBase
{
    public int MinimumBonus = -2;
    public int MaximumBonus = 3;

    public override void OnAttack(Species self, Species enemy, ref int damage)
    {
        int minimum = Math.Min(MinimumBonus, MaximumBonus);
        int maximum = Math.Max(MinimumBonus, MaximumBonus);
        damage += UnityEngine.Random.Range(minimum, maximum + 1);
    }
}

[Serializable]
public class EnvironmentGuardBehavior : PartBehaviorBase
{
    public int DamageReduction = 2;

    public override void OnEnvironmentDamage(Species self, ref int damage)
    {
        damage = Math.Max(0, damage - Math.Max(0, DamageReduction));
    }
}

[Serializable]
public class ArmorPiercerBehavior : PartBehaviorBase
{
    public int DefenseReduction = 2;

    public override void OnAttack(Species self, Species enemy, ref int damage)
    {
        enemy?.AddTemporaryStatModifiers(0, -Math.Max(0, DefenseReduction), 0);
    }
}

[Serializable]
public class BlindActionBehavior : PartBehaviorBase
{
    public int AttackReduction = 2;
    public int DefenseReduction = 2;
    public int ForageReduction = 1;

    public override bool ProvidesSpecialAction(SpeciesActionType action)
    {
        return action == SpeciesActionType.Blind;
    }

    public override void OnSpecialAction(Species self, Species target, SpeciesActionType action)
    {
        if (action != SpeciesActionType.Blind || target == null || !target.IsAlive)
            return;

        target.AddTemporaryStatModifiers(
            -Math.Max(0, AttackReduction),
            -Math.Max(0, DefenseReduction),
            -Math.Max(0, ForageReduction));
    }
}
