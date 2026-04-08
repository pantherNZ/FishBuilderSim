using System;

public class CombatSimulator
{
    public static void Run(Species a, Species b, int maxTicks = 20)
    {
        a.Initialize();
        b.Initialize();

        a.OnCombatStart(b);
        b.OnCombatStart(a);

        Console.WriteLine($"--- Combat Start: {a.Name} vs {b.Name} ---");

        for (int tick = 1; tick <= maxTicks; tick++)
        {
            if (!a.IsAlive || !b.IsAlive)
                break;

            Console.WriteLine($"\nTick {tick}");

            a.TickStart(b);
            b.TickStart(a);

            // Forage phase
            a.ApplyForage();
            b.ApplyForage();

            Console.WriteLine($"{a.Name} size: {a.Size}, health: {a.CurrentHealth}");
            Console.WriteLine($"{b.Name} size: {b.Size}, health: {b.CurrentHealth}");

            // Attack phase
            if (a.Size >= b.Size)
                a.AttackTarget(b);

            if (b.IsAlive && b.Size >= a.Size)
                b.AttackTarget(a);

            a.TickEnd(b);
            b.TickEnd(a);

            Console.WriteLine($"{a.Name} HP: {a.CurrentHealth}");
            Console.WriteLine($"{b.Name} HP: {b.CurrentHealth}");
        }

        Console.WriteLine("\n--- Combat End ---");

        if (a.IsAlive && !b.IsAlive)
            Console.WriteLine($"{a.Name} wins!");
        else if (b.IsAlive && !a.IsAlive)
            Console.WriteLine($"{b.Name} wins!");
        else
            Console.WriteLine("Draw!");
    }
}