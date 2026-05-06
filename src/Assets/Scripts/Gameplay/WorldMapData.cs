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
/// Call <see cref="WorldMapData(WeightedSelector{EncounterSchema}, int?)"/>
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
    /// Encounters are laid out in fairly even rings around the start node.
    /// </summary>
    /// <param name="encounters">Weighted encounter selector for this run.</param>
    /// <param name="seed">Optional RNG seed (reproducible maps for a given run).</param>
    public WorldMapData(WeightedSelector<EncounterSchema> encounters, int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        GenerateLayout(encounters, rng);
    }

    // ── Layout generation ─────────────────────────────────────────────────────

    void GenerateLayout(WeightedSelector<EncounterSchema> encounters, Random rng)
    {
        _nodes.Clear();

        var startPos = new MapPoint(0, 0);

        StartNode = new WorldMapNode
        {
            Position = startPos,
            Type = WorldMapNodeType.Start,
            IsVisited = true,
            IsAccessible = true,
        };
        Add(StartNode);
        PlayerNode = StartNode;

        if (encounters == null || !encounters.HasResult())
            return;

        var pickedSchemas = new List<EncounterSchema>();
        while (encounters.HasResult())
        {
            var schema = encounters.TakeResult();
            if (schema != null && schema.EnemyGroup != null)
                pickedSchemas.Add(schema);
        }

        if (pickedSchemas.Count == 0)
            return;

        const int firstRingCapacity = 8;
        const float baseRadius = 4f;
        const float ringSpacing = 3f;

        int schemaIndex = 0;
        int ringIndex = 0;

        while (schemaIndex < pickedSchemas.Count)
        {
            ringIndex++;
            int ringCapacity = firstRingCapacity * ringIndex;
            int ringNodeCount = Math.Min(ringCapacity, pickedSchemas.Count - schemaIndex);

            double ringOffset = rng.NextDouble() * (Math.PI * 2.0);
            var ringNodes = new List<WorldMapNode>(ringNodeCount);

            for (int i = 0; i < ringNodeCount; i++)
            {
                double t = i / (double)ringCapacity;
                double angle = ringOffset + t * (Math.PI * 2.0);
                double angleJitter = (rng.NextDouble() - 0.5) * ((Math.PI * 2.0) / (ringCapacity * 6.0));
                angle += angleJitter;

                double radius = baseRadius + (ringIndex - 1) * ringSpacing + (rng.NextDouble() - 0.5) * 0.35;
                int x = (int)Math.Round(Math.Cos(angle) * radius);
                int y = (int)Math.Round(Math.Sin(angle) * radius);

                var desiredPos = new MapPoint(x, y);
                var pos = FindNearestFreePosition(desiredPos, rng);

                bool isBoss = schemaIndex == pickedSchemas.Count - 1;
                bool isElite = !isBoss && schemaIndex % 4 == 3;

                var encounter = pickedSchemas[schemaIndex].CreateEncounter();
                schemaIndex++;

                var node = new WorldMapNode
                {
                    Position = pos,
                    Encounter = encounter,
                    Type = isBoss ? WorldMapNodeType.Boss :
                                   isElite ? WorldMapNodeType.Elite :
                                             WorldMapNodeType.Combat,
                    IsVisited = encounter.IsCompleted && encounter.PlayerWon,
                    IsAccessible = true,
                };

                Add(node);
                Connect(StartNode, node);
                ringNodes.Add(node);

                if (node.IsVisited)
                    PlayerNode = node;
            }

            // Connect each ring into a loop to keep traversal costs meaningful.
            for (int i = 0; i < ringNodes.Count - 1; i++)
                Connect(ringNodes[i], ringNodes[i + 1]);

            if (ringNodes.Count > 2)
                Connect(ringNodes[^1], ringNodes[0]);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void Add(WorldMapNode node) => _nodes[node.Position] = node;

    void Connect(WorldMapNode a, WorldMapNode b)
    {
        if (a == null || b == null)
            return;

        if (!a.Connections.Contains(b.Position))
            a.Connections.Add(b.Position);

        if (!b.Connections.Contains(a.Position))
            b.Connections.Add(a.Position);
    }

    MapPoint FindNearestFreePosition(MapPoint desired, Random rng)
    {
        if (!_nodes.ContainsKey(desired))
            return desired;

        for (int radius = 1; radius <= 4; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                        continue;

                    var candidate = new MapPoint(desired.X + dx, desired.Y + dy);
                    if (!_nodes.ContainsKey(candidate))
                        return candidate;
                }
            }
        }

        // Extremely rare fallback if the local neighborhood is packed.
        int offset = _nodes.Count + 1;
        int sign = rng.NextDouble() < 0.5 ? -1 : 1;
        return new MapPoint(desired.X + sign * offset, desired.Y - sign * offset);
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
}
