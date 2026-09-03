using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A floating health bar that displays a species' name, current HP, status
/// effects, and either player action controls or the enemy's intended action.
/// </summary>
public class SpeciesHealthBarElement : VisualElement
{
    readonly Species _species;
    readonly VisualElement _fill;
    readonly Label _hpLabel;
    readonly VisualElement _statusEffectsRow;
    readonly VisualElement _actionButtonsRow;
    readonly VisualElement _actionIcon;
    readonly List<Button> _actionButtons = new();
    readonly Dictionary<Button, Part> _actionButtonParts = new();
    bool _actionControlsVisible;
    bool _intentVisible;

    public event Action<Part> OnActionSelected;

    public SpeciesHealthBarElement(Species species)
    {
        _species = species;

        AddToClassList("bp-health-bar");
        style.position = Position.Absolute;

        _statusEffectsRow = new VisualElement();
        _statusEffectsRow.AddToClassList("bp-health-bar__status-effects");
        Add(_statusEffectsRow);

        _actionButtonsRow = new VisualElement();
        _actionButtonsRow.AddToClassList("bp-health-bar__actions");
        Add(_actionButtonsRow);

        _actionIcon = new VisualElement();
        _actionIcon.AddToClassList("bp-health-bar__action-btn");
        _actionIcon.AddToClassList("bp-health-bar__action-btn--enemy");
        _actionIcon.style.display = DisplayStyle.None;
        Add(_actionIcon);

        var nameLabel = new Label(species.Name?.ToUpper() ?? "?");
        nameLabel.AddToClassList("bp-health-bar__name");
        Add(nameLabel);

        var track = new VisualElement();
        track.AddToClassList("bp-health-bar__track");
        _fill = new VisualElement();
        _fill.AddToClassList("bp-health-bar__fill");
        track.Add(_fill);
        Add(track);

        _hpLabel = new Label();
        _hpLabel.AddToClassList("bp-health-bar__hp-text");
        Add(_hpLabel);

        Refresh();
    }

