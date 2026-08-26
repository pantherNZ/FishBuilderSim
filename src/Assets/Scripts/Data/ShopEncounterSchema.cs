using UnityEngine;

/// <summary>
/// Configured special encounter that opens the mutation shop.
/// </summary>
[CreateAssetMenu(fileName = "NewShopEncounter", menuName = "FishBuilderSim/Shop Encounter Schema")]
public class ShopEncounterSchema : EncounterSchema
{
    public override Encounter CreateEncounter()
    {
        return new ShopEncounter(this);
    }
}