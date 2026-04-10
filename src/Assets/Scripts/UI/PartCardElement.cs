using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Represents a single part card inside the library grid.
/// Handles its own visual state and click selection.
/// </summary>
public class PartCardElement : VisualElement
{
    // ── USS class names ──────────────────────────────────────
    const string CardClass = "sep-part-card";
    const string IconClass = "sep-part-card__icon";
    const string NameClass = "sep-part-card__name";
    const string TagClass = "sep-part-card__type-tag";
    const string SelectedClass = "sep-part-card--selected";
    const string EquippedClass = "sep-part-card--equipped";

    // ── Sub-elements ─────────────────────────────────────────
    readonly VisualElement _icon;
    readonly Label _nameLabel;
    readonly Label _typeTag;

    public Part Part { get; private set; }

    public event Action<PartCardElement> OnSelected;

    public PartCardElement(Part part)
    {
        Part = part;

        AddToClassList(CardClass);
        ApplyRarityClass(part.Rarity);

        _icon = new VisualElement();
        _icon.AddToClassList(IconClass);
        Add(_icon);

        _nameLabel = new Label(part.Name.ToUpper());
        _nameLabel.AddToClassList(NameClass);
        Add(_nameLabel);

        _typeTag = new Label(GetTypeLabel(part));
        _typeTag.AddToClassList(TagClass);
        _typeTag.AddToClassList(GetTypeTagClass(part));
        Add(_typeTag);

        RegisterCallback<ClickEvent>(_ => OnSelected?.Invoke(this));
    }

    public void SetSelected(bool selected) =>
        Utility.UI.EnableClass(selected, this, SelectedClass);

    public void SetEquipped(bool equipped) =>
        Utility.UI.EnableClass(equipped, this, EquippedClass);

    // ── Helpers ──────────────────────────────────────────────

    void ApplyRarityClass(PartRarity rarity)
    {
        string cls = rarity switch
        {
            PartRarity.Common => "sep-rarity--common",
            PartRarity.Uncommon => "sep-rarity--uncommon",
            PartRarity.Rare => "sep-rarity--rare",
            PartRarity.Epic => "sep-rarity--epic",
            PartRarity.Legendary => "sep-rarity--legendary",
            _ => "sep-rarity--common",
        };
        AddToClassList(cls);
    }

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
