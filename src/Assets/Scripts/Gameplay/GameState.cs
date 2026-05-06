using System;
using System.Collections.Generic;
using System.Linq;
using Runtime.Game;
using Schema;

public class GameState
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    public const int RewardPartChoices = 3;
    public const int MutationPointsPerWin = 2;
    public const int StartingEncounterCount = 2;

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    public PlayerInventory Inventory { get; private set; }
    public Species PlayerSpecies { get; private set; }

    /// <summary>The encounter the player is currently facing (null if none queued).</summary>
    public Encounter CurrentEncounter;

    public bool IsGameOver { get; private set; }

    /// <summary>Parts offered as reward after the last completed encounter.</summary>
    public List<Part> PendingRewardChoices { get; private set; } = new List<Part>();

    /// <summary>The procedurally generated world map for this run.</summary>
    public WorldMapData WorldMap { get; private set; }

    // Master catalogue of all parts that can appear as rewards.
    private readonly List<PartSchema> _partCatalogue;
    private readonly Random _rng;
    private bool _isChoosingStartingPart;
    private PartSchema _selectedStartingPartKey;

    // -------------------------------------------------------------------------
    // Construction / Restart
    // -------------------------------------------------------------------------

    public GameState(IEnumerable<PartSchema> partCatalogue = null, int? seed = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        _partCatalogue = partCatalogue?.ToList() ?? BuildDefaultCatalogue();
        Start();
    }

    /// <summary>Resets everything and starts a fresh run.</summary>
    public void Restart()
    {
        IsGameOver = false;
        PendingRewardChoices.Clear();
        Start();
    }

    private void Start()
    {
        // Build the player's starting species (no parts)
        PlayerSpecies = new Species
        {
            Name = "Your Fish",
            BaseHealth = 10,
            BaseSize = 1,
            BaseAttack = 0,
            BaseDefense = 0,
            BaseForage = 1,
        };

        Inventory = new PlayerInventory();

        _isChoosingStartingPart = true;
        _selectedStartingPartKey = null;

        // Build the world map from the generated encounters
        RebuildWorldMap();

        PendingRewardChoices = GlobalConstantsHandler.Constants.StartingParts
            .Select(schema => schema.CreatePart())
            .ToList();
    }

    // -------------------------------------------------------------------------
    // Encounter management
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call after a combat simulation completes. Records the result, issues
    /// rewards on a win, or triggers game-over on a loss.
    /// </summary>
    public void HandleEncounterResult(bool playerWon)
    {
        var encounter = CurrentEncounter;
        if (encounter == null || encounter.IsCompleted)
            return;

        encounter.IsCompleted = true;
        encounter.PlayerWon = playerWon;

        if (!playerWon)
        {
            IsGameOver = true;
            return;
        }

        // Grant mutation points
        Inventory.MutationPoints += MutationPointsPerWin;

        // Generate reward part choices
        PendingRewardChoices = GenerateRewardChoices(RewardPartChoices);
    }

    /// <summary>
    /// Player picks one of the pending reward parts to add to their available pool.
    /// Pass null to skip the reward entirely.
    /// </summary>
    public void SelectReward(Part chosen)
    {
        if (chosen != null)
        {
            if (!PendingRewardChoices.Contains(chosen))
                throw new InvalidOperationException($"'{chosen.Name}' is not one of the pending reward choices.");

            Inventory.AddToPart(chosen);
        }

        if (_isChoosingStartingPart)
        {
            _selectedStartingPartKey = chosen.Schema;
            RebuildWorldMap();
            _isChoosingStartingPart = false;
        }

        PendingRewardChoices.Clear();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // Rarity / reward sampling
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a 0-1 success rate across all completed encounters.
    /// </summary>
    // private float WinRate()
    // {
    //     var completed = Encounters.Where(e => e.IsCompleted).ToList();
    //     if (completed.Count == 0) return 1f;
    //     return completed.Count(e => e.PlayerWon) / (float)completed.Count;
    // }

    /// <summary>
    /// Computes the target rarity as a float index into <see cref="PartRarity"/>.
    /// Progress (0-1) moves the centre up the rarity scale; win rate shifts it
    /// further up on a hot streak or back down on a losing run, producing a
    /// natural bell-curve peak that rewards skilled play at late encounters.
    /// </summary>
    private float TargetRarityValue()
    {
        int maxRarity = (int)PartRarity.Legendary;          // 4
                                                            //int totalEnc = Math.Max(1, Encounters.Count);
                                                            //float progress = Math.Min(1f, EncounterIndex / (float)totalEnc); // 0..1
                                                            // float winRate = WinRate();                                       // 0..1

        // Base centre rises with progress; win-rate nudges it ±1 tier
        //float centre = progress * maxRarity;// + (winRate - 0.5f) * 2f;
        //return Math.Clamp(centre, 0f, maxRarity);
        return 0.0f;
    }

    /// <summary>
    /// Gaussian weight for a part given the target rarity centre and spread σ.
    /// </summary>
    private static float GaussianWeight(float rarityValue, float centre, float sigma)
    {
        float diff = rarityValue - centre;
        return (float)Math.Exp(-(diff * diff) / (2f * sigma * sigma));
    }

    private List<Part> GenerateRewardChoices(int count)
    {
        float centre = TargetRarityValue();
        // Spread widens slightly at mid-progress so the player sees variety
        float sigma = 0.8f + 0.4f * (float)Math.Sin(Math.PI * centre / (int)PartRarity.Legendary);

        // Build weighted pool — duplicate entries raise their probability
        var weightedPool = new List<PartSchema>();
        foreach (var part in _partCatalogue)
        {
            float weight = GaussianWeight((float)part.Rarity, centre, sigma);
            // Convert weight to a ticket count (1-20) so we can do simple random picks
            int tickets = Math.Max(1, (int)Math.Round(weight * 20));
            for (int t = 0; t < tickets; t++)
                weightedPool.Add(part);
        }

        // Fisher-Yates shuffle the ticket pool then pick without replacement by part
        for (int i = weightedPool.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (weightedPool[i], weightedPool[j]) = (weightedPool[j], weightedPool[i]);
        }

        var chosen = new List<Part>();
        foreach (var partSchema in weightedPool)
        {
            var part = partSchema.CreatePart();
            if (!chosen.Contains(part))
                chosen.Add(part);
            if (chosen.Count == count)
                break;
        }

        // Top up with any remaining catalogue parts if the pool was too small
        foreach (var partSchema in _partCatalogue)
        {
            if (chosen.Count >= count)
                break;
            var part = partSchema.CreatePart();
            if (!chosen.Contains(part))
                chosen.Add(part);
        }

        return chosen;
    }

    private WeightedSelector<EncounterSchema> GetPossibleEncounters()
    {
        var list = new WeightedSelector<EncounterSchema>();

        var encounterSchemas = DataManager.Instance?.Encounters;
        if (encounterSchemas != null)
        {
            foreach (var schema in encounterSchemas)
            {
                if (schema == null || schema.StartEncounter || schema.Weight <= 0 || schema.EnemyGroup == null)
                    continue;

                list.AddItem(schema, schema.Weight);
            }
        }

        return list;
    }

    private void RebuildWorldMap()
    {
        WorldMap = new WorldMapData(GetPossibleEncounters(), _rng.Next(), GetStartingEncountersForSelection());
    }

    private List<EncounterSchema> GetStartingEncountersForSelection()
    {
        var result = new List<EncounterSchema>(StartingEncounterCount);
        var used = new HashSet<EncounterSchema>();

        var startingEncounterMap = GlobalConstantsHandler.Constants?.StartingPartEncounters;
        if (_selectedStartingPartKey != null
            && startingEncounterMap != null
            && startingEncounterMap.TryGetValue(_selectedStartingPartKey, out var configuredList)
            && configuredList?.encounters != null)
        {
            foreach (var schema in configuredList.encounters)
            {
                if (schema == null || schema.EnemyGroup == null || !used.Add(schema))
                    continue;

                result.Add(schema);
                if (result.Count >= StartingEncounterCount)
                    return result;
            }
        }

        var startPool = DataManager.Instance?.Encounters?
            .Where(schema => schema != null && schema.StartEncounter && schema.EnemyGroup != null && !used.Contains(schema))
            .ToList();

        if (startPool != null)
        {
            while (result.Count < StartingEncounterCount && startPool.Count > 0)
            {
                int index = _rng.Next(startPool.Count);
                var schema = startPool[index];
                startPool.RemoveAt(index);
                used.Add(schema);
                result.Add(schema);
            }
        }

        return result;
    }

    private SpeciesGroup BuildEnemyGroup(int encounterNumber)
    {
        int enemyCount = 1 + encounterNumber / 3; // 1 enemy for enc 1-2, 2 for 3-5, etc.

        var group = new SpeciesGroup($"Encounter {encounterNumber}");
        for (int i = 0; i < enemyCount; i++)
        {
            int scale = encounterNumber;
            group.Add(new Species
            {
                Name = $"Wild Fish {encounterNumber}-{i + 1}",
                BaseHealth = 8 + scale * 2,
                BaseSize = 1 + scale,
                BaseAttack = scale,
                BaseDefense = scale / 2,
                BaseForage = 1 + scale / 2,
            });
        }

        return group;
    }

    private static List<PartSchema> BuildDefaultCatalogue() => Schema.DataManager.Instance.Parts.ToList();
}
