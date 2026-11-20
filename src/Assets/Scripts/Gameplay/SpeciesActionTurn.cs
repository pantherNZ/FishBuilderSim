using System;
using System.Collections.Generic;

/// <summary>Ordered enemy actions to perform during one turn in its action sequence.</summary>
[Serializable]
public class SpeciesActionTurn
{
    public List<SpeciesActionType> Actions = new();

    public SpeciesActionTurn Clone()
    {
        return new SpeciesActionTurn
        {
            Actions = new List<SpeciesActionType>(Actions ?? new List<SpeciesActionType>()),
        };
    }
}
