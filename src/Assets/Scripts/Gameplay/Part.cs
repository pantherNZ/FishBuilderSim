using System;
using System.Collections.Generic;
using System.Linq;

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
    public string Description;

    public PartRarity Rarity = PartRarity.Common;

    // Cost in mutation points to remove this part once equipped
    public int MutationCost;

    // Flat stat contributions
    public int BaseAttack = 0;
    public int BaseDefense = 0;
    public int BaseForage = 0;
    public int BaseHealth = 0;
    public int BaseSize = 0;
    public int Attack => BaseAttack + Behaviors.Sum(p => p.Attack);
    public int Defense => BaseDefense + Behaviors.Sum(p => p.Defense);
    public int Forage => BaseForage + Behaviors.Sum(p => p.Forage);
    public int Health => BaseHealth + Behaviors.Sum(p => p.Health);
    public int Size => BaseSize + Behaviors.Sum(p => p.Size);
    public bool CanAttack = true;
    public bool CanDefend = true;
    public bool CanForage = true;

    /// <summary>
    /// Polymorphic list of combat behaviours attached to this part.
    /// Each entry handles its own hooks — no central switch statement required.
    /// </summary>
    public List<PartBehaviorBase> Behaviors = new();

    public virtual void OnEncounterStart(Species self, SpeciesGroup enemy)
    {
        foreach (var b in Behaviors)
            b.OnEncounterStart(self, enemy);
    }

    public virtual void OnTickStart(Species self)
    {
        foreach (var b in Behaviors)
            b.OnTickStart(self);
    }

    public virtual void OnStartForageAction(Species self)
    {
        foreach (var b in Behaviors)
            b.OnStartForageAction(self);
    }

    public virtual void OnEndForageAction(Species self)
    {
        foreach (var b in Behaviors)
            b.OnEndForageAction(self);
    }

    public virtual void OnStartAttackAction(Species self)
    {
        foreach (var b in Behaviors)
            b.OnStartAttackAction(self);
    }


    public virtual void OnEndAttackAction(Species self)
    {
        foreach (var b in Behaviors)
            b.OnEndAttackAction(self);
    }

    public virtual void OnDefendAction(Species self)
    {
        foreach (var b in Behaviors)
            b.OnDefendAction(self);
    }

    public virtual void OnAttack(Species self, Species enemy, ref int damage)
    {
        foreach (var b in Behaviors)
            b.OnAttack(self, enemy, ref damage);
    }

    public virtual void OnDefend(Species self, Species attacker, ref int damage)
    {
        foreach (var b in Behaviors)
            b.OnDefend(self, attacker, ref damage);
    }

    public virtual void OnForage(Species self, ref int forageAmount)
    {
        foreach (var b in Behaviors)
            b.OnForage(self, ref forageAmount);
    }

    public virtual void OnTickEnd(Species self)
    {
        foreach (var b in Behaviors)
            b.OnTickEnd(self);
    }
}