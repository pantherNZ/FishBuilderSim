public class RazorJaws : Part
{
    public RazorJaws()
    {
        Name = "Razor Jaws";
        Rarity = PartRarity.Common;
        Attack = 3;
    }
}

public class FilterFeeder : Part
{
    public FilterFeeder()
    {
        Name = "Filter Feeder";
        Rarity = PartRarity.Common;
        Forage = 2;
    }
}

public class ArmoredScales : Part
{
    public ArmoredScales()
    {
        Name = "Armored Scales";
        Rarity = PartRarity.Uncommon;
        Defense = 2;
    }
}

public class SpikedBody : Part
{
    public SpikedBody()
    {
        Name = "Spiked Body";
        Rarity = PartRarity.Rare;
    }

    public override void OnDefend(Species self, Species attacker, ref int damage)
    {
        // Reflect 1 damage back
        attacker.CurrentHealth -= 1;
    }
}

public class Frenzy : Part
{
    public Frenzy()
    {
        Name = "Frenzy";
        Rarity = PartRarity.Epic;
    }

    public override void OnAttack(Species self, Species enemy, ref int damage)
    {
        if (self.CurrentHealth < self.MaxHealth / 2)
        {
            damage += 2;
        }
    }
}