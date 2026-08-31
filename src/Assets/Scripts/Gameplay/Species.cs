using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum AttackBehavior
{
    Largest,       // target the enemy with the highest Size
    Smallest,      // target the enemy with the lowest Size
    Strongest,     // target the enemy with the highest Attack
    Weakest,       // target the enemy with the lowest current health
    PreferPredator,// target the enemy with the highest Attack first
    PreferForager, // target the enemy with the highest Forage first
    Random,        // target a random enemy
}

public class Species
{
    public const float AttackHealthToSizeRatio = 0.5f;

    public SpeciesSchema Schema;
    public string Name;
    public Sprite Portrait;
    public SpeciesGroup Group;

    // Base stats
    public int BaseHealth;
    public int BaseSize;
    public int BaseAttack;
    public int BaseDefense;
    public int BaseForage;

    // Runtime stats
    public int CurrentHealth;
    public int CurrentSize;
    public int TemporaryAttackModifier;
    public int TemporaryDefenseModifier;
    public int TemporaryForageModifier;

    public List<Part> Parts = new List<Part>();
    public List<StatusEffect> StatusEffects = new List<StatusEffect>();
    public IReadOnlyList<StatusEffect> ActiveStatusEffects => StatusEffects;

    public AttackBehavior AttackBehavior = AttackBehavior.Largest;

    // Computed stats
    public int Attack => Math.Max(0, BaseAttack + Parts.Sum(p => p.Attack) + TemporaryAttackModifier);
    public int Defense => Math.Max(0, BaseDefense + Parts.Sum(p => p.Defense) + TemporaryDefenseModifier);
    public int Forage => Math.Max(0, BaseForage + Parts.Sum(p => p.Forage) + TemporaryForageModifier);
    public int MaxHealth => BaseHealth + Parts.Sum(p => p.Health);
    public int Size => BaseSize + Parts.Sum(p => p.Size) + CurrentSize;
    public bool CanAttack => Parts.All(p => p.CanAttack);
    public bool CanDefend => Parts.All(p => p.CanDefend);
    public bool CanForage => Parts.All(p => p.CanForage);

    public IReadOnlyList<Part> GetActionParts()
    {
        return Parts.Where(part => part != null && part.HasAction).ToList();
    }

    public bool CanUseAction(Part sourcePart, SpeciesActionType actionType)
    {
        return sourcePart != null
            && Parts.Contains(sourcePart)
            && sourcePart.IsActionSelectable
            && sourcePart.ActionType == actionType;
    }

    public void Initialize()
    {
        CurrentHealth = MaxHealth;
        CurrentSize = 0;
        StatusEffects.Clear();
    }

    public bool IsAlive => CurrentHealth > 0;

    public Species PickTarget(IEnumerable<Species> enemies)
    {
        var candidates = enemies.Where(e => e.IsAlive);
        return AttackBehavior switch
        {
            AttackBehavior.Largest => candidates.OrderByDescending(e => e.Size).FirstOrDefault(),
            AttackBehavior.Smallest => candidates.OrderBy(e => e.Size).FirstOrDefault(),
            AttackBehavior.Strongest => candidates.OrderByDescending(e => e.Attack).FirstOrDefault(),
            AttackBehavior.Weakest => candidates.OrderBy(e => e.CurrentHealth).FirstOrDefault(),
            AttackBehavior.PreferPredator => candidates.OrderByDescending(e => e.Attack).FirstOrDefault(),
            AttackBehavior.PreferForager => candidates.OrderByDescending(e => e.Forage).FirstOrDefault(),
            AttackBehavior.Random => candidates.OrderBy(_ => UnityEngine.Random.value).FirstOrDefault(),
            _ => candidates.FirstOrDefault(),
        };
    }

    public void OnEncounterStart(SpeciesGroup enemy)
    {
        foreach (var part in Parts)
            part.OnEncounterStart(this, enemy);
    }

    public void OnTickStart()
    {
        ClearTemporaryStatModifiers();
        OnTurnStart();
    }

