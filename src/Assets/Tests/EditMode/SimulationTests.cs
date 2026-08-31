using NUnit.Framework;
using UnityEngine;

public class SimulationTests
{
    [Test]
    public void PlayerFishVsEnemyFish_CombatRunsSuccessfully()
    {
        var player = new Species
        {
            Name = "Player Fish",
            BaseHealth = 10,
            BaseSize = 1,
            BaseAttack = 0,
            BaseDefense = 0,
            BaseForage = 1
        };

        player.Parts.Add(new Part { Name = "Razor Jaws", BaseAttack = 2 });
        player.Parts.Add(new Part { Name = "Filter Feeder", BaseForage = 2 });

        var enemy = new Species
        {
            Name = "Enemy Fish",
            BaseHealth = 10,
            BaseSize = 5,
            BaseAttack = 1,
            BaseDefense = 1,
            BaseForage = 1
        };

        enemy.Parts.Add(new Part { Name = "Armored Scales", BaseDefense = 1 });
        //enemy.Parts.Add(new Part { Name = "Spiked Body", Behaviors = new() { new ReflectBehavior { AmountToReflect = 1 } } });

        Assert.DoesNotThrow(() => CombatSimulator.Run(player, enemy, logging: true));
    }

    [Test]
    public void AttackConvertsHalfOfActualHealthDamageToSize()
    {
        var attacker = new Species
        {
            Name = "Attacker",
            BaseAttack = 5,
            BaseHealth = 10,
            CurrentHealth = 10,
        };
        var target = new Species
        {
            Name = "Target",
            BaseHealth = 10,
            BaseDefense = 1,
            CurrentHealth = 10,
        };

        attacker.AttackAction(target);

        Assert.That(target.CurrentHealth, Is.EqualTo(6));
        Assert.That(attacker.CurrentSize, Is.EqualTo(2));
    }

    [Test]
    public void LeechBehaviorTransfersStoredSizeFromTarget()
    {
        var leech = new Species { Name = "Leech", BaseHealth = 10, CurrentHealth = 10 };
        var target = new Species { Name = "Target", BaseHealth = 10, CurrentHealth = 10, CurrentSize = 4 };
        var part = new Part
        {
            Behaviors = new() { new LeechBehavior { MinSizeStolen = 1, MaxSizeStolen = 2 } },
        };

        int damage = 1;
        part.OnAttack(leech, target, ref damage);

        Assert.That(leech.CurrentSize, Is.InRange(1, 2));
        Assert.That(target.CurrentSize, Is.InRange(2, 3));
        Assert.That(leech.CurrentSize + target.CurrentSize, Is.EqualTo(4));
    }

    [Test]
    public void ComboAttackBehaviorBuildsDamageAgainstTheSameTarget()
    {
        var attacker = new Species { Name = "Attacker", BaseHealth = 10, CurrentHealth = 10 };
        var target = new Species { Name = "Target", BaseHealth = 10, CurrentHealth = 10 };
        var part = new Part
        {
            Behaviors = new() { new ComboAttackBehavior { BonusDamagePerCombo = 1, MaxCombo = 3 } },
        };
        part.OnEncounterStart(attacker, new SpeciesGroup("Enemies", new[] { target }));

        int firstDamage = 1;
        int secondDamage = 1;
        int thirdDamage = 1;
        part.OnAttack(attacker, target, ref firstDamage);
        part.OnAttack(attacker, target, ref secondDamage);
        part.OnAttack(attacker, target, ref thirdDamage);

        Assert.That(firstDamage, Is.EqualTo(1));
        Assert.That(secondDamage, Is.EqualTo(2));
        Assert.That(thirdDamage, Is.EqualTo(3));
    }

    [Test]
    public void SchoolingGrowthBenefitsTheAttackedFishAndLivingAllies()
    {
        var attacked = new Species { Name = "Attacked", BaseHealth = 10 };
        attacked.Parts.Add(new Part
        {
            Behaviors = new() { new SchoolingGrowthBehavior { SizeGain = 1 } },
        });
        var ally = new Species { Name = "Ally", BaseHealth = 10 };
        var attacker = new Species { Name = "Attacker", BaseHealth = 10, CurrentHealth = 10 };
        var group = new SpeciesGroup("School", new[] { attacked, ally });
        group.Initialize();

        int damage = 1;
        attacked.TakeDamage(attacker, ref damage);

        Assert.That(attacked.CurrentSize, Is.EqualTo(1));
        Assert.That(ally.CurrentSize, Is.EqualTo(1));
    }

    [Test]
    public void PlanktonBloomAddsSizeOnEverySecondForage()
    {
        var forager = new Species { Name = "Forager", BaseHealth = 10, BaseForage = 1 };
        forager.Parts.Add(new Part
        {
            Behaviors = new() { new PlanktonBloomBehavior { ForagesPerBloom = 2, BonusSize = 2 } },
        });
        forager.Initialize();

        forager.ForageAction();
        Assert.That(forager.CurrentSize, Is.EqualTo(1));

        forager.ForageAction();
        Assert.That(forager.CurrentSize, Is.EqualTo(4));
    }

