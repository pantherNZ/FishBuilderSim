using NUnit.Framework;

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
}
