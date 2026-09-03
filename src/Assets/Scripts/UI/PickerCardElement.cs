using System;
using UnityEngine.UIElements;

/// <summary>
/// A selectable part card used by <see cref="CardPickerPanel"/>.
/// </summary>
public sealed class PickerCardElement : VisualElement
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

        var icon = new VisualElement();
        icon.AddToClassList(IconClass);
        Add(icon);

        var name = new Label(part.Name.ToUpper());
        name.AddToClassList(NameClass);
        Add(name);

        var rarityLabel = new Label(part.Rarity.ToString().ToUpper());
        rarityLabel.AddToClassList(RarityClass);
        Add(rarityLabel);

        var tag = new Label(GetTypeLabel(part));
        tag.AddToClassList(TypeTagClass);
        tag.AddToClassList(GetTypeTagClass(part));
        Add(tag);

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
