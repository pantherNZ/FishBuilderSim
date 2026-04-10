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
    }

    public void Add(Species species) => Members.Add(species);

    public void Remove(Species species) => Members.Remove(species);

    public IEnumerable<Species> Alive => Members.Where(s => s.IsAlive);

    public bool HasAlive => Members.Any(s => s.IsAlive);

    public void Initialize()
    {
        foreach (var member in Members)
            member.Initialize();
    }
}