    [Test]
    public void AdjacentBonusUsesOnlyImmediateParts()
    {
        var left = new Part { Name = "Left" };
        var center = new Part
        {
            Behaviors = new() { new AdjacentBonusBehavior { BonusAttack = 2, BonusForage = 3 } },
        };
        var right = new Part { Name = "Right" };
        var fish = new Species { Name = "Fish", BaseHealth = 10 };
        fish.Parts.Add(left);
        fish.Parts.Add(center);
        fish.Parts.Add(right);

        center.OnEncounterStart(fish, null);

        int damage = 0;
        center.OnAttack(fish, new Species { Name = "Enemy" }, ref damage);
        int forage = 0;
        center.OnForage(fish, ref forage);

        Assert.That(damage, Is.EqualTo(4));
        Assert.That(forage, Is.EqualTo(6));
    }

    [Test]
    public void ArchetypeResonanceCountsMatchingEquippedParts()
    {
        var resonance = new Part
        {
            Archetype = PartArchetype.Attack,
            Behaviors = new() { new ArchetypeResonanceBehavior { BonusAttackPerMatch = 2 } },
        };
        var matching = new Part { Archetype = PartArchetype.Attack };
        var different = new Part { Archetype = PartArchetype.Defense };
        var fish = new Species { Name = "Fish", BaseHealth = 10 };
        fish.Parts.Add(resonance);
        fish.Parts.Add(matching);
        fish.Parts.Add(different);

        resonance.OnEncounterStart(fish, null);
        int damage = 0;
        resonance.OnAttack(fish, new Species { Name = "Enemy" }, ref damage);

        Assert.That(damage, Is.EqualTo(2));
    }

    [Test]
    public void ReactiveForageTriggersWhenFishIsAttacked()
    {
        var fish = new Species { Name = "Fish", BaseHealth = 10, BaseForage = 2, CurrentHealth = 10 };
        fish.Parts.Add(new Part { Behaviors = new() { new ReactiveForageBehavior() } });
        var attacker = new Species { Name = "Attacker", BaseHealth = 10, CurrentHealth = 10 };
        int damage = 1;

        fish.TakeDamage(attacker, ref damage);

        Assert.That(fish.CurrentSize, Is.EqualTo(2));
        Assert.That(fish.CurrentHealth, Is.EqualTo(9));
    }

    [Test]
    public void EnemyForageLeechStealsSizeAfterOpposingForage()
    {
        var owner = new Species { Name = "Owner", BaseHealth = 10, CurrentHealth = 10 };
        owner.Parts.Add(new Part { Behaviors = new() { new EnemyForageLeechBehavior { SizeToSteal = 2 } } });
        var enemy = new Species { Name = "Enemy", BaseHealth = 10, BaseForage = 1, CurrentHealth = 10, CurrentSize = 4 };
        var ownerGroup = new SpeciesGroup("Owner", new[] { owner });

        enemy.ForageAction();
        ownerGroup.OnEnemyForaged(enemy);

        Assert.That(enemy.CurrentSize, Is.EqualTo(3));
        Assert.That(owner.CurrentSize, Is.EqualTo(2));
    }

    [Test]
    public void SuccessfulDefenseGrowthRewardsFullyBlockedAttack()
    {
        var defender = new Species { Name = "Defender", BaseHealth = 10, CurrentHealth = 10, BaseDefense = 2 };
        defender.Parts.Add(new Part { Behaviors = new() { new DefensiveGrowthBehavior { SizeGain = 1 } } });
        var attacker = new Species { Name = "Attacker", BaseHealth = 10, CurrentHealth = 10 };
        int damage = 2;

        defender.TakeDamage(attacker, ref damage);

        Assert.That(defender.CurrentSize, Is.EqualTo(1));
        Assert.That(defender.CurrentHealth, Is.EqualTo(10));
    }

    [Test]
    public void RandomAttackModifierStaysWithinConfiguredRange()
    {
        var fish = new Species { Name = "Fish", BaseHealth = 10, CurrentHealth = 10 };
        var part = new Part
        {
            Behaviors = new() { new RandomAttackBehavior { MinimumBonus = -3, MaximumBonus = 2 } },
        };

        for (int i = 0; i < 20; i++)
        {
            int damage = 0;
            part.OnAttack(fish, new Species { Name = "Enemy" }, ref damage);
            Assert.That(damage, Is.InRange(-3, 2));
        }
    }

    [Test]
    public void EnvironmentGuardReducesEnvironmentalDamage()
    {
        var fish = new Species { Name = "Fish", BaseHealth = 10, CurrentHealth = 10 };
        fish.Parts.Add(new Part { Behaviors = new() { new EnvironmentGuardBehavior { DamageReduction = 2 } } });

        fish.TakeEnvironmentDamage(5);

        Assert.That(fish.CurrentHealth, Is.EqualTo(7));
    }

