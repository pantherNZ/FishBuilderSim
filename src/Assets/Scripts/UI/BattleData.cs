using System.Collections.Generic;

/// <summary>
/// Data passed to <see cref="BattlePanel.Show"/> to initialise the battle UI.
/// </summary>
public class BattleData
{
    /// <summary>All species on the player's side (drives health bars and tooltips).</summary>
    public List<Species> PlayerGroup = new();

    /// <summary>All enemy species in this encounter (drives health bars and tooltips).</summary>
    public List<Species> EnemyGroup = new();

    /// <summary>
    /// The player's parts available as playable action cards during the encounter.
    /// Typically the currently equipped parts pulled from <see cref="PlayerInventory"/>.
    /// </summary>
    public List<Part> ActionCards = new();
}
