using System;
using System.Collections.Generic;

public enum PartRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4,
}

public class Part
{
    public string Name;

    public PartRarity Rarity = PartRarity.Common;

    // Cost in mutation points to remove this part once equipped
    public int MutationCost;

    // Flat stat contributions
    public int Attack;
    public int Defense;
    public int Forage;
    public int Health;
    public int Size;

    /// <summary>
    /// Polymorphic list of combat behaviours attached to this part.
    /// Each entry handles its own hooks — no central switch statement required.
    /// </summary>
    public List<PartBehaviorBase> Behaviors = new();

    // Called once when combat starts
    public virtual void OnCombatStart(Species self, Species enemy)
    {
        foreach (var b in Behaviors) b.OnCombatStart(self, enemy);
    }

    // Called every tick BEFORE actions
    public virtual void OnTickStart(Species self, Species enemy)
    {
        foreach (var b in Behaviors) b.OnTickStart(self, enemy);
    }

    // Called when attacking
    public virtual void OnAttack(Species self, Species enemy, ref int damage)
    {
        foreach (var b in Behaviors) b.OnAttack(self, enemy, ref damage);
    }

    // Called when taking damage
    public virtual void OnDefend(Species self, Species attacker, ref int damage)
    {
        foreach (var b in Behaviors) b.OnDefend(self, attacker, ref damage);
    }

    // Called after tick resolves
    public virtual void OnTickEnd(Species self, Species enemy)
    {
        foreach (var b in Behaviors) b.OnTickEnd(self, enemy);
    }
}