    public void OnTurnStart()
    {
        TickStatusEffects();
        if (!IsAlive)
            return;

        foreach (var part in Parts)
            part.OnTickStart(this);
    }

    public void ApplyStatusEffect(StatusEffect statusEffect)
    {
        if (statusEffect == null || statusEffect.IsExpired)
            return;

        StatusEffects.Add(statusEffect);
        statusEffect.Apply(this);
    }

    public void OnEnemyForaged(Species enemy)
    {
        foreach (var part in Parts)
            part.OnEnemyForaged(this, enemy);
    }

    public void OnTickEnd()
    {
        foreach (var part in Parts)
            part.OnTickEnd(this);
    }

    public void ForageAction()
    {
        if (!CanForage)
            return;

        foreach (var part in Parts)
            part.OnStartForageAction(this);

        int forageAmount = Forage;
        foreach (var part in Parts)
            part.OnForage(this, ref forageAmount);

        CurrentSize += Mathf.Max(0, forageAmount);

        foreach (var part in Parts)
            part.OnEndForageAction(this);
    }

    public void DefendAction()
    {
        foreach (var part in Parts)
            part.OnDefendAction(this);
    }

    public void SpecialAction(SpeciesActionType action, Species target)
    {
        foreach (var part in Parts)
            part.OnSpecialAction(this, target, action);
    }

    public bool ProvidesSpecialAction(SpeciesActionType action)
    {
        return Parts.Any(part => part.ProvidesSpecialAction(action));
    }

    public void AttackAction(Species enemy, Part sourcePart = null)
    {
        if (Attack <= 0)
            return;
        if (!CanAttack)
            return;

        foreach (var part in Parts)
            part.OnStartAttackAction(this);

        int damage = Attack;

        foreach (var part in Parts)
            part.OnAttack(this, enemy, ref damage);

        int healthBefore = enemy.CurrentHealth;
        enemy.TakeDamage(this, ref damage);
        int healthLost = Mathf.Max(0, Mathf.Min(healthBefore, healthBefore - enemy.CurrentHealth));
        CurrentSize += Mathf.FloorToInt(healthLost * Mathf.Max(0f, AttackHealthToSizeRatio));

        foreach (var part in Parts)
            part.OnAttackHit(this, enemy, healthLost, sourcePart);

        foreach (var part in Parts)
            part.OnEndAttackAction(this);
    }

    public void TakeDamage(Species attacker, ref int damage)
    {
        int defenseToUse = CanDefend ? Defense : 0;

        int mitigated = damage - defenseToUse;
        if (mitigated < 0)
            mitigated = 0;

        foreach (var part in Parts)
            part.OnDefend(this, attacker, ref mitigated);

        if (attacker != null && mitigated <= 0)
            foreach (var part in Parts)
                part.OnSuccessfulDefense(this, attacker);

        CurrentHealth -= mitigated;
    }

    public void TakeEnvironmentDamage(int damage)
    {
        int adjustedDamage = Math.Max(0, damage);
        foreach (var part in Parts)
            part.OnEnvironmentDamage(this, ref adjustedDamage);

        CurrentHealth -= Math.Max(0, adjustedDamage);
    }

    public void AddTemporaryStatModifiers(int attack, int defense, int forage)
    {
        TemporaryAttackModifier += attack;
        TemporaryDefenseModifier += defense;
        TemporaryForageModifier += forage;
    }

    public void ClearTemporaryStatModifiers()
    {
        TemporaryAttackModifier = 0;
        TemporaryDefenseModifier = 0;
        TemporaryForageModifier = 0;
    }

    void TickStatusEffects()
    {
        for (int index = StatusEffects.Count - 1; index >= 0; index--)
        {
            var statusEffect = StatusEffects[index];
            if (statusEffect == null || statusEffect.IsExpired)
            {
                StatusEffects.RemoveAt(index);
                continue;
            }

            statusEffect.Tick(this);
            if (statusEffect.IsExpired)
                StatusEffects.RemoveAt(index);
        }
    }
}