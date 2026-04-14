using System;
using System.Collections.Generic;

/// <summary>
/// The type of activity at a map node.
/// </summary>
public enum WorldMapNodeType
{
    Start,
    Combat,
    Elite,
    Boss,
}

/// <summary>
/// A single location on the world map. Holds its logical grid position,
/// the <see cref="Encounter"/> it triggers, and connection information to
/// adjacent nodes.
/// </summary>
public class WorldMapNode
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Logical grid position (used as the dictionary key in WorldMapData).</summary>
    public MapPoint Position;

    public WorldMapNodeType Type;

    // ── Encounter data ────────────────────────────────────────────────────────

    /// <summary>
    /// The encounter the player enters at this node.
    /// Null only for the <see cref="WorldMapNodeType.Start"/> node.
    /// </summary>
    public Encounter Encounter;

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>True once the player has cleared this node.</summary>
    public bool IsVisited;

    /// <summary>True if the player can currently travel to this node.</summary>
    public bool IsAccessible;

    // ── Connectivity ──────────────────────────────────────────────────────────

    /// <summary>Grid positions of all nodes directly connected to this one.</summary>
    public readonly List<MapPoint> Connections = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Display name shown on the map.</summary>
    public string DisplayName => Type switch
    {
        WorldMapNodeType.Start => "HOME WATERS",
        WorldMapNodeType.Boss => encounter_name("ALPHA "),
        WorldMapNodeType.Elite => encounter_name("RIVAL "),
        _ => encounter_name(string.Empty),
    };

    string encounter_name(string prefix) =>
        Encounter?.EnemyGroup?.Name ?? $"{prefix}ENCOUNTER";
}