    public void SetActionControlsVisible(bool visible)
    {
        _actionControlsVisible = visible;
        if (!visible)
        {
            _actionButtonsRow.style.display = DisplayStyle.None;
            if (!_intentVisible)
                _actionIcon.style.display = DisplayStyle.None;
            return;
        }

        _actionIcon.RemoveFromClassList("bp-health-bar__action-btn--enemy");

        if (_actionIcon.style.display == DisplayStyle.None)
            _actionButtonsRow.style.display = _actionButtons.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void SetIntentVisible(bool visible)
    {
        _intentVisible = visible;

        if (!visible)
        {
            _actionIcon.RemoveFromClassList("bp-health-bar__action-btn--enemy");
            if (!_actionControlsVisible)
                _actionIcon.style.display = DisplayStyle.None;
        }
    }

    public void ConfigureActionButtons(IReadOnlyList<Part> actions, Species attackTarget)
    {
        _actionButtonsRow.Clear();
        _actionButtons.Clear();
        _actionButtonParts.Clear();

        if (!_actionControlsVisible || actions == null || actions.Count == 0)
        {
            _actionButtonsRow.style.display = DisplayStyle.None;
            return;
        }

        foreach (var action in actions)
        {
            var localPart = action;
            var actionType = localPart == null ? SpeciesActionType.Attack : localPart.ActionType;
            var button = new Button(() => OnActionSelected?.Invoke(localPart));
            button.text = string.Empty;
            button.AddToClassList("bp-health-bar__action-btn");

            AddActionIcon(button, localPart, actionType);

            var strength = new Label(ActionStrengthLabel(_species, actionType, attackTarget));
            strength.AddToClassList("bp-health-bar__action-btn-strength");
            button.Add(strength);

            button.tooltip = localPart == null ? SkillTooltip(null) : SkillTooltip(localPart);
            if (localPart != null && localPart.IsPassive)
                button.AddToClassList("bp-health-bar__action-btn--passive");

            _actionButtonsRow.Add(button);
            _actionButtons.Add(button);
            _actionButtonParts[button] = localPart;
        }

        if (_actionIcon.style.display == DisplayStyle.None)
            _actionButtonsRow.style.display = DisplayStyle.Flex;
    }

    public void SetActionButtonsEnabled(bool enabled)
    {
        foreach (var button in _actionButtons)
            button.SetEnabled(enabled
                && _actionButtonParts.TryGetValue(button, out var part)
                && (part == null || part.IsActionSelectable));
    }

    public void SetStatusEffectsPosition(float left, float top)
    {
        _statusEffectsRow.style.left = left;
        _statusEffectsRow.style.top = top;
    }

    public void SetActionControlsPosition(float left, float top)
    {
        _actionButtonsRow.style.left = left;
        _actionButtonsRow.style.top = top;
        _actionIcon.style.top = top;
    }

    public void SetSelectedAction(Part sourcePart, SpeciesActionType actionType)
    {
        if (!_actionControlsVisible)
            return;

        foreach (var button in _actionButtons)
        {
            bool selected = _actionButtonParts.TryGetValue(button, out var part)
                && part == sourcePart
                && (sourcePart != null || actionType == SpeciesActionType.Attack);
            Utility.UI.EnableClass(selected, button, "bp-health-bar__action-btn--selected");
        }
    }

    public void SetIntentAction(SpeciesActionType? action, Species attackTarget, Part sourcePart)
    {
        if (!_intentVisible)
            return;

        _actionButtonsRow.style.display = DisplayStyle.None;
        _actionIcon.Clear();

        if (!action.HasValue)
        {
            _actionIcon.RemoveFromClassList("bp-health-bar__action-btn--enemy");
            _actionIcon.tooltip = string.Empty;
            _actionIcon.style.display = DisplayStyle.None;
            return;
        }

        Part displayPart = sourcePart;
        if (displayPart == null && action.Value != SpeciesActionType.Attack)
        {
            displayPart = _species.Parts.FirstOrDefault(part =>
                part != null && part.IsActionSelectable && part.ActionType == action.Value);
        }
        AddActionIcon(_actionIcon, displayPart, action.Value);

        var strength = new Label(ActionStrengthLabel(_species, action.Value, attackTarget));
        strength.AddToClassList("bp-health-bar__action-btn-strength");
        _actionIcon.Add(strength);

        _actionIcon.tooltip = $"Enemy intends to {ActionLabel(action.Value).ToLowerInvariant()}.";
        _actionIcon.AddToClassList("bp-health-bar__action-btn--enemy");
        _actionIcon.style.display = DisplayStyle.Flex;
    }

    static void AddActionIcon(VisualElement container, Part part, SpeciesActionType actionType)
    {
        if (part?.ActionIcon != null)
        {
            var icon = new Image
            {
                sprite = part.ActionIcon,
                scaleMode = ScaleMode.ScaleToFit,
            };
            icon.AddToClassList("bp-health-bar__action-btn-icon");
            container.Add(icon);
        }
        else
        {
            var iconFallback = new Label(ActionGlyph(actionType));
            iconFallback.AddToClassList("bp-health-bar__action-btn-icon");
            container.Add(iconFallback);
        }
    }

    /// <summary>Reads current health and updates the bar, HP text, and statuses.</summary>
    public void Refresh()
    {
        int max = Mathf.Max(1, _species.MaxHealth);
        float pct = Mathf.Clamp01((float)_species.CurrentHealth / max);

        _fill.style.width = Length.Percent(pct * 100f);

        _fill.RemoveFromClassList("bp-health-bar__fill--low");
        _fill.RemoveFromClassList("bp-health-bar__fill--critical");
        if (pct <= 0.25f)
            _fill.AddToClassList("bp-health-bar__fill--critical");
        else if (pct <= 0.5f)
            _fill.AddToClassList("bp-health-bar__fill--low");

        _hpLabel.text = $"{_species.CurrentHealth}/{max}";
        RefreshStatusEffects();
    }

    void RefreshStatusEffects()
    {
        _statusEffectsRow.Clear();

        foreach (var statusEffect in _species.ActiveStatusEffects)
        {
            if (statusEffect == null || statusEffect.IsExpired)
                continue;

            var statusIcon = new Image
            {
                sprite = statusEffect.Sprite,
                scaleMode = ScaleMode.ScaleToFit,
                tooltip = $"{statusEffect.Name} - {statusEffect.RemainingTurns} turn{(statusEffect.RemainingTurns == 1 ? string.Empty : "s")} remaining",
            };
            statusIcon.AddToClassList("bp-health-bar__status-icon");

            if (statusEffect.Sprite == null)
            {
                string statusName = string.IsNullOrWhiteSpace(statusEffect.Name) ? "?" : statusEffect.Name;
                var fallback = new Label(statusName.Substring(0, 1).ToUpperInvariant());
                fallback.AddToClassList("bp-health-bar__status-icon-fallback");
                statusIcon.Add(fallback);
            }

            _statusEffectsRow.Add(statusIcon);
        }

        _statusEffectsRow.style.display = _statusEffectsRow.childCount > 0
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    static string ActionLabel(SpeciesActionType action)
    {
        return action switch
        {
            SpeciesActionType.Forage => "FORAGE",
            SpeciesActionType.Defend => "DEFEND",
            SpeciesActionType.Blind => "BLIND",
            _ => "ATTACK",
        };
    }

    static string ActionGlyph(SpeciesActionType action)
    {
        return action switch
        {
            SpeciesActionType.Forage => "FOR",
            SpeciesActionType.Defend => "DEF",
            SpeciesActionType.Blind => "BLD",
            _ => "ATK",
        };
    }

    static string ActionLabel(Part part)
    {
        if (part == null)
            return "ATTACK";
        if (!string.IsNullOrWhiteSpace(part.ActionName))
            return part.ActionName.ToUpperInvariant();
        return ActionLabel(part.ActionType);
    }

    static string ActionStrengthLabel(Species species, SpeciesActionType action, Species attackTarget)
    {
        return action switch
        {
            SpeciesActionType.Attack => $"DMG {species.GetSizeAdjustedAttackDamage(attackTarget)}",
            SpeciesActionType.Forage => $"FOR +{species.Forage}",
            _ => ActionGlyph(action),
        };
    }

    static string SkillTooltip(Part part)
    {
        if (part == null)
            return "ATTACK\nBASIC\nAttack the selected target.";

        string type = part.IsPassive ? "PASSIVE" : ActionLabel(part.ActionType);
        string description = string.IsNullOrWhiteSpace(part.Description)
            ? "No description available."
            : part.Description;
        return $"{ActionLabel(part)}\n{type}\n{description}";
    }
}
