using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Overlay panel that presents a set of part cards and lets the player pick one.
/// The number of cards shown is data-driven via <see cref="CardPickerData"/> —
/// typically 3 (reward) or 5 (draft).
///
/// Usage:
///   var picker = CardPickerPanel.Show(uiDocument, data);
///   picker.OnPicked += part => { … };   // null when the player skips
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class CardPickerPanel : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────
    [Tooltip("UXML asset to use. Leave null to load from Resources/UI/CardPickerPanel.")]
    public VisualTreeAsset OverrideUxml;

    // ── Events ────────────────────────────────────────────────

    /// <summary>
    /// Fired when the player confirms a choice.
    /// The argument is the chosen <see cref="Part"/>, or <c>null</c> when skipped.
    /// </summary>
    public event Action<Part> OnPicked;

    // ── Private state ─────────────────────────────────────────
    UIDocument _doc;
    VisualElement _root;

    Label _titleLabel;
    Label _subtitleLabel;
    VisualElement _cardRow;
    Button _skipBtn;

    PickerCardElement _selectedCard;
    CardPickerData _data;

    // ── Unity lifecycle ───────────────────────────────────────

    void Awake()
    {
        _doc = GetComponent<UIDocument>();

        VisualTreeAsset asset = OverrideUxml
            ? OverrideUxml
            : Resources.Load<VisualTreeAsset>("UI/CardPickerPanel");

        if (asset == null)
        {
            Debug.LogError("[CardPickerPanel] Could not load CardPickerPanel.uxml from Resources/UI/");
            return;
        }

        _root = asset.CloneTree();
        _doc.rootVisualElement.Add(_root);

        _titleLabel = _root.Q<Label>("cpp-title");
        _subtitleLabel = _root.Q<Label>("cpp-subtitle");
        _cardRow = _root.Q<VisualElement>("cpp-card-row");
        _skipBtn = _root.Q<Button>("cpp-skip-btn");

        _skipBtn.clicked += OnSkipClicked;

        Hide();
    }

    // ── Public API ────────────────────────────────────────────

    /// <summary>
    /// Populate and show the picker for <paramref name="data"/>.
    /// </summary>
    public void Show(CardPickerData data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _selectedCard = null;

        _titleLabel.text = data.Title ?? "CHOOSE A PART";
        _subtitleLabel.text = data.Subtitle ?? $"Pick 1 of {data.Choices?.Count ?? 0}";

        Utility.UI.EnableClass(!data.AllowSkip, _skipBtn, "cpp-skip-btn--hidden");

        BuildCards(data.Choices);

        _root.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Hide the panel without triggering <see cref="OnPicked"/>.
    /// </summary>
    public void Hide()
    {
        _root.style.display = DisplayStyle.None;
    }

    // ── Card building ─────────────────────────────────────────

    void BuildCards(IReadOnlyList<Part> choices)
    {
        _cardRow.Clear();

        if (choices == null) return;

        foreach (var part in choices)
        {
            var card = new PickerCardElement(part);
            card.OnSelected += HandleCardSelected;
            _cardRow.Add(card);
        }
    }

    // ── Interaction ───────────────────────────────────────────

    void HandleCardSelected(PickerCardElement card)
    {
        if (_selectedCard == card)
        {
            // Double-click / second tap confirms the selection
            Confirm(card.Part);
            return;
        }

        // Deselect previous
        _selectedCard?.SetSelected(false);
        _selectedCard = card;
        card.SetSelected(true);
    }

    void OnSkipClicked()
    {
        Hide();
        OnPicked?.Invoke(null);
    }

    void Confirm(Part part)
    {
        Hide();
        OnPicked?.Invoke(part);
    }

    // ── Nested visual element ─────────────────────────────────

    /// <summary>
    /// A larger card element used inside the picker overlay.
    /// </summary>
    sealed class PickerCardElement : VisualElement
    {
        const string CardClass = "cpp-card";
        const string IconClass = "cpp-card__icon";
        const string NameClass = "cpp-card__name";
        const string RarityClass = "cpp-card__rarity";
        const string TypeTagClass = "cpp-card__type-tag";
        const string StatRowClass = "cpp-card__stat-row";
        const string StatPipClass = "cpp-card__stat-pip";
        const string SelectedClass = "cpp-card--selected";

        public Part Part { get; }
        public event Action<PickerCardElement> OnSelected;

        public PickerCardElement(Part part)
        {
            Part = part;
            AddToClassList(CardClass);
            AddToClassList(RarityToCssClass(part.Rarity));

            // Icon placeholder
            var icon = new VisualElement();
            icon.AddToClassList(IconClass);
            Add(icon);

            // Name
            var name = new Label(part.Name.ToUpper());
            name.AddToClassList(NameClass);
            Add(name);

            // Rarity text
            var rarityLabel = new Label(part.Rarity.ToString().ToUpper());
            rarityLabel.AddToClassList(RarityClass);
            Add(rarityLabel);

            // Type tag
            var tag = new Label(GetTypeLabel(part));
            tag.AddToClassList(TypeTagClass);
            tag.AddToClassList(GetTypeTagClass(part));
            Add(tag);

            // Stat pips
            var statRow = new VisualElement();
            statRow.AddToClassList(StatRowClass);
            AddStatPip(statRow, "ATK", part.Attack);
            AddStatPip(statRow, "DEF", part.Defense);
            AddStatPip(statRow, "HP", part.Health);
            AddStatPip(statRow, "FRG", part.Forage);
            AddStatPip(statRow, "SZ", part.Size);
            Add(statRow);

            RegisterCallback<ClickEvent>(_ => OnSelected?.Invoke(this));
        }

        public void SetSelected(bool selected) =>
            Utility.UI.EnableClass(selected, this, SelectedClass);

        // ── Helpers ───────────────────────────────────────────

        static void AddStatPip(VisualElement row, string label, int value)
        {
            if (value <= 0) return;
            var pip = new Label($"{label} +{value}");
            pip.AddToClassList(StatPipClass);
            row.Add(pip);
        }

        static string RarityToCssClass(PartRarity rarity) => rarity switch
        {
            PartRarity.Common => "cpp-rarity--common",
            PartRarity.Uncommon => "cpp-rarity--uncommon",
            PartRarity.Rare => "cpp-rarity--rare",
            PartRarity.Epic => "cpp-rarity--epic",
            PartRarity.Legendary => "cpp-rarity--legendary",
            _ => "cpp-rarity--common",
        };

        static string GetTypeLabel(Part part)
        {
            if (part.Attack > 0) return "Attack";
            if (part.Defense > 0) return "Defense";
            if (part.Forage > 0) return "Feeding";
            if (part.Health > 0) return "Defense";
            return "Mutation";
        }

        static string GetTypeTagClass(Part part)
        {
            if (part.Attack > 0) return "sep-tag--attack";
            if (part.Defense > 0) return "sep-tag--defense";
            if (part.Forage > 0) return "sep-tag--feeding";
            if (part.Health > 0) return "sep-tag--defense";
            return "sep-tag--mutation";
        }
    }
}
