using System.Collections.Generic;
using System.Linq;

public class SpeciesGroup
{
    public string Name;
    public List<Species> Members = new List<Species>();

    public SpeciesGroup(string name)
    {
        Name = name;
    }

    public SpeciesGroup(string name, IEnumerable<Species> members)
    {
        Name = name;
        Members = new List<Species>(members);
        BindMembers();
    }

    public void Add(Species species)
    {
        if (species == null)
            return;

        Members.Add(species);
        species.Group = this;
    }

    public void Remove(Species species)
    {
        if (!Members.Remove(species))
            return;

        if (species?.Group == this)
            species.Group = null;
    }

    public IEnumerable<Species> Alive => Members.Where(s => s.IsAlive);

    public bool HasAlive => Members.Any(s => s.IsAlive);

    public void Initialize()
    {
        BindMembers();
        foreach (var member in Members)
            member.Initialize();
    }

    public void OnEncounterStart(SpeciesGroup enemy)
    {
        BindMembers();
        foreach (var member in Members)
            member.OnEncounterStart(enemy);
    }

    public void OnTickStart()
    {
        foreach (var member in Members)
            member.OnTickStart();
    }

    public void OnTurnStart()
    {
        foreach (var member in Members)
            if (member != null && member.IsAlive)
                member.OnTurnStart();
    }

    public void ClearTemporaryStatModifiers()
    {
        foreach (var member in Members)
            member?.ClearTemporaryStatModifiers();
    }

    public void OnEnemyForaged(Species enemy)
    {
        foreach (var member in Members)
            if (member != null && member.IsAlive)
                member.OnEnemyForaged(enemy);
    }

    public void OnTickEnd()
    {
        foreach (var member in Members)
            member.OnTickEnd();
    }

    void BindMembers()
    {
        foreach (var member in Members)
            if (member != null)
                member.Group = this;
    }
}
