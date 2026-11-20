using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class GameEndPanel : MonoBehaviour
{
    public event Action<Part> OnRewardSelected;
    public event Action OnExitToWorldRequested;

    UIDocument _document;
    VisualElement _root;
    Label _titleLabel;
    Label _messageLabel;
    Label _lootTitleLabel;
    VisualElement _lootRow;
    Label _emptyLootLabel;
    Button _primaryButton;
    Part _selectedReward;
    VisualElement _selectedLootCard;
    bool _awaitingRewardConfirmation;

    void Awake()
    {
        _document = GetComponent<UIDocument>();
        _root = _document.rootVisualElement;
        _titleLabel = _root.Q<Label>("gep-title");
        _messageLabel = _root.Q<Label>("gep-message");
        _lootTitleLabel = _root.Q<Label>("gep-loot-title");
        _lootRow = _root.Q<VisualElement>("gep-loot-row");
        _emptyLootLabel = _root.Q<Label>("gep-empty-loot");
        _primaryButton = _root.Q<Button>("gep-primary-btn");

        _primaryButton.clicked += HandlePrimaryButtonClicked;
        Hide();
    }

    public void ShowVictory(IReadOnlyList<Part> rewardChoices)
    {
        _selectedReward = null;
        _selectedLootCard = null;
        SetResultState(true, "VICTORY", "A reward has been discovered.");
        _lootTitleLabel.text = "LOOT AVAILABLE";
        BuildLoot(rewardChoices);
        _awaitingRewardConfirmation = _lootRow.childCount > 0;
        _primaryButton.text = _awaitingRewardConfirmation ? "SELECT TRAIT" : "EXIT TO WORLD";
        _primaryButton.SetEnabled(!_awaitingRewardConfirmation);
        ShowRoot();
    }

    public void ShowVictoryWithLoot(Part loot)
    {
        _selectedReward = null;
        _selectedLootCard = null;
        _awaitingRewardConfirmation = false;
        SetResultState(true, "VICTORY", loot == null ? "Reward skipped." : "Reward claimed.");
        _lootTitleLabel.text = loot == null ? "LOOT" : "LOOT ACQUIRED";
        BuildLoot(loot == null ? null : new[] { loot });
        _primaryButton.text = "EXIT TO WORLD";
        _primaryButton.SetEnabled(true);
        ShowRoot();
    }

    public void ShowDefeat()
    {
        _selectedReward = null;
        _selectedLootCard = null;
        _awaitingRewardConfirmation = false;
        SetResultState(false, "DEFEAT", "Your species were defeated.");
        _lootTitleLabel.text = "LOOT";
        BuildLoot(null);
        _primaryButton.text = "EXIT TO WORLD";
        _primaryButton.SetEnabled(true);
        ShowRoot();
    }

    public void Hide()
    {
        if (_root != null)
            _root.style.display = DisplayStyle.None;
    }

    void SetResultState(bool victory, string title, string message)
    {
        _titleLabel.text = title;
        _messageLabel.text = message;
        _titleLabel.EnableInClassList("gep-title--win", victory);
        _titleLabel.EnableInClassList("gep-title--loss", !victory);
    }

    void BuildLoot(IReadOnlyList<Part> loot)
    {
        _lootRow.Clear();
        bool hasLoot = loot != null && loot.Count > 0;
        _emptyLootLabel.style.display = hasLoot ? DisplayStyle.None : DisplayStyle.Flex;

        if (!hasLoot)
            return;

        foreach (var part in loot)
        {
            if (part == null)
                continue;

            var card = new VisualElement();
            card.AddToClassList("gep-loot-card");
            card.AddToClassList(RarityClass(part.Rarity));
            card.AddToClassList("gep-loot-card--selectable");
            card.RegisterCallback<ClickEvent>(_ => HandleLootSelected(part, card));

            var icon = new Image
            {
                sprite = part.ActionIcon,
                scaleMode = ScaleMode.ScaleToFit,
            };
            icon.AddToClassList("gep-loot-card__icon");
            card.Add(icon);

            card.Add(new Label(part.Name?.ToUpperInvariant() ?? "UNKNOWN PART")
            {
                name = "gep-loot-card-name",
            });
            card.Add(new Label(part.Rarity.ToString().ToUpperInvariant())
            {
                name = "gep-loot-card-rarity",
            });
            card.Add(new Label(StatsText(part))
            {
                name = "gep-loot-card-stats",
            });

            if (!string.IsNullOrWhiteSpace(part.Description))
            {
                card.Add(new Label(part.Description)
                {
                    name = "gep-loot-card-description",
                });
            }

            _lootRow.Add(card);
        }
    }

    void HandleLootSelected(Part part, VisualElement card)
    {
        if (!_awaitingRewardConfirmation || part == null)
        {
            return;
        }

        _selectedLootCard?.RemoveFromClassList("gep-loot-card--selected");
        _selectedReward = part;
        _selectedLootCard = card;
        _selectedLootCard.AddToClassList("gep-loot-card--selected");
        _primaryButton.text = "CLAIM REWARD";
        _primaryButton.SetEnabled(true);
    }

    void ShowRoot()
    {
        _root.style.display = DisplayStyle.Flex;
    }

    void HandlePrimaryButtonClicked()
    {
        if (_awaitingRewardConfirmation)
        {
            if (_selectedReward == null)
            {
                return;
            }

            _awaitingRewardConfirmation = false;
            Hide();
            OnRewardSelected?.Invoke(_selectedReward);
            return;
        }

        Hide();
        OnExitToWorldRequested?.Invoke();
    }

    static string StatsText(Part part)
    {
        var stats = new List<string>();
        if (part.Attack > 0) stats.Add($"ATK +{part.Attack}");
        if (part.Defense > 0) stats.Add($"DEF +{part.Defense}");
        if (part.Health > 0) stats.Add($"HP +{part.Health}");
        if (part.Forage > 0) stats.Add($"FRG +{part.Forage}");
        if (part.Size > 0) stats.Add($"SZ +{part.Size}");
        return stats.Count > 0 ? string.Join("  ", stats) : "UTILITY";
    }

    static string RarityClass(PartRarity rarity) => rarity switch
    {
        PartRarity.Common => "gep-rarity--common",
        PartRarity.Uncommon => "gep-rarity--uncommon",
        PartRarity.Rare => "gep-rarity--rare",
        PartRarity.Epic => "gep-rarity--epic",
        PartRarity.Legendary => "gep-rarity--legendary",
        _ => "gep-rarity--common",
    };
}
