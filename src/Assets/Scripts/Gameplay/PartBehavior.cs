using System;

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

    /// <summary>Called when the owning species is hit. Modify <paramref name="damage"/> in-place.</summary>
    public virtual void OnDefend(Species self, Species attacker, ref int damage) { }

    /// <summary>Called when the owning species is foraging. Modify <paramref name="forageAmount"/> in-place.</summary>
    public virtual void OnForage(Species self, ref int forageAmount) { }

    /// <summary>Called after each tick resolves.</summary>
    public virtual void OnTickEnd(Species self) { }

    /// <summary>Returns a shallow copy of this behaviour instance.</summary>
    public PartBehaviorBase Clone() => (PartBehaviorBase)MemberwiseClone();
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
