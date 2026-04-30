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
    public string Name;
    public Sprite Portrait;

    // Base stats
    public int BaseHealth;
    public int BaseSize;
    public int BaseAttack;
    public int BaseDefense;
    public int BaseForage;

    // Runtime stats
    public int CurrentHealth;
    public int CurrentSize;

    public List<Part> Parts = new List<Part>();

    public AttackBehavior AttackBehavior = AttackBehavior.Largest;

    // Computed stats
    public int Attack => BaseAttack + Parts.Sum(p => p.Attack);
    public int Defense => BaseDefense + Parts.Sum(p => p.Defense);
    public int Forage => BaseForage + Parts.Sum(p => p.Forage);
    public int MaxHealth => BaseHealth + Parts.Sum(p => p.Health);
    public int Size => BaseSize + Parts.Sum(p => p.Size) + CurrentSize;
    public bool CanAttack => Parts.All(p => p.CanAttack);
    public bool CanDefend => Parts.All(p => p.CanDefend);
    public bool CanForage => Parts.All(p => p.CanForage);

    public void Initialize()
    {
        CurrentHealth = MaxHealth;
        CurrentSize = 0;
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
        foreach (var part in Parts)
            part.OnTickStart(this);
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

        CurrentSize += Forage;

        foreach (var part in Parts)
            part.OnEndForageAction(this);
    }

    public void DefendAction()
    {
        foreach (var part in Parts)
            part.OnDefendAction(this);
    }

    public void AttackAction(Species enemy)
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

        enemy.TakeDamage(this, ref damage);

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

        CurrentHealth -= mitigated;
    }
}