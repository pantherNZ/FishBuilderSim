using System;
using System.Linq;

public class CombatSimulator
{
    // Single species overload — wraps each species in a one-member group
    public static void Run(Species a, Species b, int maxTicks = 20)
    {
        Run(new SpeciesGroup(a.Name, new[] { a }),
            new SpeciesGroup(b.Name, new[] { b }),
            maxTicks);
    }

    // Group vs group overload
    public static void Run(SpeciesGroup groupA, SpeciesGroup groupB, int maxTicks = 20)
    {
        groupA.Initialize();
        groupB.Initialize();

        // Trigger OnCombatStart for every member against every enemy
        foreach (var a in groupA.Members)
        {
            foreach (var b in groupB.Members)
            {
                a.OnCombatStart(b);
                b.OnCombatStart(a);
            }
        }

        Console.WriteLine($"--- Combat Start: {groupA.Name} vs {groupB.Name} ---");

        for (int tick = 1; tick <= maxTicks; tick++)
        {
            if (!groupA.HasAlive || !groupB.HasAlive)
                break;

            Console.WriteLine($"\nTick {tick}");
            Tick(groupA, groupB);
        }

        Console.WriteLine("\n--- Combat End ---");

        if (groupA.HasAlive && !groupB.HasAlive)
            Console.WriteLine($"{groupA.Name} wins!");
        else if (groupB.HasAlive && !groupA.HasAlive)
            Console.WriteLine($"{groupB.Name} wins!");
        else
            Console.WriteLine("Draw!");
    }

    static void Tick(SpeciesGroup groupA, SpeciesGroup groupB)
    {
        // TickStart for each alive member against a random alive enemy
        foreach (var a in groupA.Alive.ToList())
        {
            var target = groupB.Alive.FirstOrDefault();
            if (target != null)
                a.TickStart(target);
        }
        foreach (var b in groupB.Alive.ToList())
        {
            var target = groupA.Alive.FirstOrDefault();
            if (target != null)
                b.TickStart(target);
        }

        // Forage phase
        foreach (var a in groupA.Alive.ToList())
            a.ApplyForage();
        foreach (var b in groupB.Alive.ToList())
            b.ApplyForage();

        foreach (var a in groupA.Alive)
            Console.WriteLine($"  [{groupA.Name}] {a.Name} size: {a.Size}, health: {a.CurrentHealth}");
        foreach (var b in groupB.Alive)
            Console.WriteLine($"  [{groupB.Name}] {b.Name} size: {b.Size}, health: {b.CurrentHealth}");

        // Attack phase — each alive member picks a target based on its AttackBehavior
        foreach (var a in groupA.Alive.ToList())
        {
            var target = a.PickTarget(groupB.Alive);
            if (target != null && a.Size >= target.Size)
                a.AttackTarget(target);
        }
        foreach (var b in groupB.Alive.ToList())
        {
            var target = b.PickTarget(groupA.Alive);
            if (target != null && b.IsAlive && b.Size >= target.Size)
                b.AttackTarget(target);
        }

        // TickEnd
        foreach (var a in groupA.Alive.ToList())
        {
            var target = groupB.Alive.FirstOrDefault();
            if (target != null)
                a.TickEnd(target);
        }
        foreach (var b in groupB.Alive.ToList())
        {
            var target = groupA.Alive.FirstOrDefault();
            if (target != null)
                b.TickEnd(target);
        }

        foreach (var a in groupA.Members)
            Console.WriteLine($"  [{groupA.Name}] {a.Name} HP: {a.CurrentHealth}");
        foreach (var b in groupB.Members)
            Console.WriteLine($"  [{groupB.Name}] {b.Name} HP: {b.CurrentHealth}");
    }
}