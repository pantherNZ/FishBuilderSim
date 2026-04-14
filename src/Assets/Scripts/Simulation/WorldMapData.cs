using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// MapPoint — lightweight grid coordinate that works as a dictionary key.
// Kept in the Simulation layer so there is no UnityEngine dependency.
// ---------------------------------------------------------------------------

/// <summary>
/// Immutable 2-D integer grid coordinate used as the key in
/// <see cref="WorldMapData.Nodes"/>.
/// </summary>
public readonly struct MapPoint : IEquatable<MapPoint>
{
    public readonly int X;
    public readonly int Y;

    public MapPoint(int x, int y) { X = x; Y = y; }

    public bool Equals(MapPoint other) => X == other.X && Y == other.Y;
    public override bool Equals(object obj) => obj is MapPoint p && Equals(p);
    public override int GetHashCode() => X * 397 ^ Y;
    public override string ToString() => $"({X},{Y})";

    public static MapPoint operator +(MapPoint a, MapPoint b) => new(a.X + b.X, a.Y + b.Y);
}

// ---------------------------------------------------------------------------
// WorldMapData
// ---------------------------------------------------------------------------

/// <summary>
/// Generates and owns the full world map for a run.
/// Call <see cref="WorldMapData(System.Collections.Generic.IReadOnlyList{Encounter}, int?)"/>
/// once at game start and pass it to the <c>WorldMapPanel</c>.
/// </summary>
public class WorldMapData
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Distance between grid cells in canvas pixels.
    /// The UI multiplies node positions by this value when placing elements.
    /// </summary>
    public const int CellSize = 200;

    // ── Data ──────────────────────────────────────────────────────────────────

    /// <summary>All nodes, keyed by their logical grid position.</summary>
    public IReadOnlyDictionary<MapPoint, WorldMapNode> Nodes => _nodes;

    /// <summary>The special start node where the player begins.</summary>
    public WorldMapNode StartNode { get; private set; }

    /// <summary>The node the player is currently at.</summary>
    public WorldMapNode PlayerNode { get; private set; }

    // ── Internals ─────────────────────────────────────────────────────────────

    readonly Dictionary<MapPoint, WorldMapNode> _nodes = new();

    // ── Construction ──────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a world map from the provided encounter list.
    /// Encounters are laid out left-to-right in columns with slight vertical
    /// variation so the map looks like an open ocean chart.
    /// </summary>
    /// <param name="encounters">The ordered list of encounters for this run.</param>
    /// <param name="seed">Optional RNG seed (reproducible maps for a given run).</param>
    public WorldMapData(IReadOnlyList<Encounter> encounters, int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        GenerateLayout(encounters, rng);
    }

    // ── Layout generation ─────────────────────────────────────────────────────

    void GenerateLayout(IReadOnlyList<Encounter> encounters, Random rng)
    {
        // Place the start node in the upper half of a 10-row grid.
        const int centerRow = 4;
        var startPos = new MapPoint(0, centerRow);

        StartNode = new WorldMapNode
        {
            Position = startPos,
            Type = WorldMapNodeType.Start,
            IsVisited = true,
            IsAccessible = true,
        };
        Add(StartNode);
        PlayerNode = StartNode;

        // Each encounter occupies its own column, spaced 3 cells apart.
        // Rows are distributed with Gaussian-ish tendency toward the centre row.
        WorldMapNode prevNode = StartNode;

        for (int i = 0; i < encounters.Count; i++)
        {
            int col = 3 + i * 3;                          // cols 3, 6, 9, 12, …
            int row = SampleRow(rng, centerRow, rows: 8); // rows 0-7

            // Avoid exact collisions (very unlikely but defensive).
            var pos = new MapPoint(col, row);
            while (_nodes.ContainsKey(pos))
                pos = new MapPoint(col, pos.Y + 1);

            bool isBoss = i == encounters.Count - 1;
            bool isElite = !isBoss && i % 3 == 2;

            var enc = encounters[i];
            var node = new WorldMapNode
            {
                Position = pos,
                Encounter = enc,
                Type = isBoss ? WorldMapNodeType.Boss :
                               isElite ? WorldMapNodeType.Elite :
                                         WorldMapNodeType.Combat,
                IsVisited = enc.IsCompleted && enc.PlayerWon,
                IsAccessible = i == 0 || (encounters[i - 1].IsCompleted),
            };

            Add(node);

            // Bidirectional connection for line drawing.
            prevNode.Connections.Add(pos);
            node.Connections.Add(prevNode.Position);

            // Advance player node to the last visited node.
            if (node.IsVisited)
                PlayerNode = node;

            prevNode = node;
        }
    }

    // ── Mutation ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Update the player's current position on the map.
    /// Also marks the destination node as visited and updates accessibility of
    /// all subsequent nodes.
    /// </summary>
    public void MovePlayerTo(WorldMapNode destination)
    {
        PlayerNode = destination;
        destination.IsVisited = true;

        // Open up direct neighbours of the new position.
        foreach (var connPos in destination.Connections)
        {
            if (_nodes.TryGetValue(connPos, out var neighbour) && !neighbour.IsVisited)
                neighbour.IsAccessible = true;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void Add(WorldMapNode node) => _nodes[node.Position] = node;

    /// <summary>Sample a row biased toward <paramref name="center"/>.</summary>
    static int SampleRow(Random rng, int center, int rows)
    {
        // Average three uniform samples — cheap approximation of a bell curve.
        int a = rng.Next(0, rows);
        int b = rng.Next(0, rows);
        int c = rng.Next(0, rows);
        int raw = (a + b + c) / 3;
        // Clamp away from the very edges so nodes aren't cut off.
        return Math.Clamp(raw, 1, rows - 2);
    }
}
