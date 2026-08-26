using System;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ShopPanel : MonoBehaviour
{
    public event Action<int> OnPartPurchaseRequested;
    public event Action OnSpeciesPurchaseRequested;
    public event Action OnLeaveRequested;

    UIDocument _document;
    VisualElement _root;
    VisualElement _partOffers;
    VisualElement _speciesOffer;
    Label _pointsLabel;
    Label _speciesEmptyLabel;
    Button _leaveButton;

    GameState _gameState;
    ShopEncounter _shop;

    void Awake()
    {
        _document = GetComponent<UIDocument>();
        _root = _document.rootVisualElement;
        _partOffers = _root.Q<VisualElement>("shop-part-offers");
        _speciesOffer = _root.Q<VisualElement>("shop-species-offer");
        _pointsLabel = _root.Q<Label>("shop-points");
        _speciesEmptyLabel = _root.Q<Label>("shop-species-empty");
        _leaveButton = _root.Q<Button>("shop-leave");

        _leaveButton.clicked += () => OnLeaveRequested?.Invoke();
        Hide();
    }

    public void Show(GameState gameState, ShopEncounter shop)
    {
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        _shop = shop ?? throw new ArgumentNullException(nameof(shop));
        Refresh();
        _root.style.display = DisplayStyle.Flex;
    }

    public void Hide()
    {
        if (_root != null)
            _root.style.display = DisplayStyle.None;
    }

    public void Refresh()
    {
        if (_gameState == null || _shop == null)
            return;

        _pointsLabel.text = $"MUTATION POINTS  {_gameState.Inventory.MutationPoints}";
        BuildPartOffers();
        BuildSpeciesOffer();
    }

    void BuildPartOffers()
    {
        _partOffers.Clear();
        for (int index = 0; index < _shop.PartOffers.Count; index++)
        {
            var offer = _shop.PartOffers[index];
            var card = new VisualElement();
            card.AddToClassList("shop-part-card");
            card.AddToClassList(RarityClass(offer.Part.Rarity));

            card.Add(new Label(offer.Part.Name.ToUpper()) { name = "shop-card-name" });
            card.Add(new Label(offer.Part.Rarity.ToString().ToUpper()) { name = "shop-card-rarity" });
            card.Add(new Label(StatsText(offer.Part)) { name = "shop-card-stats" });

            var button = new Button(() => OnPartPurchaseRequested?.Invoke(index))
            {
                text = offer.IsPurchased ? "PURCHASED" : $"BUY  {offer.Cost} MP",
            };
            button.name = "shop-buy-part";
            button.SetEnabled(!offer.IsPurchased && _gameState.Inventory.MutationPoints >= offer.Cost);
            card.Add(button);
            _partOffers.Add(card);
        }
    }

    void BuildSpeciesOffer()
    {
        _speciesOffer.Clear();
        var offer = _shop.SpeciesOffer;
        if (offer == null)
        {
            _speciesEmptyLabel.style.display = DisplayStyle.Flex;
            return;
        }

        _speciesEmptyLabel.style.display = DisplayStyle.None;
        _speciesOffer.Add(new Label(offer.Species.Name?.ToUpper() ?? "SPECIES") { name = "shop-species-name" });
        _speciesOffer.Add(new Label("NEW SPECIES") { name = "shop-species-kicker" });
        _speciesOffer.Add(new Label(StatsText(offer.Species)) { name = "shop-species-stats" });

        var button = new Button(() => OnSpeciesPurchaseRequested?.Invoke())
        {
            text = offer.IsPurchased ? "PURCHASED" : $"BUY SPECIES  {offer.Cost} MP",
        };
        button.name = "shop-buy-species";
        button.SetEnabled(!offer.IsPurchased && _gameState.Inventory.MutationPoints >= offer.Cost);
        _speciesOffer.Add(button);
    }

    static string StatsText(Part part)
    {
        return $"ATK +{part.Attack}   DEF +{part.Defense}   HP +{part.Health}\nFOR +{part.Forage}   SIZE +{part.Size}";
    }

    static string StatsText(Species species)
    {
        return $"HP {species.MaxHealth}   ATK {species.Attack}   DEF {species.Defense}\nFOR {species.Forage}   SIZE {species.Size}";
    }

    static string RarityClass(PartRarity rarity) => rarity switch
    {
        PartRarity.Common => "shop-rarity-common",
        PartRarity.Uncommon => "shop-rarity-uncommon",
        PartRarity.Rare => "shop-rarity-rare",
        PartRarity.Epic => "shop-rarity-epic",
        PartRarity.Legendary => "shop-rarity-legendary",
        _ => "shop-rarity-common",
    };
}
