using System;
using System.Collections.Generic;
using System.Linq;
using Runtime.Game;
using Schema;

public class GameState : EventReceiver
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
    public IReadOnlyList<Species> OwnedSpecies => _ownedSpecies;

    /// <summary>The encounter the player is currently facing (null if none queued).</summary>
    public Encounter CurrentEncounter;

    public bool IsGameOver { get; private set; }

    /// <summary>Parts offered as reward after the last completed encounter.</summary>
    public List<Part> PendingRewardChoices { get; private set; } = new List<Part>();

    /// <summary>The procedurally generated world map for this run.</summary>
    public WorldMapData WorldMap { get; private set; }

    // Master catalogue of all parts that can appear as rewards.
    private readonly List<PartSchema> _partCatalogue;
    private readonly List<Species> _ownedSpecies = new();
    private readonly Random _rng;
    private bool _isChoosingStartingPart;
    private PartSchema _selectedStartingPartKey;
    private int _worldMapSeed;

    public bool IsChoosingStartingPart => _isChoosingStartingPart;
    public PartSchema SelectedStartingPartKey => _selectedStartingPartKey;
    public int WorldMapSeed => _worldMapSeed;

    // -------------------------------------------------------------------------
    // Construction / Restart
    // -------------------------------------------------------------------------

    public GameState(IEnumerable<PartSchema> partCatalogue = null, int? seed = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        Runtime.Events.RequestGlobalSave.Subscribe(this, OnGlobalSaveRequested);
        _partCatalogue = partCatalogue?.ToList() ?? BuildDefaultCatalogue();
        Start();
        Load();
    }

    void OnGlobalSaveRequested(Runtime.Events.RequestGlobalSave _)
    {
        var save = GetSave();
        if (save == null) return;

        save.isGameOver = IsGameOver;
        save.isChoosingStartingPart = _isChoosingStartingPart;
        save.selectedStartingPartSchemaHash = _selectedStartingPartKey?.GetHashCode() ?? 0;
        save.worldMapSeed = _worldMapSeed;
        save.playerX = WorldMap?.PlayerNode?.Position.X ?? 0;
        save.playerY = WorldMap?.PlayerNode?.Position.Y ?? 0;

        save.hasCurrentEncounter = CurrentEncounter != null;
        save.currentEncounterX = 0;
        save.currentEncounterY = 0;
        if (save.hasCurrentEncounter && WorldMap != null)
        {
            foreach (var node in WorldMap.Nodes.Values)
            {
                if (node.Encounter != CurrentEncounter)
                    continue;

                save.currentEncounterX = node.Position.X;
                save.currentEncounterY = node.Position.Y;
                break;
            }
        }

        save.playerHealth = PlayerSpecies?.CurrentHealth ?? 0;
        save.playerSize = PlayerSpecies?.CurrentSize ?? 0;
        save.ownedSpeciesSchemaHashes.Clear();
        foreach (var species in _ownedSpecies)
        {
            if (species?.Schema != null)
                save.ownedSpeciesSchemaHashes.Add(species.Schema.GetHashCode());
        }

        var shop = CurrentEncounter as ShopEncounter;
        save.shopPartOfferSchemaHashes.Clear();
        save.shopPartOfferPurchased.Clear();
        if (shop != null)
        {
            foreach (var offer in shop.PartOffers)
            {
                save.shopPartOfferSchemaHashes.Add(offer.Schema?.GetHashCode() ?? 0);
                save.shopPartOfferPurchased.Add(offer.IsPurchased);
            }

            save.shopSpeciesOfferSchemaHash = shop.SpeciesOffer?.Schema?.GetHashCode() ?? 0;
            save.shopSpeciesPurchased = shop.SpeciesPurchased;
        }
        else
        {
            save.shopSpeciesOfferSchemaHash = 0;
            save.shopSpeciesPurchased = false;
        }

        save.pendingRewardPartSchemaHashes.Clear();
        if (PendingRewardChoices != null)
        {
            foreach (var part in PendingRewardChoices)
                save.pendingRewardPartSchemaHashes.Add(part?.Schema?.GetHashCode() ?? 0);
        }

        save.nodes.Clear();
        if (WorldMap != null)
        {
            foreach (var node in WorldMap.Nodes.Values)
            {
                save.nodes.Add(new Save.MapNodeSave
                {
                    x = node.Position.X,
                    y = node.Position.Y,
                    isVisited = node.IsVisited,
                    isAccessible = node.IsAccessible,
                    encounterCompleted = node.Encounter?.IsCompleted ?? false,
                    playerWon = node.Encounter?.PlayerWon ?? false,
                });
            }
        }

        save.pendingSave = true;
    }

    void Load()
    {
        var save = GetSave();
        if (save == null || !save.ExistsOnDisk()) return;

        IsGameOver = save.isGameOver;
        _isChoosingStartingPart = save.isChoosingStartingPart;
        _selectedStartingPartKey = FindPartSchema(save.selectedStartingPartSchemaHash);
        _worldMapSeed = save.worldMapSeed;
        RebuildWorldMap(useExistingSeed: true);

        foreach (var nodeSave in save.nodes ?? new List<Save.MapNodeSave>())
        {
            if (!WorldMap.Nodes.TryGetValue(new MapPoint(nodeSave.x, nodeSave.y), out var node))
                continue;

            node.IsVisited = nodeSave.isVisited;
            node.IsAccessible = nodeSave.isAccessible;
            if (node.Encounter != null)
            {
                node.Encounter.IsCompleted = nodeSave.encounterCompleted;
                node.Encounter.PlayerWon = nodeSave.playerWon;
            }
        }

        if (WorldMap.Nodes.TryGetValue(new MapPoint(save.playerX, save.playerY), out var playerNode))
            WorldMap.MovePlayerTo(playerNode);

        CurrentEncounter = null;
        if (save.hasCurrentEncounter && WorldMap.Nodes.TryGetValue(
                new MapPoint(save.currentEncounterX, save.currentEncounterY), out var encounterNode))
            CurrentEncounter = encounterNode.Encounter;

        RestoreOwnedSpecies(save.ownedSpeciesSchemaHashes);
        if (CurrentEncounter is ShopEncounter shop)
            RestoreShopOffers(shop, save);

        PendingRewardChoices = (save.pendingRewardPartSchemaHashes ?? new List<int>())
            .Select(FindPartSchema)
            .Where(schema => schema != null)
            .Select(schema => schema.CreatePart())
            .ToList();

        Inventory.ApplyToSpecies(PlayerSpecies);
        PlayerSpecies.CurrentHealth = save.playerHealth;
        PlayerSpecies.CurrentSize = save.playerSize;
    }

    static PartSchema FindPartSchema(int schemaHash)
    {
        if (schemaHash == 0 || Schema.DataManager.Instance == null)
            return null;

        return Schema.DataManager.Instance.FindAssetByHash(schemaHash) as PartSchema;
    }

    static SpeciesSchema FindSpeciesSchema(int schemaHash)
    {
        if (schemaHash == 0 || Schema.DataManager.Instance == null)
            return null;

        return Schema.DataManager.Instance.FindAssetByHash(schemaHash) as SpeciesSchema;
    }

    void RestoreOwnedSpecies(IEnumerable<int> schemaHashes)
    {
        var hashes = schemaHashes?.Where(hash => hash != 0).ToList();
        if (hashes == null || hashes.Count == 0)
            return;

        foreach (var hash in hashes.Distinct())
        {
            var species = FindSpeciesSchema(hash)?.CreateSpecies();
            if (species != null && !_ownedSpecies.Any(owned => owned?.Schema == species.Schema))
                _ownedSpecies.Add(species);
        }
    }

    void RestoreShopOffers(ShopEncounter shop, Save.GameStateSave save)
    {
        var schemas = (save.shopPartOfferSchemaHashes ?? new List<int>())
            .Select(FindPartSchema)
            .Where(schema => schema != null);
        var speciesSchema = FindSpeciesSchema(save.shopSpeciesOfferSchemaHash);
        shop.RestoreOffers(
            schemas,
            save.shopPartOfferPurchased,
            speciesSchema,
            ShopEncounter.GetSpeciesCost(_ownedSpecies.Count),
            save.shopSpeciesPurchased);
    }

    static Save.GameStateSave GetSave()
    {
        var runtimeConstants = Runtime.Game.GlobalConstantsHandler.RuntimeConstants;
        if (runtimeConstants == null || Save.SaveManager.Instance == null)
            return null;

        return runtimeConstants.gameStateSave;
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

        _ownedSpecies.Clear();
        _ownedSpecies.Add(PlayerSpecies);

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

    public bool EnterShop(WorldMapNode destination)
    {
        if (destination?.Encounter is not ShopEncounter shop || WorldMap == null)
            return false;

        CurrentEncounter = shop;
        WorldMap.MovePlayerTo(destination);
        if (shop.PartOffers.Count == 0 && shop.SpeciesOffer == null)
        {
            int seed = GlobalConstants.GenerateSeedStatic(
                _worldMapSeed,
                destination.Position.X,
                destination.Position.Y,
                7919);
            shop.PopulateOffers(
                _partCatalogue,
                Inventory.AvailableParts.Concat(Inventory.ActiveParts)
                    .Where(part => part?.Schema != null)
                    .Select(part => part.Schema.GetHashCode()),
                DataManager.Instance?.Species,
                _ownedSpecies.Where(species => species?.Schema != null)
                    .Select(species => species.Schema.GetHashCode()),
                _ownedSpecies.Count,
                seed);
        }

        Runtime.Events.RequestGlobalSave.Trigger(new Runtime.Events.RequestGlobalSave());
        return true;
    }

    public bool TryPurchaseShopPart(int offerIndex)
    {
        var shop = CurrentEncounter as ShopEncounter;
        if (shop == null || !shop.TryPurchasePart(offerIndex, Inventory))
            return false;

        Runtime.Events.RequestGlobalSave.Trigger(new Runtime.Events.RequestGlobalSave());
        return true;
    }

    public bool TryPurchaseShopSpecies()
    {
        var shop = CurrentEncounter as ShopEncounter;
        if (shop?.SpeciesOffer?.Schema == null
            || _ownedSpecies.Any(owned => owned?.Schema == shop.SpeciesOffer.Schema))
            return false;

        if (shop == null || !shop.TryPurchaseSpecies(Inventory, out var species))
            return false;

        _ownedSpecies.Add(species);
        Runtime.Events.RequestGlobalSave.Trigger(new Runtime.Events.RequestGlobalSave());
        return true;
    }

    public void LeaveShop()
    {
        if (CurrentEncounter is not ShopEncounter shop)
            return;

        shop.IsCompleted = true;
        shop.PlayerWon = true;
        CurrentEncounter = null;
        Runtime.Events.RequestGlobalSave.Trigger(new Runtime.Events.RequestGlobalSave());
    }

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
        PlayerSpecies.CurrentSize = 0;

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
            _selectedStartingPartKey = chosen?.Schema;
            RebuildWorldMap();
            _isChoosingStartingPart = false;
        }

        PendingRewardChoices.Clear();

        Runtime.Events.RequestGlobalSave.Trigger(new Runtime.Events.RequestGlobalSave());
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
                if (schema == null || schema.StartEncounter || schema.Weight <= 0 || !schema.HasEnemyDefinition)
                    continue;

                list.AddItem(schema, schema.Weight);
            }
        }

        return list;
    }

    private ShopEncounterSchema GetShopSchema()
    {
        return DataManager.Instance?.Encounters?
            .OfType<ShopEncounterSchema>()
            .Where(schema => schema != null && schema.Weight > 0)
            .OrderBy(schema => schema.GetHashCode())
            .FirstOrDefault();
    }

    private void RebuildWorldMap(bool useExistingSeed = false)
    {
        if (!useExistingSeed)
            _worldMapSeed = _rng.Next();

        WorldMap = new WorldMapData(
            GetPossibleEncounters(),
            _worldMapSeed,
            GetStartingEncountersForSelection(),
            GetShopSchema(),
            GlobalConstantsHandler.Constants?.EncounterDifficultyScaling ?? 0f);
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
                if (schema == null)
                    continue;

                if (!schema.HasEnemyDefinition)
                    continue;

                if (!used.Add(schema))
                    continue;

                result.Add(schema);
                if (result.Count >= StartingEncounterCount)
                    return result;
            }
        }

        var allEncounters = DataManager.Instance?.Encounters;
        var startPool = allEncounters?
            .Where(schema => schema != null && schema.StartEncounter && schema.HasEnemyDefinition && !used.Contains(schema))
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
