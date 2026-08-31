using System;
using UnityEngine;

public enum StatusEffectKind
{
    Buff,
    Debuff,
}

public abstract class StatusEffect
{
    public string Name { get; }
    public StatusEffectKind Kind { get; }
    public Sprite Sprite;
    public int RemainingTurns { get; private set; }
    public bool IsExpired => RemainingTurns <= 0;

    protected StatusEffect(string name, StatusEffectKind kind, int duration, Sprite sprite = null)
    {
        Name = name;
        Kind = kind;
        Sprite = sprite;
        RemainingTurns = Math.Max(1, duration);
    }

    internal void Tick(Species target)
    {
        if (target == null || IsExpired)
            return;

        OnTurnStart(target);
        RemainingTurns--;

        if (IsExpired)
            OnExpired(target);
    }

    internal void Apply(Species target)
    {
        OnApplied(target);
    }

    protected virtual void OnApplied(Species target) { }
    protected virtual void OnTurnStart(Species target) { }
    protected virtual void OnExpired(Species target) { }
}

public sealed class BleedStatusEffect : StatusEffect
{
    public const int Duration = 3;
    public const int DamagePerTurn = 1;

    public BleedStatusEffect(int duration = Duration, Sprite sprite = null)
        : base("Bleed", StatusEffectKind.Debuff, duration, sprite)
    {
    }

    protected override void OnTurnStart(Species target)
    {
        if (target.IsAlive)
            target.CurrentHealth = Math.Max(0, target.CurrentHealth - DamagePerTurn);
    }
}
