using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CombatSimulator
{
    // Single species overload — wraps each species in a one-member group
    public static void Run(Species a, Species b, int maxTicks = 20, bool logging = false)
    {
        var groupA = new SpeciesGroup(a.Name, new[] { a });
        var groupB = new SpeciesGroup(b.Name, new[] { b });
        var manager = new ActionManager(maxActions: 1);

        var target = b.IsAlive ? b : null;
        manager.SetAction(new SpeciesAction
        {
            Actor = a,
            Type = a.Attack > 0 && a.CanAttack ? SpeciesActionType.Attack
                : (a.Forage > 0 && a.CanForage ? SpeciesActionType.Forage : SpeciesActionType.Defend),
            Targets = target != null ? new List<Species> { target } : null,
        });

        Run(groupA, groupB, manager, maxTicks, logging);
    }

    public static void Run(SpeciesGroup groupA, SpeciesGroup groupB, int maxTicks = 20, bool logging = false)
    {
        Run(groupA, groupB, null, maxTicks, logging);
    }

    // Group vs group overload
    public static void Run(SpeciesGroup groupA, SpeciesGroup groupB, ActionManager actionManager, int maxTicks = 20, bool logging = false)
    {
        groupA.OnEncounterStart(groupB);
        groupB.OnEncounterStart(groupA);

        if (logging)
            Debug.Log($"--- Combat Start: {groupA.Name} vs {groupB.Name} ---");

        for (int tick = 1; tick <= maxTicks; tick++)
        {
            if (!groupA.HasAlive || !groupB.HasAlive)
                break;

            if (logging)
                Debug.Log($"\nTick {tick}");
            Tick(groupA, groupB, actionManager, logging);
        }

        if (logging)
        {
            Debug.Log("\n--- Combat End ---");

            if (groupA.HasAlive && !groupB.HasAlive)
                Debug.Log($"{groupA.Name} wins!");
            else if (groupB.HasAlive && !groupA.HasAlive)
                Debug.Log($"{groupB.Name} wins!");
            else
                Debug.Log("Draw!");
        }
    }

    public static void ExecuteActions(SpeciesGroup actingGroup, SpeciesGroup opposingGroup, ActionManager actionManager, bool logging = false)
    {
        if (actingGroup == null || opposingGroup == null)
            return;

        ResolveActions(actingGroup, opposingGroup, actionManager);

        if (!logging)
            return;

        foreach (var member in actingGroup.Members)
            Debug.Log($"  [{actingGroup.Name}] {member.Name} HP: {member.CurrentHealth}");
        foreach (var member in opposingGroup.Members)
            Debug.Log($"  [{opposingGroup.Name}] {member.Name} HP: {member.CurrentHealth}");
    }

    static void Tick(SpeciesGroup groupA, SpeciesGroup groupB, ActionManager actionManager, bool logging)
    {
        groupA.OnTickStart();
        groupB.OnTickStart();

        if (logging)
        {
            foreach (var a in groupA.Alive)
                Debug.Log($"  [{groupA.Name}] {a.Name} size: {a.Size}, health: {a.CurrentHealth}");
            foreach (var b in groupB.Alive)
                Debug.Log($"  [{groupB.Name}] {b.Name} size: {b.Size}, health: {b.CurrentHealth}");
        }

        ResolveActions(groupA, groupB, actionManager);

        groupA.OnTickEnd();
        groupB.OnTickEnd();

        if (logging)
        {
            foreach (var a in groupA.Members)
                Debug.Log($"  [{groupA.Name}] {a.Name} HP: {a.CurrentHealth}");
            foreach (var b in groupB.Members)
                Debug.Log($"  [{groupB.Name}] {b.Name} HP: {b.CurrentHealth}");
        }
    }

    static void ResolveActions(SpeciesGroup actingGroup, SpeciesGroup opposingGroup, ActionManager actionManager)
    {
        IReadOnlyList<SpeciesAction> actions = actionManager?.Actions;
        if (actions == null || actions.Count == 0)
            actions = BuildDefaultActions(actingGroup, opposingGroup);

        foreach (var action in actions)
        {
            if (action.Actor == null || !action.Actor.IsAlive)
                continue;

            switch (action.Type)
            {
                case SpeciesActionType.Forage:
                    if (action.Actor.Forage > 0 && action.Actor.CanForage)
                        action.Actor.ForageAction();
                    break;
                case SpeciesActionType.Defend:
                    if (action.Actor.CanDefend)
                        action.Actor.DefendAction();
                    break;
                case SpeciesActionType.Attack:
                    if (action.Actor.Attack <= 0 || !action.Actor.CanAttack)
                        break;

                    var targets = action.Targets;
                    if (targets == null || targets.Count == 0)
                    {
                        var fallbackTarget = action.Actor.PickTarget(opposingGroup.Alive);
                        if (fallbackTarget != null)
                            action.Actor.AttackAction(fallbackTarget);
                        break;
                    }

                    bool attackedAny = false;

                    foreach (var target in targets)
                    {
                        if (target != null && target.IsAlive)
                        {
                            action.Actor.AttackAction(target);
                            attackedAny = true;
                        }
                    }

                    if (!attackedAny)
                    {
                        var fallbackTarget = action.Actor.PickTarget(opposingGroup.Alive);
                        if (fallbackTarget != null)
                            action.Actor.AttackAction(fallbackTarget);
                    }
                    break;
            }
        }
    }

    static IReadOnlyList<SpeciesAction> BuildDefaultActions(SpeciesGroup groupA, SpeciesGroup groupB)
    {
        var actor = groupA.Alive.FirstOrDefault();
        if (actor == null)
            return Array.Empty<SpeciesAction>();

        var actionType = actor.Attack > 0 && actor.CanAttack
            ? SpeciesActionType.Attack
            : (actor.Forage > 0 && actor.CanForage ? SpeciesActionType.Forage : SpeciesActionType.Defend);

        List<Species> targets = null;
        if (actionType == SpeciesActionType.Attack)
        {
            var target = actor.PickTarget(groupB.Alive);
            if (target != null)
                targets = new List<Species> { target };
        }

        return new[]
        {
            new SpeciesAction
            {
                Actor = actor,
                Type = actionType,
                Targets = targets,
            },
        };
    }
}