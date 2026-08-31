using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum PartRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4,
}

public enum PartArchetype
{
    Utility,
    Attack,
    Defense,
    Foraging,
}

public class Part
{
    public PartSchema Schema;
    public string Name;
    public string Description;

    public PartRarity Rarity = PartRarity.Common;
    public PartArchetype Archetype = PartArchetype.Utility;

    // Skill metadata granted by this part.
    public string ActionName;
    public Sprite ActionIcon;
    public SpeciesActionType ActionType = SpeciesActionType.None;
    public bool IsPassive;
    public bool HasAction => ActionType != SpeciesActionType.None;
    public bool IsActionSelectable => HasAction && !IsPassive;

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
        {
            b.AttachTo(this);
            b.OnEncounterStart(self, enemy);
        }
    }

    public virtual void OnTickStart(Species self)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnTickStart(self);
        }
    }

    public virtual void OnStartForageAction(Species self)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnStartForageAction(self);
        }
    }

    public virtual void OnEndForageAction(Species self)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnEndForageAction(self);
        }
    }

    public virtual void OnStartAttackAction(Species self)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnStartAttackAction(self);
        }
    }


    public virtual void OnEndAttackAction(Species self)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnEndAttackAction(self);
        }
    }

    public virtual void OnDefendAction(Species self)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnDefendAction(self);
        }
    }

    public virtual void OnAttack(Species self, Species enemy, ref int damage)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnAttack(self, enemy, ref damage);
        }
    }

    public virtual void OnAttackHit(Species self, Species enemy, int damage, Part sourcePart)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnAttackHit(self, enemy, damage, sourcePart);
        }
    }

    public virtual void OnDefend(Species self, Species attacker, ref int damage)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnDefend(self, attacker, ref damage);
        }
    }

    public virtual void OnSuccessfulDefense(Species self, Species attacker)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnSuccessfulDefense(self, attacker);
        }
    }

    public virtual void OnForage(Species self, ref int forageAmount)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnForage(self, ref forageAmount);
        }
    }

    public virtual void OnEnemyForaged(Species self, Species enemy)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnEnemyForaged(self, enemy);
        }
    }

    public virtual void OnEnvironmentDamage(Species self, ref int damage)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnEnvironmentDamage(self, ref damage);
        }
    }

    public bool ProvidesSpecialAction(SpeciesActionType action)
    {
        foreach (var b in Behaviors)
            if (b.ProvidesSpecialAction(action))
                return true;
        return false;
    }

    public virtual void OnSpecialAction(Species self, Species target, SpeciesActionType action)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnSpecialAction(self, target, action);
        }
    }

    public virtual void OnTickEnd(Species self)
    {
        foreach (var b in Behaviors)
        {
            b.AttachTo(this);
            b.OnTickEnd(self);
        }
    }
}