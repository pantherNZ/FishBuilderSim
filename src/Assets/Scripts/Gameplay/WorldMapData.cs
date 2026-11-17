using System.Collections.Generic;
using Runtime.Game;
using UnityEngine;

// ---------------------------------------------------------------------------
// MapPoint — lightweight grid coordinate that works as a dictionary key.
// Kept in the Simulation layer so there is no UnityEngine dependency.
// ---------------------------------------------------------------------------

/// <summary>
/// Immutable 2-D integer grid coordinate used as the key in
/// <see cref="WorldMapData.Nodes"/>.
/// </summary>
public readonly struct MapPoint : System.IEquatable<MapPoint>
{
    public readonly int X;
    public readonly int Y;

    public MapPoint(int x, int y) { X = x; Y = y; }

    public bool Equals(MapPoint other) => X == other.X && Y == other.Y;
    public override bool Equals(object obj) => obj is MapPoint p && Equals(p);
    public override int GetHashCode() => X * 397 ^ Y;
    public override string ToString() => $"({X},{Y})";
    public static implicit operator UnityEngine.Vector2Int(MapPoint p) => new(p.X, p.Y);

    public static MapPoint operator +(MapPoint a, MapPoint b) => new(a.X + b.X, a.Y + b.Y);
}

// ---------------------------------------------------------------------------
// WorldMapData
// ---------------------------------------------------------------------------

