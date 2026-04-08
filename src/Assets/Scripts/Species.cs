using System.Collections.Generic;
using System.Linq;

public class Species
{
    public string Name;

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

    // Computed stats
    public int Attack => BaseAttack + Parts.Sum(p => p.Attack);
    public int Defense => BaseDefense + Parts.Sum(p => p.Defense);
    public int Forage => BaseForage + Parts.Sum(p => p.Forage);
    public int MaxHealth => BaseHealth + Parts.Sum(p => p.Health);
    public int Size => BaseSize + Parts.Sum(p => p.Size) + CurrentSize;

    public void Initialize()
    {
        CurrentHealth = MaxHealth;
        CurrentSize = 0;
    }

    public bool IsAlive => CurrentHealth > 0;

    public void OnCombatStart(Species enemy)
    {
        foreach (var part in Parts)
            part.OnCombatStart(this, enemy);
    }

    public void TickStart(Species enemy)
    {
        foreach (var part in Parts)
            part.OnTickStart(this, enemy);
    }

    public void TickEnd(Species enemy)
    {
        foreach (var part in Parts)
            part.OnTickEnd(this, enemy);
    }

    public void ApplyForage()
    {
        CurrentSize += Forage;
    }

    public void AttackTarget(Species enemy)
    {
        if (Attack <= 0) return;

        int damage = Attack;

        foreach (var part in Parts)
            part.OnAttack(this, enemy, ref damage);

        enemy.TakeDamage(this, ref damage);
    }

    public void TakeDamage(Species attacker, ref int damage)
    {
        int mitigated = damage - Defense;
        if (mitigated < 0) mitigated = 0;

        foreach (var part in Parts)
            part.OnDefend(this, attacker, ref mitigated);

        CurrentHealth -= mitigated;
    }
}