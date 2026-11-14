using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

public class SimulationTests
{
    [Test]
    public void EachFishStartsWithFiveEnergyAndRefreshesOnlyOnItsTurn()
    {
        var first = new Species { BaseHealth = 10, CurrentHealth = 10 };
        var second = new Species { BaseHealth = 10, CurrentHealth = 10 };

        Assert.That(first.CurrentEnergy, Is.EqualTo(5));
        Assert.That(first.TrySpendActionResources(null, SpeciesActionType.Attack), Is.True);
        Assert.That(first.CurrentEnergy, Is.EqualTo(4));
        Assert.That(second.CurrentEnergy, Is.EqualTo(5));

        second.OnTurnStart();
        Assert.That(first.CurrentEnergy, Is.EqualTo(4));
        first.OnTurnStart();
        Assert.That(first.CurrentEnergy, Is.EqualTo(5));
    }

    [TestCase(0, 5, 5)]
    [TestCase(1, 5, 0)]
    [TestCase(2, 2, 1)]
    [TestCase(5, 1, 0)]
    [TestCase(6, 0, 5)]
    public void SkillEnergyCostLimitsUsesWithinATurn(int cost, int uses, int remainingEnergy)
    {
        var fish = new Species { BaseHealth = 10, CurrentHealth = 10 };
        var skill = new Part { ActionType = SpeciesActionType.Attack, EnergyCost = cost };
        fish.Parts.Add(skill);

        for (int index = 0; index < uses; index++)
        {
            Assert.That(fish.TrySpendActionResources(skill, skill.ActionType), Is.True);
        }

        Assert.That(fish.CurrentEnergy, Is.EqualTo(remainingEnergy));
        if (cost > 0)
        {
            Assert.That(fish.TrySpendActionResources(skill, skill.ActionType), Is.False);
            Assert.That(fish.CurrentEnergy, Is.EqualTo(remainingEnergy));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void CooldownCountsOwnerTurnStartsWithZeroAllowingImmediateReuse(int cooldown)
    {
        var fish = new Species { BaseHealth = 10, CurrentHealth = 10 };
        var skill = new Part { ActionType = SpeciesActionType.Attack, EnergyCost = 0, CooldownTurns = cooldown };
        fish.Parts.Add(skill);

        Assert.That(fish.TrySpendActionResources(skill, skill.ActionType), Is.True);
        Assert.That(fish.GetRemainingCooldown(skill), Is.EqualTo(cooldown));
        Assert.That(fish.CanUseAction(skill, skill.ActionType), Is.EqualTo(cooldown == 0));

        for (int turn = 1; turn <= cooldown; turn++)
        {
            fish.OnTurnStart();
            Assert.That(fish.GetRemainingCooldown(skill), Is.EqualTo(cooldown - turn));
            Assert.That(fish.CanUseAction(skill, skill.ActionType), Is.EqualTo(turn == cooldown));
        }

        Assert.That(fish.TrySpendActionResources(skill, skill.ActionType), Is.True);
    }

    [Test]
    public void CooldownsAreIndependentForEachFishAndEquippedSkill()
    {
        var first = new Species { CurrentHealth = 10 };
        var second = new Species { CurrentHealth = 10 };
        var skill = new Part { ActionType = SpeciesActionType.Attack, CooldownTurns = 2 };
        var duplicate = new Part { ActionType = SpeciesActionType.Attack, CooldownTurns = 2 };
        first.Parts.Add(skill);
        first.Parts.Add(duplicate);
        second.Parts.Add(skill);

        first.TrySpendActionResources(skill, skill.ActionType);
        second.OnTurnStart();

        Assert.That(first.GetRemainingCooldown(skill), Is.EqualTo(2));
        Assert.That(first.CanUseAction(duplicate, duplicate.ActionType), Is.True);
        Assert.That(second.CanUseAction(skill, skill.ActionType), Is.True);
    }

    [Test]
    public void EncounterAndInitializationResetEnergyAndCooldowns()
    {
        var fish = new Species { BaseHealth = 10, CurrentHealth = 10 };
        var skill = new Part { ActionType = SpeciesActionType.Attack, EnergyCost = 5, CooldownTurns = 3 };
        fish.Parts.Add(skill);

        fish.TrySpendActionResources(skill, skill.ActionType);
        fish.OnEncounterStart(new SpeciesGroup("Enemy"));
        Assert.That(fish.CurrentEnergy, Is.EqualTo(5));
        Assert.That(fish.GetRemainingCooldown(skill), Is.Zero);

        fish.TrySpendActionResources(skill, skill.ActionType);
        fish.Initialize();
        Assert.That(fish.CurrentEnergy, Is.EqualTo(5));
        Assert.That(fish.GetRemainingCooldown(skill), Is.Zero);
    }

    [Test]
    public void CombatExecutionChargesOnceForMultipleTargetsAndCannotReplayCooldownSkill()
    {
        var actor = new Species { BaseHealth = 10, CurrentHealth = 10 };
        var skill = new Part { ActionType = SpeciesActionType.Attack, EnergyCost = 2, CooldownTurns = 1 };
        actor.Parts.Add(skill);
        var first = new Species { BaseHealth = 100, CurrentHealth = 100 };
        var second = new Species { BaseHealth = 100, CurrentHealth = 100 };
        var allies = new SpeciesGroup("Allies", new[] { actor });
        var enemies = new SpeciesGroup("Enemies", new[] { first, second });
        var manager = new ActionManager();
        manager.SetAction(new SpeciesAction
        {
            Actor = actor,
            SourcePart = skill,
            Type = skill.ActionType,
            Targets = new() { first, second },
        });

        CombatSimulator.ExecuteActions(allies, enemies, manager);
        Assert.That(actor.CurrentEnergy, Is.EqualTo(3));
        Assert.That(actor.GetRemainingCooldown(skill), Is.EqualTo(1));
        Assert.That(first.CurrentHealth, Is.EqualTo(99));
        Assert.That(second.CurrentHealth, Is.EqualTo(99));

        CombatSimulator.ExecuteActions(allies, enemies, manager);
        Assert.That(actor.CurrentEnergy, Is.EqualTo(3));
        Assert.That(first.CurrentHealth, Is.EqualTo(99));
        Assert.That(second.CurrentHealth, Is.EqualTo(99));
    }

    [Test]
    public void EmptyActionQueueAndMissingTargetsDoNotSpendEnergyOrStartCooldown()
    {
        var actor = new Species { CurrentHealth = 10 };
        var skill = new Part { ActionType = SpeciesActionType.Attack, EnergyCost = 3, CooldownTurns = 2 };
        actor.Parts.Add(skill);
        var allies = new SpeciesGroup("Allies", new[] { actor });
        var enemies = new SpeciesGroup("Enemies");
        var manager = new ActionManager();

        CombatSimulator.ExecuteActions(allies, enemies, manager);
        manager.SetAction(new SpeciesAction { Actor = actor, SourcePart = skill, Type = skill.ActionType });
        CombatSimulator.ExecuteActions(allies, enemies, manager);

        Assert.That(actor.CurrentEnergy, Is.EqualTo(5));
        Assert.That(actor.GetRemainingCooldown(skill), Is.Zero);
    }

    [Test]
    public void QueuedBasicAttacksCannotBypassEnergyValidationAtExecution()
    {
        var actor = new Species { CurrentHealth = 10 };
        var target = new Species { BaseHealth = 100, CurrentHealth = 100 };
        var allies = new SpeciesGroup("Allies", new[] { actor });
        var enemies = new SpeciesGroup("Enemies", new[] { target });
        var manager = new ActionManager();
        manager.SetAction(new SpeciesAction { Actor = actor, Type = SpeciesActionType.Attack });

        for (int index = 0; index < 6; index++)
        {
            CombatSimulator.ExecuteActions(allies, enemies, manager);
        }

        Assert.That(actor.CurrentEnergy, Is.Zero);
        Assert.That(target.CurrentHealth, Is.EqualTo(95));
        manager.Clear();
        manager.SetAction(new SpeciesAction { Actor = actor, Type = SpeciesActionType.Attack });
        Assert.That(manager.Actions, Is.Empty);
    }

    [TestCase(SpeciesActionType.Forage)]
    [TestCase(SpeciesActionType.Defend)]
    [TestCase(SpeciesActionType.Blind)]
    public void EveryActiveSkillTypeConsumesResources(SpeciesActionType type)
    {
        var actor = new Species { BaseHealth = 10, CurrentHealth = 10, BaseForage = 2 };
        var skill = new Part { ActionType = type, EnergyCost = 3, CooldownTurns = 2 };
        if (type == SpeciesActionType.Blind)
        {
            skill.Behaviors.Add(new BlindActionBehavior());
        }
        actor.Parts.Add(skill);
        var target = new Species { CurrentHealth = 10 };
        var manager = new ActionManager();
        // Legacy source-less requests must resolve a real owned skill, not bypass its cost.
        manager.SetAction(new SpeciesAction { Actor = actor, Type = type });
        Assert.That(manager.Actions[0].SourcePart, Is.SameAs(skill));

        CombatSimulator.ExecuteActions(new SpeciesGroup("Allies", new[] { actor }),
            new SpeciesGroup("Enemies", new[] { target }), manager);

        Assert.That(actor.CurrentEnergy, Is.EqualTo(2));
        Assert.That(actor.GetRemainingCooldown(skill), Is.EqualTo(2));
    }

    [Test]
    public void SkillPreviewShowsEnergyCostAndRemainingCooldown()
    {
        var actor = new Species { CurrentHealth = 10 };
        var skill = new Part { ActionType = SpeciesActionType.Attack, EnergyCost = 2, CooldownTurns = 3 };
        actor.Parts.Add(skill);
        actor.TrySpendActionResources(skill, skill.ActionType);

        var button = SpeciesHealthBarElement.CreateActionButton(actor, skill);
        Assert.That(button.tooltip, Does.Contain("Energy: 2"));
        Assert.That(button.tooltip, Does.Contain("Cooldown remaining: 3"));
        Assert.That(button.ClassListContains("bp-health-bar__action-btn--cooldown"), Is.True);
    }

    [Test]
    public void SkillBarDisablesCooldownAndUnaffordableSkillsButKeepsFreeSkillsAvailable()
    {
        var actor = new Species { BaseHealth = 10, CurrentHealth = 10 };
        var cooldownSkill = new Part { ActionType = SpeciesActionType.Attack, EnergyCost = 5, CooldownTurns = 2 };
        var freeSkill = new Part { ActionType = SpeciesActionType.Attack, EnergyCost = 0 };
        actor.Parts.Add(cooldownSkill);
        actor.Parts.Add(freeSkill);
        actor.TrySpendActionResources(cooldownSkill, cooldownSkill.ActionType);
        var bar = new SpeciesHealthBarElement(actor);
        bar.SetActionControlsVisible(true);
        bar.ConfigureActionButtons(new Part[] { null, cooldownSkill, freeSkill }, null);
        bar.SetActionButtonsEnabled(true);
        var buttons = bar.Query<Button>().ToList();

        Assert.That(buttons[0].enabledSelf, Is.False);
        Assert.That(buttons[1].enabledSelf, Is.False);
        Assert.That(buttons[1].Q<Label>(className: "bp-health-bar__action-cooldown").text, Is.EqualTo("CD 2"));
        Assert.That(buttons[2].enabledSelf, Is.True);
        Assert.That(buttons[2].Q<Label>(className: "bp-health-bar__action-cost").text, Is.EqualTo("0"));
    }

    [Test]
    public void BattleLayoutIncludesAnExplicitEndTurnControl()
    {
        var tree = Resources.Load<VisualTreeAsset>("UI/BattlePanel");
        Assert.That(tree, Is.Not.Null);
        var root = tree.CloneTree();
        Assert.That(root.Q<Button>("bp-end-turn-btn").text, Is.EqualTo("END TURN"));
    }

    [Test]
    public void DuplicateSkillPreviewUsesAvailableCopyWhenFirstIsCoolingDown()
    {
        var actor = new Species { CurrentHealth = 10 };
        var first = new Part { ActionType = SpeciesActionType.Attack, ActionName = "Bite", CooldownTurns = 2 };
        var second = new Part { ActionType = SpeciesActionType.Attack, ActionName = "Bite", CooldownTurns = 2 };
        actor.Parts.Add(first);
        actor.Parts.Add(second);
        actor.TrySpendActionResources(first, first.ActionType);

        var actions = SpeciesHealthBarElement.GetAvailableActions(actor);

        Assert.That(actions, Is.EqualTo(new Part[] { null, second }));
    }

    [Test]
    public void PartSchemaCopiesAndClampsSkillResourceMetadata()
    {
        var schema = ScriptableObject.CreateInstance<PartSchema>();
        try
        {
            schema.EnergyCost = 3;
            schema.CooldownTurns = 2;
            var skill = schema.CreatePart();
            Assert.That(skill.EnergyCost, Is.EqualTo(3));
            Assert.That(skill.CooldownTurns, Is.EqualTo(2));

            schema.EnergyCost = -1;
            schema.CooldownTurns = -2;
            skill = schema.CreatePart();
            Assert.That(skill.EnergyCost, Is.Zero);
            Assert.That(skill.CooldownTurns, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(schema);
        }
    }

    [Test]
    public void EditorSkillPreviewIncludesBasicAttackAndDistinctSelectableSkills()
    {
        var species = new Species();
        var bite = new Part { ActionType = SpeciesActionType.Attack, ActionName = "Bite" };
        var forage = new Part { ActionType = SpeciesActionType.Forage, ActionName = "Feed" };
        species.Parts.Add(bite);
        species.Parts.Add(new Part { ActionType = SpeciesActionType.Attack, ActionName = " bite " });
        species.Parts.Add(new Part { ActionType = SpeciesActionType.Attack });
        species.Parts.Add(new Part { ActionType = SpeciesActionType.Defend, IsPassive = true });
        species.Parts.Add(new Part());
        species.Parts.Add(forage);

        var actions = SpeciesHealthBarElement.GetAvailableActions(species);

        Assert.That(actions, Is.EqualTo(new Part[] { null, bite, forage }));
        Assert.That(SpeciesHealthBarElement.GetAvailableActions(null), Is.Empty);
    }

    [Test]
    public void SharedSkillButtonShowsEncounterIconStrengthAndTooltip()
    {
        var species = new Species { BaseForage = 3 };
        var part = new Part
        {
            ActionType = SpeciesActionType.Forage,
            ActionName = "Filter Feed",
            Description = "Gather food.",
        };

        var button = SpeciesHealthBarElement.CreateActionButton(species, part);

        Assert.That(button.ClassListContains("bp-health-bar__action-btn"), Is.True);
        Assert.That(button.Q<Label>(className: "bp-health-bar__action-btn-icon").text, Is.EqualTo("FOR"));
        Assert.That(button.Q<Label>(className: "bp-health-bar__action-btn-strength").text, Is.EqualTo("FOR +3"));
        Assert.That(button.tooltip, Does.StartWith("FILTER FEED\nFORAGE\n"));
        Assert.That(button.tooltip, Does.Contain("Energy: 1 | Cooldown: 0 turns"));
        Assert.That(button.tooltip, Does.EndWith("Gather food."));
    }

    [Test]
    public void SpeciesEditorLayoutHasOverlayDetailsAndBottomSkillBar()
    {
        var tree = Resources.Load<VisualTreeAsset>("UI/SpeciesEditorPanel");
        Assert.That(tree, Is.Not.Null);
        var root = tree.CloneTree();
        var editorRoot = root.Q("sep-root");
        var workspace = root.Q("workspace");
        var popup = root.Q("detail-panel");
        var skillBar = root.Q("player-skill-bar");

        Assert.That(root.Q("editor-panel").parent, Is.SameAs(workspace));
        Assert.That(popup.parent, Is.SameAs(editorRoot));
        Assert.That(skillBar.parent, Is.SameAs(editorRoot));
        Assert.That(editorRoot.IndexOf(skillBar), Is.GreaterThan(editorRoot.IndexOf(workspace)));
        Assert.That(popup.pickingMode, Is.EqualTo(PickingMode.Ignore));
        Assert.That(root.Q("detail-pointer").pickingMode, Is.EqualTo(PickingMode.Ignore));
        Assert.That(root.Q("player-skill-buttons"), Is.Not.Null);
    }

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
    public void EqualSizeFishDealNormalDamage()
    {
        var attacker = new Species
        {
            Name = "Attacker",
            BaseHealth = 10,
            BaseSize = 10,
            BaseAttack = 4,
            CurrentHealth = 10,
        };
        var target = new Species
        {
            Name = "Target",
            BaseHealth = 20,
            BaseSize = 10,
            CurrentHealth = 20,
        };

        attacker.AttackAction(target);

        Assert.That(target.CurrentHealth, Is.EqualTo(16));
    }

    [Test]
    public void DoubleSizeFishDealDoubleDamage()
    {
        var attacker = new Species
        {
            Name = "Attacker",
            BaseHealth = 10,
            BaseSize = 10,
            BaseAttack = 4,
            CurrentHealth = 10,
        };
        var target = new Species
        {
            Name = "Target",
            BaseHealth = 20,
            BaseSize = 5,
            CurrentHealth = 20,
        };

        attacker.AttackAction(target);

        Assert.That(target.CurrentHealth, Is.EqualTo(12));
    }


    [Test]
    public void BasicAttackDoesNotRequireAttackCapableParts()
    {
        var attacker = new Species
        {
            Name = "Attacker",
            BaseHealth = 10,
            BaseSize = 10,
            BaseAttack = 4,
            CurrentHealth = 10,
        };
        attacker.Parts.Add(new Part { CanAttack = false });
        var target = new Species
        {
            Name = "Target",
            BaseHealth = 20,
            BaseSize = 10,
            CurrentHealth = 20,
        };

        attacker.AttackAction(target);

        Assert.That(target.CurrentHealth, Is.EqualTo(16));
    }

    [Test]
    public void SpeciesWithoutPartsCanUseBasicAttack()
    {
        var attacker = new Species
        {
            Name = "Attacker",
            BaseHealth = 10,
            BaseSize = 5,
            CurrentHealth = 10,
        };
        var target = new Species
        {
            Name = "Target",
            BaseHealth = 10,
            BaseSize = 5,
            CurrentHealth = 10,
        };

        attacker.AttackAction(target);

        Assert.That(target.CurrentHealth, Is.EqualTo(9));
    }

    [Test]
    public void ForageRequiresAnActiveForagePart()
    {
        var forager = new Species
        {
            Name = "Forager",
            BaseHealth = 10,
            BaseForage = 2,
            CurrentHealth = 10,
        };

        forager.ForageAction();
        Assert.That(forager.CurrentSize, Is.EqualTo(0));

        forager.Parts.Add(new Part { ActionType = SpeciesActionType.Forage });
        forager.ForageAction();

        Assert.That(forager.CurrentSize, Is.EqualTo(2));
    }
    [Test]
    public void HalfSizeFishDealHalfDamage()
    {
        var attacker = new Species
        {
            Name = "Attacker",
            BaseHealth = 10,
            BaseSize = 5,
            BaseAttack = 4,
            CurrentHealth = 10,
        };
        var target = new Species
        {
            Name = "Target",
            BaseHealth = 20,
            BaseSize = 10,
            CurrentHealth = 20,
        };

        attacker.AttackAction(target);

        Assert.That(target.CurrentHealth, Is.EqualTo(18));
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
        forager.Parts.Add(new Part { ActionType = SpeciesActionType.Forage });
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
        fish.Parts.Add(new Part { ActionType = SpeciesActionType.Forage });
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
        enemy.Parts.Add(new Part { ActionType = SpeciesActionType.Forage });
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

    [Test]
    public void BleedConsumesOneHealthAtTheStartOfThreeTurns()
    {
        var texture = new Texture2D(1, 1);
        var statusSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        var predatorPart = new Part
        {
            ActionType = SpeciesActionType.Attack,
            ActionIcon = statusSprite,
            Behaviors = new() { new BleedOnHitBehavior() },
        };
        var attacker = new Species
        {
            Name = "Predator",
            BaseHealth = 10,
            CurrentHealth = 10,
            BaseAttack = 3,
        };
        attacker.Parts.Add(predatorPart);
        var target = new Species { Name = "Target", BaseHealth = 10, CurrentHealth = 10 };

        attacker.AttackAction(target, predatorPart);

        Assert.That(target.CurrentHealth, Is.EqualTo(7));
        Assert.That(target.ActiveStatusEffects.Count, Is.EqualTo(1));
        Assert.That(target.ActiveStatusEffects[0].Sprite, Is.SameAs(statusSprite));
        Assert.That(target.ActiveStatusEffects[0].RemainingTurns, Is.EqualTo(3));

        target.OnTurnStart();
        Assert.That(target.CurrentHealth, Is.EqualTo(6));
        Assert.That(target.ActiveStatusEffects[0].RemainingTurns, Is.EqualTo(2));

        target.OnTurnStart();
        Assert.That(target.CurrentHealth, Is.EqualTo(5));

        target.OnTurnStart();
        Assert.That(target.CurrentHealth, Is.EqualTo(4));

        Object.DestroyImmediate(statusSprite);
        Object.DestroyImmediate(texture);
        Assert.That(target.ActiveStatusEffects.Count, Is.EqualTo(0));

        target.OnTurnStart();
        Assert.That(target.CurrentHealth, Is.EqualTo(4));
    }

    [Test]
    public void BleedOnlyAppliesWhenPredatorAttackDealsDamage()
    {
        var predatorPart = new Part
        {
            ActionType = SpeciesActionType.Attack,
            Behaviors = new() { new BleedOnHitBehavior() },
        };
        var attacker = new Species
        {
            Name = "Predator",
            BaseHealth = 10,
            CurrentHealth = 10,
            BaseAttack = 3,
        };
        attacker.Parts.Add(predatorPart);
        var target = new Species
        {
            Name = "Armored Target",
            BaseHealth = 10,
            CurrentHealth = 10,
            BaseDefense = 3,
        };

        attacker.AttackAction(target, predatorPart);

        Assert.That(target.CurrentHealth, Is.EqualTo(10));
        Assert.That(target.ActiveStatusEffects.Count, Is.EqualTo(0));
    }

    [Test]
    public void BleedRequiresPredatorAttackSourceWhenAnotherAttackIsSelected()
    {
        var predatorPart = new Part
        {
            ActionType = SpeciesActionType.Attack,
            Behaviors = new() { new BleedOnHitBehavior() },
        };
        var otherAttackPart = new Part { ActionType = SpeciesActionType.Attack };
        var attacker = new Species
        {
            Name = "Predator",
            BaseHealth = 10,
            CurrentHealth = 10,
            BaseAttack = 3,
        };
        attacker.Parts.Add(predatorPart);
        attacker.Parts.Add(otherAttackPart);
        var target = new Species { Name = "Target", BaseHealth = 10, CurrentHealth = 10 };

        attacker.AttackAction(target, otherAttackPart);

        Assert.That(target.CurrentHealth, Is.EqualTo(7));
        Assert.That(target.ActiveStatusEffects.Count, Is.EqualTo(0));

        var predatorTarget = new Species { Name = "Predator Target", BaseHealth = 10, CurrentHealth = 10 };
        attacker.AttackAction(predatorTarget, predatorPart);

        Assert.That(predatorTarget.ActiveStatusEffects.Count, Is.EqualTo(1));
    }
}
