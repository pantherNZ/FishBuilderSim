using System;
using UnityEngine;

[Serializable]
public enum StatusEffectKind
{
    Buff,
    Debuff,
}

[Serializable]
public enum StatusEffectType
{
    Bleed,
    DeepWound,
    Blind,
    Poison,
}

[Serializable]
public abstract class StatusEffect
{
    public StatusEffectType Type { get; }
    public string Name { get; }
    public StatusEffectKind Kind { get; }
    public Sprite Sprite;
    public int RemainingTurns { get; private set; }
    public bool IsExpired => RemainingTurns <= 0;
    public virtual bool CanStack => false;

    protected StatusEffect(StatusEffectType type, string name, StatusEffectKind kind, int duration, Sprite sprite = null)
    {
        Type = type;
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

    public static StatusEffect Create(StatusEffectType type, int duration = 1, Sprite sprite = null)
    {
        return type switch
        {
            StatusEffectType.Bleed => new BleedStatusEffect(duration, sprite),
            StatusEffectType.DeepWound => new DeepWoundStatusEffect(duration, sprite),
            StatusEffectType.Blind => new BlindStatusEffect(duration, sprite),
            StatusEffectType.Poison => new BleedStatusEffect(duration, sprite, "Poison", StatusEffectType.Poison),
            _ => throw new ArgumentException($"Unknown status effect type: {type}"),
        };
    }
}

[Serializable]
public sealed class BleedStatusEffect : StatusEffect
{
    public const int DamagePerTurn = 1;

    public BleedStatusEffect(int duration, Sprite sprite = null, string nameOverride = "Bleed", StatusEffectType type = StatusEffectType.Bleed)
        : base(type, nameOverride, StatusEffectKind.Debuff, duration, sprite)
    {
    }

    protected override void OnTurnStart(Species target)
    {
        if (target.IsAlive)
        {
            target.ApplyDamage(DamagePerTurn);
        }
    }
}


[Serializable]
public sealed class DeepWoundStatusEffect : StatusEffect
{
    public int MaxHealthModifierPercent => -10;

    int appliedMaxHealthModifier;

    public DeepWoundStatusEffect(int duration, Sprite sprite = null)
        : base(StatusEffectType.DeepWound, "Deep Wound", StatusEffectKind.Debuff, duration, sprite)
    {
    }

    protected override void OnApplied(Species target)
    {
        appliedMaxHealthModifier = MaxHealthModifierPercent * target.MaxHealth / 100;
        target.TemporaryMaxHealthModifier += appliedMaxHealthModifier;
    }

    protected override void OnExpired(Species target)
    {
        target.TemporaryMaxHealthModifier -= appliedMaxHealthModifier;
    }
}

[Serializable]
public sealed class BlindStatusEffect : StatusEffect
{
    public int attackModifierPercent => -50;

    int appliedAttackModifier;

    public BlindStatusEffect(int duration, Sprite sprite = null)
        : base(StatusEffectType.Blind, "Blind", StatusEffectKind.Debuff, duration, sprite)
    {
    }

    protected override void OnApplied(Species target)
    {
        appliedAttackModifier = attackModifierPercent * target.BaseAttack / 100;
        target.TemporaryAttackModifier += appliedAttackModifier;
    }

    protected override void OnExpired(Species target)
    {
        target.TemporaryAttackModifier -= appliedAttackModifier;
    }
}