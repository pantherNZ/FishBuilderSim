public class RazorJaws : Part
{
    public RazorJaws()
    {
        Name = "Razor Jaws";
        Attack = 3;
    }
}

public class FilterFeeder : Part
{
    public FilterFeeder()
    {
        Name = "Filter Feeder";
        Forage = 2;
    }
}

public class ArmoredScales : Part
{
    public ArmoredScales()
    {
        Name = "Armored Scales";
        Defense = 2;
    }
}

public class SpikedBody : Part
{
    public SpikedBody()
    {
        Name = "Spiked Body";
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
    }

    public override void OnAttack(Species self, Species enemy, ref int damage)
    {
        if (self.CurrentHealth < self.MaxHealth / 2)
        {
            damage += 2;
        }
    }
}