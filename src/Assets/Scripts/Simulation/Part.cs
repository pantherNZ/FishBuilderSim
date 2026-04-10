using System;

public enum PartRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4,
}

public abstract class Part
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

    // Called once when combat starts
    public virtual void OnCombatStart(Species self, Species enemy) { }

    // Called every tick BEFORE actions
    public virtual void OnTickStart(Species self, Species enemy) { }

    // Called when attacking
    public virtual void OnAttack(Species self, Species enemy, ref int damage) { }

    // Called when taking damage
    public virtual void OnDefend(Species self, Species enemy, ref int damage) { }

    // Called after tick resolves
    public virtual void OnTickEnd(Species self, Species enemy) { }
}