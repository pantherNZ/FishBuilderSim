using System;
using System.Collections.Generic;
using System.Linq;

public enum SpeciesActionType
{
    None,
    Attack,
    Forage,
    Defend,
}

public struct SpeciesAction
{
    public Species Actor;
    public SpeciesActionType Type;
    public List<Species> Targets;
}

public class ActionManager
{
    readonly List<SpeciesAction> _actions = new();

    public int MaxActions { get; private set; }

    public IReadOnlyList<SpeciesAction> Actions => _actions;

    public ActionManager(int maxActions = 1)
    {
        MaxActions = Math.Max(1, maxActions);
    }

    public void SetMaxActions(int maxActions)
    {
        MaxActions = Math.Max(1, maxActions);
        TrimOverflow();
    }

    public void Clear() => _actions.Clear();

    public bool RemoveActionForActor(Species actor)
    {
        if (actor == null)
            return false;

        int index = _actions.FindIndex(a => a.Actor == actor);
        if (index < 0)
            return false;

        _actions.RemoveAt(index);
        return true;
    }

    public void SetAction(SpeciesAction action)
    {
        if (action.Actor == null)
            return;

        RemoveActionForActor(action.Actor);
        _actions.Add(action);
        TrimOverflow();
    }

    public bool TryGetActionForActor(Species actor, out SpeciesAction action)
    {
        int index = _actions.FindIndex(a => a.Actor == actor);
        if (index >= 0)
        {
            action = _actions[index];
            return true;
        }

        action = default;
        return false;
    }

    void TrimOverflow()
    {
        while (_actions.Count > MaxActions)
            _actions.RemoveAt(0);
    }
}
