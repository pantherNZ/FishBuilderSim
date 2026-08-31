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

}
