using System.Collections.Generic;

public enum BattleStepAction
{
    Attack,
    Forage,
    Defend,
    Blind,
}

public class BattleStepRequest
{
    public Species Actor;
    public BattleStepAction Action;
    public ActionManager ActionManager;
}

/// <summary>
/// Data passed to <see cref="BattlePanel.Show"/> to initialise the battle UI.
/// </summary>
public class BattleData
{
    /// <summary>All species on the player's side (drives health bars and tooltips).</summary>
    public List<Species> PlayerGroup = new();

    /// <summary>All enemy species in this encounter (drives health bars and tooltips).</summary>
    public List<Species> EnemyGroup = new();

    /// <summary>Checks target allegiance against the action's current combat rules.</summary>
    public bool IsValidActionTarget(Species actor, SpeciesActionType actionType, Species target)
    {
        if (actor == null || !actor.IsAlive || target == null || !target.IsAlive
            || PlayerGroup == null || !PlayerGroup.Contains(actor))
        {
            return false;
        }

        switch (actionType)
        {
            case SpeciesActionType.Attack:
            case SpeciesActionType.Blind:
                return EnemyGroup != null && EnemyGroup.Contains(target) && !PlayerGroup.Contains(target);
            case SpeciesActionType.Defend:
            case SpeciesActionType.Forage:
                return target == actor;
            default:
                return false;
        }
    }

}
