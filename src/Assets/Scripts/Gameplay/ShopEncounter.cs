using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ShopPartOffer
{
    public PartSchema Schema { get; }
    public Part Part { get; }
    public int Cost { get; }
    public bool IsPurchased { get; private set; }

    public ShopPartOffer(PartSchema schema, int cost)
    {
        Schema = schema;
        Part = schema?.CreatePart();
        Cost = cost;
    }

    public void MarkPurchased() => IsPurchased = true;
}

public sealed class ShopSpeciesOffer
{
    public SpeciesSchema Schema { get; }
    public Species Species { get; }
    public int Cost { get; }
    public bool IsPurchased { get; private set; }

    public ShopSpeciesOffer(SpeciesSchema schema, int cost)
    {
        Schema = schema;
        Species = schema?.CreateSpecies();
        Cost = cost;
    }

    public void MarkPurchased() => IsPurchased = true;
}

/// <summary>
/// Runtime state and transaction logic for one shop visit.
/// </summary>
public sealed class ShopEncounter : Encounter
{
    public const int PartOfferCount = 5;
    public const int BaseSpeciesCost = 5;
    public const int SpeciesCostStep = 5;

    public ShopEncounterSchema Schema { get; }
    public IReadOnlyList<ShopPartOffer> PartOffers => _partOffers;
    public ShopSpeciesOffer SpeciesOffer { get; private set; }
    public bool SpeciesPurchased => SpeciesOffer?.IsPurchased == true;

    readonly List<ShopPartOffer> _partOffers = new();

    public ShopEncounter(ShopEncounterSchema schema) : base(null)
    {
        Schema = schema;
    }

    public void PopulateOffers(
        IEnumerable<PartSchema> partCatalogue,
        IEnumerable<int> ownedPartHashes,
        IEnumerable<SpeciesSchema> speciesCatalogue,
        IEnumerable<int> ownedSpeciesHashes,
        int ownedFishCount,
        int seed)
    {
        _partOffers.Clear();
        SpeciesOffer = null;

        var random = new Random(seed);
        var ownedParts = new HashSet<int>(ownedPartHashes ?? Enumerable.Empty<int>());
        var parts = (partCatalogue ?? Enumerable.Empty<PartSchema>())
            .Where(part => part != null && !ownedParts.Contains(part.GetHashCode()))
            .OrderBy(_ => random.Next())
            .Take(PartOfferCount)
            .ToList();

        foreach (var part in parts)
            _partOffers.Add(new ShopPartOffer(part, GetPartCost(part.Rarity)));

        var owned = new HashSet<int>(ownedSpeciesHashes ?? Enumerable.Empty<int>());
        var species = (speciesCatalogue ?? Enumerable.Empty<SpeciesSchema>())
            .Where(candidate => candidate != null && !owned.Contains(candidate.GetHashCode()))
            .OrderBy(_ => random.Next())
            .FirstOrDefault();

        if (species != null)
            SpeciesOffer = new ShopSpeciesOffer(species, GetSpeciesCost(ownedFishCount));
    }

    public bool TryPurchasePart(int offerIndex, PlayerInventory inventory)
    {
        if (inventory == null || offerIndex < 0 || offerIndex >= _partOffers.Count)
            return false;

        var offer = _partOffers[offerIndex];
        if (offer.IsPurchased || !inventory.TryPurchasePart(offer.Part, offer.Cost))
            return false;

        offer.MarkPurchased();
        return true;
    }

    public bool TryPurchaseSpecies(PlayerInventory inventory, out Species species)
    {
        species = null;
        if (inventory == null || SpeciesOffer == null || SpeciesOffer.IsPurchased)
            return false;

        if (inventory.MutationPoints < SpeciesOffer.Cost)
            return false;

        inventory.MutationPoints -= SpeciesOffer.Cost;
        SpeciesOffer.MarkPurchased();
        species = SpeciesOffer.Species;
        return true;
    }

    public static int GetPartCost(PartRarity rarity) => (int)rarity + 1;

    public static int GetSpeciesCost(int ownedFishCount)
        => BaseSpeciesCost + SpeciesCostStep * Math.Max(0, ownedFishCount);

    public void RestorePurchasedParts(IEnumerable<bool> purchasedStates)
    {
        var states = purchasedStates?.ToList() ?? new List<bool>();
        for (int i = 0; i < _partOffers.Count && i < states.Count; i++)
            if (states[i])
                _partOffers[i].MarkPurchased();
    }

    public void RestoreSpeciesPurchased(bool purchased)
    {
        if (purchased)
            SpeciesOffer?.MarkPurchased();
    }

    public void RestoreOffers(
        IEnumerable<PartSchema> partSchemas,
        IEnumerable<bool> purchasedStates,
        SpeciesSchema speciesSchema,
        int speciesCost,
        bool speciesPurchased)
    {
        _partOffers.Clear();
        var states = purchasedStates?.ToList() ?? new List<bool>();
        var schemas = partSchemas?.Where(schema => schema != null).ToList()
            ?? new List<PartSchema>();

        for (int i = 0; i < schemas.Count; i++)
        {
            var offer = new ShopPartOffer(schemas[i], GetPartCost(schemas[i].Rarity));
            if (i < states.Count && states[i])
                offer.MarkPurchased();
            _partOffers.Add(offer);
        }

        SpeciesOffer = speciesSchema == null
            ? null
            : new ShopSpeciesOffer(speciesSchema, speciesCost);
        RestoreSpeciesPurchased(speciesPurchased);
    }
}