using System;

// =============================================================================
// PartBehaviorBase — abstract base for all part behaviours.
// Each subclass is [Serializable] so Unity's [SerializeReference] can store
// polymorphic instances in a PartDefinition asset.
// =============================================================================

/// <summary>
/// Base class for a combat behaviour attached to a <see cref="Part"/>.
/// Subclass this to add new behaviours with their own data fields;
/// override only the hooks you need.
/// </summary>
[Serializable]
public abstract class PartBehaviorBase
{
    /// <summary>Called once at the start of a combat encounter.</summary>
    public virtual void OnCombatStart(Species self, Species enemy) { }

    /// <summary>Called every tick before actions are resolved.</summary>
    public virtual void OnTickStart(Species self, Species enemy) { }

    /// <summary>Called when the owning species attacks. Modify <paramref name="damage"/> in-place.</summary>
    public virtual void OnAttack(Species self, Species enemy, ref int damage) { }

    /// <summary>Called when the owning species is hit. Modify <paramref name="damage"/> in-place.</summary>
    public virtual void OnDefend(Species self, Species attacker, ref int damage) { }

    /// <summary>Called after each tick resolves.</summary>
    public virtual void OnTickEnd(Species self, Species enemy) { }
}

// =============================================================================
// Concrete behaviours
// =============================================================================

/// <summary>
/// Reflects a flat amount of damage back to the attacker whenever the owner
/// is hit. Equivalent to the old SpikedBody subclass.
/// </summary>
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

/// <summary>
/// Grants bonus attack damage while the owner's HP is below a percentage
/// threshold. Equivalent to the old Frenzy subclass.
/// </summary>
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