/// <summary>
/// Generates and owns the full world map for a run.
/// Call <see cref="WorldMapData(WeightedSelector{EncounterSchema}, int?, IReadOnlyList{EncounterSchema})"/>
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

    public const int MaxEncounterCount = 100;

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
    /// <param name="startingEncounters">Ordered encounter schemas that should appear first on the map.</param>
    /// <param name="shopSchema">Optional configured shop inserted once in a seeded ring 2-4.</param>
    public WorldMapData(
        WeightedSelector<EncounterSchema> encounters,
        int? seed = null,
        IReadOnlyList<EncounterSchema> startingEncounters = null,
        ShopEncounterSchema shopSchema = null)
    {
        Utility.IRandom rng = seed.HasValue ? new Utility.SeededRandom(seed.Value) : new Utility.UnityRandom();
        GenerateLayout(encounters, rng, startingEncounters, shopSchema);
    }

    // ── Layout generation ─────────────────────────────────────────────────────

    void GenerateLayout(
        WeightedSelector<EncounterSchema> encounters,
        Utility.IRandom rng,
        IReadOnlyList<EncounterSchema> startingEncounters,
        ShopEncounterSchema shopSchema)
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

        var pickedSchemas = new List<EncounterSchema>();
        var startingSchemaSet = new HashSet<EncounterSchema>();

        if (startingEncounters != null)
        {
            foreach (var schema in startingEncounters)
            {
                if (pickedSchemas.Count >= MaxEncounterCount)
                    break;

                if (schema == null || !schema.HasEnemyDefinition || !startingSchemaSet.Add(schema))
                    continue;

                pickedSchemas.Add(schema);
            }
        }

        bool reserveShopSlot = shopSchema != null && !pickedSchemas.Contains(shopSchema);
        int randomEncounterLimit = MaxEncounterCount - (reserveShopSlot ? 1 : 0);
        while (encounters != null && encounters.HasResult() && pickedSchemas.Count < randomEncounterLimit)
        {
            var schema = encounters.GetResult();
            if (schema == null || !schema.HasEnemyDefinition)
                break;

            pickedSchemas.Add(schema);
        }

        if (reserveShopSlot && pickedSchemas.Count < MaxEncounterCount)
        {
            const int shopFirstRingCapacity = 8;
            const int secondRingCapacity = 16;
            const int thirdRingCapacity = 24;
            const int fourthRingCapacity = 32;
            int minimumIndex = Mathf.Min(shopFirstRingCapacity, pickedSchemas.Count);
            int maximumIndex = Mathf.Min(
                pickedSchemas.Count,
                shopFirstRingCapacity + secondRingCapacity + thirdRingCapacity + fourthRingCapacity - 1);
            int insertionIndex = minimumIndex <= maximumIndex
                ? rng.Range(minimumIndex, maximumIndex + 1)
                : pickedSchemas.Count;
            pickedSchemas.Insert(insertionIndex, shopSchema);
        }

        if (pickedSchemas.Count == 0)
            return;

        const int firstRingCapacity = 8;
        const float baseRadius = 4f;
        const float ringSpacing = 3f;
        const int initiallyAccessibleEncounters = 1;

        int schemaIndex = 0;
        int ringIndex = 0;

        while (schemaIndex < pickedSchemas.Count)
        {
            ringIndex++;
            int ringCapacity = firstRingCapacity * ringIndex;
            int ringNodeCount = Mathf.Min(ringCapacity, pickedSchemas.Count - schemaIndex);

            float ringOffset = rng.value * (Mathf.PI * 2.0f);
            var ringNodes = new List<WorldMapNode>(ringNodeCount);

            for (int i = 0; i < ringNodeCount; i++)
            {
                float t = i / (float)ringCapacity;
                float angle = ringOffset + t * (Mathf.PI * 2.0f);
                float angleJitter = (rng.value - 0.5f) * ((Mathf.PI * 2.0f) / (ringCapacity * 6.0f));
                angle += angleJitter;

                float radius = baseRadius + (ringIndex - 1) * ringSpacing + (rng.value - 0.5f) * 0.35f;
                int x = (int)Mathf.Round(Mathf.Cos(angle) * radius);
                int y = (int)Mathf.Round(Mathf.Sin(angle) * radius);

                var desiredPos = new MapPoint(x, y);
                var pos = FindNearestFreePosition(desiredPos, rng);

                var schema = pickedSchemas[schemaIndex];
                int encounterOrder = schemaIndex;
                var encounter = schema.CreateEncounter();
                bool isStartingEncounter = startingSchemaSet.Contains(schema);
                if (!isStartingEncounter)
                    ScaleEncounter(encounter, ((UnityEngine.Vector2Int)pos).magnitude);

                bool isShop = schema is ShopEncounterSchema || encounter is ShopEncounter;
                bool isBoss = !isShop && schemaIndex == pickedSchemas.Count - 1;
                bool isElite = !isBoss && schemaIndex % 4 == 3;
                schemaIndex++;

                var node = new WorldMapNode
                {
                    Position = pos,
                    Depth = ringIndex,
                    Encounter = encounter,
                    Type = isShop ? WorldMapNodeType.Shop :
                              isBoss ? WorldMapNodeType.Boss :
                                   isElite ? WorldMapNodeType.Elite :
                                             WorldMapNodeType.Combat,
                    IsVisited = encounter.IsCompleted && encounter.PlayerWon,
                    IsAccessible = encounterOrder < initiallyAccessibleEncounters,
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

    static void ScaleEncounter(Encounter encounter, float distanceFromStart)
    {
        if (encounter?.EnemyGroup?.Members == null || distanceFromStart <= 0f)
            return;

        var constants = GlobalConstantsHandler.Constants;
        float multiplier = 1f + constants.EncounterDifficultyScaling.Evaluate(distanceFromStart);
        foreach (var species in encounter.EnemyGroup.Members)
        {
            if (species == null)
                continue;

            species.BaseHealth = ScaleStat(species.BaseHealth, multiplier);
            species.BaseSize = ScaleStat(species.BaseSize, multiplier);
            species.BaseAttack = ScaleStat(species.BaseAttack, multiplier);
            species.BaseDefense = ScaleStat(species.BaseDefense, multiplier);
            species.BaseForage = ScaleStat(species.BaseForage, multiplier);
        }
    }

    static int ScaleStat(int value, float distanceFromStart, AnimationCurve scalingCurve)
    {
        if (value <= 0 || scalingCurve == null)
            return value;

        float multiplier = 1f + scalingCurve.Evaluate(distanceFromStart);
        return ScaleStat(value, multiplier);
    }
    static int ScaleStat(int value, float multiplier)
    {
        if (value <= 0)
            return value;

        return Mathf.Max(value, (int)Mathf.Round(value * multiplier));
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

    MapPoint FindNearestFreePosition(MapPoint desired, Utility.IRandom rng)
    {
        if (!_nodes.ContainsKey(desired))
            return desired;

        for (int radius = 1; radius <= 4; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                        continue;

                    var candidate = new MapPoint(desired.X + dx, desired.Y + dy);
                    if (!_nodes.ContainsKey(candidate))
                        return candidate;
                }
            }
        }

        // Extremely rare fallback if the local neighborhood is packed.
        int offset = _nodes.Count + 1;
        int sign = rng.value < 0.5f ? -1 : 1;
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