    [Test]
    public void ArmorPiercerReducesDefenseForCurrentAttack()
    {
        var attacker = new Species { Name = "Attacker", BaseHealth = 10, CurrentHealth = 10, BaseAttack = 3 };
        attacker.Parts.Add(new Part { Behaviors = new() { new ArmorPiercerBehavior { DefenseReduction = 2 } } });
        var target = new Species { Name = "Target", BaseHealth = 10, CurrentHealth = 10, BaseDefense = 2 };

        attacker.AttackAction(target);

        Assert.That(target.CurrentHealth, Is.EqualTo(7));
        Assert.That(target.TemporaryDefenseModifier, Is.EqualTo(-2));
    }

    [Test]
    public void BlindActionTemporarilyReducesTargetStats()
    {
        var actor = new Species { Name = "Actor", BaseHealth = 10, CurrentHealth = 10 };
        actor.Parts.Add(new Part { Behaviors = new() { new BlindActionBehavior() } });
        var target = new Species
        {
            Name = "Target",
            BaseHealth = 10,
            CurrentHealth = 10,
            BaseAttack = 5,
            BaseDefense = 3,
            BaseForage = 2,
        };

        Assert.That(actor.ProvidesSpecialAction(SpeciesActionType.Blind), Is.True);
        actor.SpecialAction(SpeciesActionType.Blind, target);

        Assert.That(target.Attack, Is.EqualTo(3));
        Assert.That(target.Defense, Is.EqualTo(1));
        Assert.That(target.Forage, Is.EqualTo(1));

        target.OnTickStart();

        Assert.That(target.Attack, Is.EqualTo(5));
        Assert.That(target.Defense, Is.EqualTo(3));
        Assert.That(target.Forage, Is.EqualTo(2));
    }

    [Test]
    public void PartSchemaCopiesSkillMetadataToRuntimePart()
    {
        var schema = ScriptableObject.CreateInstance<PartSchema>();
        schema.displayName = "Test Skill";
        schema.ActionName = "Test Strike";
        schema.ActionType = SpeciesActionType.Attack;
        schema.IsPassive = true;

        var part = schema.CreatePart();

        Assert.That(part.Name, Is.EqualTo("Test Skill"));
        Assert.That(part.ActionName, Is.EqualTo("Test Strike"));
        Assert.That(part.ActionType, Is.EqualTo(SpeciesActionType.Attack));
        Assert.That(part.IsPassive, Is.True);
        Assert.That(part.IsActionSelectable, Is.False);

        Object.DestroyImmediate(schema);
    }

    [Test]
    public void SpeciesActionPartsPreserveOrderAndDuplicateTypes()
    {
        var first = new Part { ActionName = "First Attack", ActionType = SpeciesActionType.Attack };
        var second = new Part { ActionName = "Second Attack", ActionType = SpeciesActionType.Attack };
        var passive = new Part
        {
            ActionName = "Passive Guard",
            ActionType = SpeciesActionType.Defend,
            IsPassive = true,
        };
        var fish = new Species { Name = "Fish", BaseHealth = 10 };
        fish.Parts.Add(first);
        fish.Parts.Add(second);
        fish.Parts.Add(passive);

        var actions = fish.GetActionParts();

        Assert.That(actions.Count, Is.EqualTo(3));
        Assert.That(actions[0], Is.SameAs(first));
        Assert.That(actions[1], Is.SameAs(second));
        Assert.That(actions[2], Is.SameAs(passive));
        Assert.That(fish.CanUseAction(first, SpeciesActionType.Attack), Is.True);
        Assert.That(fish.CanUseAction(passive, SpeciesActionType.Defend), Is.False);
    }

    [Test]
    public void SourcePartActionExecutesOnlyForOwnedActivePart()
    {
        var attackerPart = new Part
        {
            ActionName = "Attack",
            ActionType = SpeciesActionType.Attack,
        };
        var attacker = new Species
        {
            Name = "Attacker",
            BaseHealth = 10,
            CurrentHealth = 10,
            BaseAttack = 3,
        };
        attacker.Parts.Add(attackerPart);
        var target = new Species { Name = "Target", BaseHealth = 10, CurrentHealth = 10 };
        var manager = new ActionManager();
        manager.SetAction(new SpeciesAction
        {
            Actor = attacker,
            Type = SpeciesActionType.Attack,
            SourcePart = attackerPart,
            Targets = new() { target },
        });

        CombatSimulator.ExecuteActions(
            new SpeciesGroup("Player", new[] { attacker }),
            new SpeciesGroup("Enemy", new[] { target }),
            manager);

        Assert.That(target.CurrentHealth, Is.EqualTo(7));
    }
}
