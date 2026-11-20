using System;
using System.Collections.Generic;
using System.Linq;
using Runtime.Game;
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
    public Species()
    {
        Parts.Add(GlobalConstantsHandler.Constants.defaultAttack.CreatePart());
        Parts.Add(GlobalConstantsHandler.Constants.defaultDefend.CreatePart());
        Parts.Add(GlobalConstantsHandler.Constants.defaultForage.CreatePart());
    }

    public const int EnergyPerTurn = 5;

    readonly Dictionary<Part, int> _skillCooldowns = new();
    public int CurrentEnergy { get; private set; } = EnergyPerTurn;

    public SpeciesSchema Schema;
    public string Name;
    public Sprite Portrait;
    public SpeciesGroup Group;
    public bool PlayerSpecies;

    // Base stats
    public int BaseHealth;
    public int BaseSize;
    public int BaseAttack;
    public int BaseDefense;
    public int BaseForage;
    public int BaseStrength;
    public int BaseBlock;

    // Runtime stats
    public int CurrentHealth;
    public int TemporaryAttackModifier;
    public int TemporaryDefenseModifier;
    public int TemporaryForageModifier;
    public int TemporaryMaxHealthModifier;
    public int TemporaryStrengthModifier;
    public int TemporaryBlockModifier; // resets each turn

    public event Action<Species, int> DamageTaken;

    public List<Part> Parts = new List<Part>();
    public Dictionary<Part, HashSet<Part>> ConnectedParts = new();
    public List<StatusEffect> StatusEffects = new List<StatusEffect>();
    public IReadOnlyList<StatusEffect> ActiveStatusEffects => StatusEffects;

    public AttackBehavior AttackBehavior = AttackBehavior.Largest;
    public List<SpeciesActionTurn> ActionSequence = new();
    int _nextActionSequenceIndex;

    public bool HasActionSequence => ActionSequence != null && ActionSequence.Count > 0;

    public void RebuildConnectedParts()
    {
        ConnectedParts = PartCirclePacking.BuildConnectedParts(Parts);

        foreach (var part in Parts)
        {
            foreach (var behavior in part.Behaviors)
            {
                behavior.AttachTo(part);
                behavior.OnConnectedPartsUpdated(part, ConnectedParts.TryGetValue(part, out var connected) ? connected : new HashSet<Part>());
            }
        }
    }

    public IReadOnlyList<SpeciesActionType> GetNextActionTurn()
    {
        if (!HasActionSequence)
        {
            return Array.Empty<SpeciesActionType>();
        }

        _nextActionSequenceIndex %= ActionSequence.Count;
        SpeciesActionTurn turn = ActionSequence[_nextActionSequenceIndex];
        _nextActionSequenceIndex = (_nextActionSequenceIndex + 1) % ActionSequence.Count;
        if (turn?.Actions == null)
        {
            return Array.Empty<SpeciesActionType>();
        }

        return turn.Actions;
    }

    public void ResetActionSequence()
    {
        _nextActionSequenceIndex = 0;
    }

    // Computed stats
    public int Attack => Math.Max(0, BaseAttack + Parts.Sum(p => p.BaseAttack) + TemporaryAttackModifier);
    public int Defense => Math.Max(0, BaseDefense + Parts.Sum(p => p.BaseDefense) + TemporaryDefenseModifier);
    public int Forage => Math.Max(0, BaseForage + Parts.Sum(p => p.BaseForage) + TemporaryForageModifier);
    public int ActionAttack(Part part) => Math.Max(0, BaseAttack + Parts.Sum(p => part == p ? p.Attack : p.BaseAttack) + TemporaryAttackModifier);
    public int ActionDefense(Part part) => Math.Max(0, BaseDefense + Parts.Sum(p => part == p ? p.Defense : p.BaseDefense) + TemporaryDefenseModifier);
    public int ActionForage(Part part) => Math.Max(0, BaseForage + Parts.Sum(p => part == p ? p.Forage : p.BaseForage) + TemporaryForageModifier);
    public int MaxHealth => BaseHealth + Parts.Sum(p => p.BaseHealth) + TemporaryMaxHealthModifier;
    public int Size => BaseSize + Parts.Sum(p => p.BaseSize);
    public int Strength => BaseStrength + Parts.Sum(p => p.BaseStrength) + TemporaryStrengthModifier;
    public int Block => BaseBlock + Parts.Sum(p => p.BaseBlock) + TemporaryBlockModifier;
    public bool CanAttack => !Parts.Any(p => !p.CanAttack);
    public bool CanDefend => !Parts.Any(p => !p.CanDefend);
    public bool CanForage => !Parts.Any(p => !p.CanForage);

    public IReadOnlyList<Part> GetActionParts()
    {
        return Parts.Where(part => part != null && part.HasAction).ToList();
    }

    public bool CanUseAction(Part sourcePart, SpeciesActionType actionType)
    {
        bool isOwnedPart = Parts.Contains(sourcePart);
        bool isBuiltInActionPart = IsBuiltInActionPart(sourcePart, actionType);
        return (isOwnedPart || isBuiltInActionPart)
            && sourcePart.IsActionSelectable
            && sourcePart.ActionType == actionType
            && CurrentEnergy >= GetActionEnergyCost(sourcePart)
            && GetRemainingCooldown(sourcePart) == 0;
    }

    public static bool IsBuiltInActionPart(Part part, SpeciesActionType actionType)
    {
        if (part?.Schema == null || GlobalConstantsHandler.Constants == null)
        {
            return false;
        }

        var constants = GlobalConstantsHandler.Constants;
        return actionType switch
        {
            SpeciesActionType.Attack => part.Schema == constants.defaultAttack,
            SpeciesActionType.Defend => part.Schema == constants.defaultDefend,
            SpeciesActionType.Forage => part.Schema == constants.defaultForage,
            _ => false,
        };
    }

    public int GetRemainingCooldown(Part part)
    {
        return part != null && _skillCooldowns.TryGetValue(part, out int remaining) ? remaining : 0;
    }

    public static int GetActionEnergyCost(Part part)
    {
        return Math.Max(0, part.EnergyCost);
    }

    public bool TrySpendActionResources(Part part, SpeciesActionType actionType)
    {
        if (!IsAlive || !CanUseAction(part, actionType))
        {
            return false;
        }

        CurrentEnergy -= GetActionEnergyCost(part);
        if (part != null && part.CooldownTurns > 0)
        {
            _skillCooldowns[part] = part.CooldownTurns;
        }

        return true;
    }

    void ResetActionResources()
    {
        CurrentEnergy = EnergyPerTurn;
        _skillCooldowns.Clear();
    }

    public void Initialize()
    {
        RebuildConnectedParts();
        CurrentHealth = MaxHealth;
        TemporaryStrengthModifier = 0;
        TemporaryAttackModifier = 0;
        TemporaryDefenseModifier = 0;
        TemporaryForageModifier = 0;
        TemporaryBlockModifier = 0;
        StatusEffects.Clear();
        ResetActionResources();
        ResetActionSequence();
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
        RebuildConnectedParts();
        ResetActionResources();
        ResetActionSequence();
        foreach (var part in Parts)
            part.OnEncounterStart(this, enemy);
    }

    public void OnEncounterFinish()
    {
        ResetActionResources();
        ResetActionSequence();
    }

    public void OnTickStart()
    {
        OnTurnStart();
    }

    public void OnTurnStart(bool playerGroup = false)
    {
        CurrentEnergy = EnergyPerTurn;
        foreach (var part in _skillCooldowns.Keys.ToList())
        {
            int remaining = _skillCooldowns[part] - 1;
            if (remaining <= 0 || !Parts.Contains(part))
            {
                _skillCooldowns.Remove(part);
            }
            else
            {
                _skillCooldowns[part] = remaining;
            }
        }

        TickStatusEffects();
        if (!IsAlive)
            return;

        foreach (var part in Parts)
            part.OnTurnStart(this);
    }

    public void ApplyStatusEffect(StatusEffect statusEffect)
    {
        if (statusEffect == null || statusEffect.IsExpired)
            return;

        if (statusEffect.CanStack || !StatusEffects.Any(se => se.Type == statusEffect.Type))
        {
            StatusEffects.Add(statusEffect);
            statusEffect.Apply(this);
        }
    }

    public void OnEnemyForaged(Species enemy)
    {
        foreach (var part in Parts)
            part.OnEnemyForaged(this, enemy);
    }

    public void OnTurnEnd(bool playerGroup = false)
    {
        if (playerGroup && !PlayerSpecies)
            TemporaryBlockModifier = 0;
        else if (!playerGroup && PlayerSpecies)
            TemporaryBlockModifier = 0;

        foreach (var part in Parts)
            part.OnTurnEnd(this);
    }

    public void ForageAction(Part part)
    {
        ExecuteForageAction(part);
    }

    internal void TriggerForageAction()
    {
        ExecuteForageAction(null);
    }

    void ExecuteForageAction(Part sourcePart)
    {
        if (!CanForage)
            return;

        foreach (var part in Parts)
            part.OnStartForageAction(this);

        int forageAmount = ActionForage(sourcePart);
        foreach (var part in Parts)
            part.OnForage(this, ref forageAmount);

        AddForageGain(forageAmount);

        foreach (var part in Parts)
            part.OnEndForageAction(this);
    }

    public void AddForageGain(int amount)
    {
        int gain = Mathf.Max(0, amount);
        TemporaryStrengthModifier += gain;
    }

    public void DefendAction(Part sourcePart)
    {
        foreach (var part in Parts)
            part.OnDefendAction(this);
        TemporaryBlockModifier += ActionDefense(sourcePart);
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
        var damage = ActionAttack(sourcePart);

        if (damage <= 0)
            return;

        foreach (var part in Parts)
            part.OnStartAttackAction(this);

        damage = GetStrengthAdjustedDamage(enemy, damage);

        foreach (var part in Parts)
            part.OnAttack(this, enemy, ref damage);

        int healthBefore = enemy.CurrentHealth;
        enemy.TakeDamage(this, ref damage);
        int healthLost = Mathf.Max(0, Mathf.Min(healthBefore, healthBefore - enemy.CurrentHealth));

        if (healthLost > 0)
            foreach (var part in Parts)
                part.OnAttackHit(this, enemy, healthLost, sourcePart);

        foreach (var part in Parts)
            part.OnEndAttackAction(this);
    }

    public int GetStrengthAdjustedAttackDamage(Species enemy) =>
        GetStrengthAdjustedDamage(enemy, Attack);

    public int GetStrengthAdjustedDamage(Species enemy, int damage)
    {
        if (enemy == null)
            return Mathf.Max(0, damage);

        float attackerStrength = Mathf.Max(1, Strength);
        float targetSize = Mathf.Max(1, enemy.Size);
        float sizeMultiplier = attackerStrength / targetSize;
        return Mathf.Max(0, Mathf.CeilToInt(damage * sizeMultiplier));
    }

    public void TakeDamage(Species attacker, ref int damage)
    {
        int defenseToUse = CanDefend ? Block : 0;

        int mitigated = damage - defenseToUse;
        if (mitigated < 0)
            mitigated = 0;

        TemporaryBlockModifier = Mathf.Max(0, TemporaryBlockModifier - damage);

        foreach (var part in Parts)
            part.OnBlock(this, attacker, ref mitigated);

        if (attacker != null && mitigated <= 0)
            foreach (var part in Parts)
                part.OnSuccessfulBlock(this, attacker);

        ApplyDamage(mitigated);
    }

    public void ApplyDamage(int damage)
    {
        int adjustedDamage = Math.Max(0, damage);
        if (adjustedDamage <= 0)
        {
            return;
        }

        CurrentHealth = Math.Max(0, CurrentHealth - adjustedDamage);
        DamageTaken?.Invoke(this, adjustedDamage);
    }

    public void AddTemporaryStatModifiers(int attack, int defense, int forage)
    {
        TemporaryAttackModifier += attack;
        TemporaryDefenseModifier += defense;
        TemporaryForageModifier += forage;
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