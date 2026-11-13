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
    readonly Label _energyLabel;
    readonly VisualElement _statusEffectsRow;
    readonly VisualElement _actionButtonsRow;
    readonly VisualElement _actionIcon;
    readonly List<Button> _actionButtons = new();
    readonly Dictionary<Button, Part> _actionButtonParts = new();
    bool _actionControlsVisible;
    bool _intentVisible;

    public event Action<Part, Vector2> OnActionSelected;

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

        _energyLabel = new Label();
        _energyLabel.AddToClassList("bp-health-bar__energy");
        _energyLabel.tooltip = "Each fish refreshes to 5 energy at the start of its turn.";
        Add(_energyLabel);

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
            var button = CreateActionButton(_species, localPart, attackTarget);
            button.RegisterCallback<ClickEvent>(evt => OnActionSelected?.Invoke(localPart, evt.position));

            _actionButtonsRow.Add(button);
            _actionButtons.Add(button);
            _actionButtonParts[button] = localPart;
        }

        if (_actionIcon.style.display == DisplayStyle.None)
            _actionButtonsRow.style.display = DisplayStyle.Flex;
    }

    /// <summary>Creates the same skill preview used by encounters and the species editor.</summary>
    public static Button CreateActionButton(Species species, Part part, Species attackTarget = null)
    {
        var actionType = part == null ? SpeciesActionType.Attack : part.ActionType;
        var button = new Button
        {
            text = string.Empty,
            tooltip = SkillTooltip(part, species),
        };
        button.AddToClassList("bp-health-bar__action-btn");
        AddActionIcon(button, part, actionType);

        var strength = new Label(ActionStrengthLabel(species, actionType, attackTarget));
        strength.AddToClassList("bp-health-bar__action-btn-strength");
        button.Add(strength);
        AddResourceBadges(button, species, part);

        if (part != null && part.IsPassive)
        {
            button.AddToClassList("bp-health-bar__action-btn--passive");
        }

        return button;
    }

    /// <summary>Basic attack followed by distinct, selectable equipped skills.</summary>
    public static IReadOnlyList<Part> GetAvailableActions(Species species)
    {
        if (species == null)
        {
            return Array.Empty<Part>();
        }

        var actions = new List<Part> { null };
        var actionKeys = new HashSet<string> { GetActionDisplayKey(null) };
        foreach (var part in species.GetActionParts())
        {
            if (part == null || !part.IsActionSelectable)
            {
                continue;
            }

            if (actionKeys.Add(GetActionDisplayKey(part)))
            {
                actions.Add(part);
            }
            else
            {
                int existingIndex = actions.FindIndex(candidate => GetActionDisplayKey(candidate) == GetActionDisplayKey(part));
                var existing = actions[existingIndex];
                if (!species.CanUseAction(existing, existing == null ? SpeciesActionType.Attack : existing.ActionType)
                    && species.CanUseAction(part, part.ActionType))
                {
                    actions[existingIndex] = part;
                }
            }
        }

        return actions;
    }

    static string GetActionDisplayKey(Part part)
    {
        SpeciesActionType actionType = part == null ? SpeciesActionType.Attack : part.ActionType;
        string actionName = part == null || string.IsNullOrWhiteSpace(part.ActionName)
            ? actionType.ToString()
            : part.ActionName.Trim();
        return $"{actionType}|{actionName.ToUpperInvariant()}";
    }

    public void SetActionButtonsEnabled(bool enabled)
    {
        foreach (var button in _actionButtons)
        {
            button.SetEnabled(enabled
                && _species.IsAlive
                && _actionButtonParts.TryGetValue(button, out var part)
                && _species.CanUseAction(part, part == null ? SpeciesActionType.Attack : part.ActionType));
        }
    }

    static void AddResourceBadges(VisualElement container, Species species, Part part)
    {
        container.RemoveFromClassList("bp-health-bar__action-btn--cooldown");
        container.RemoveFromClassList("bp-health-bar__action-btn--no-energy");
        if (part != null && part.IsPassive)
        {
            return;
        }

        var cost = new Label(Species.GetActionEnergyCost(part).ToString())
        {
            pickingMode = PickingMode.Ignore,
        };
        cost.AddToClassList("bp-health-bar__action-cost");
        container.Add(cost);

        int remaining = species.GetRemainingCooldown(part);
        if (remaining > 0)
        {
            var cooldown = new Label($"CD {remaining}")
            {
                pickingMode = PickingMode.Ignore,
            };
            cooldown.AddToClassList("bp-health-bar__action-cooldown");
            container.Add(cooldown);
            container.AddToClassList("bp-health-bar__action-btn--cooldown");
        }
        else if (species.CurrentEnergy < Species.GetActionEnergyCost(part))
        {
            container.AddToClassList("bp-health-bar__action-btn--no-energy");
        }
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
        AddResourceBadges(_actionIcon, _species, displayPart);

        _actionIcon.tooltip = $"Enemy intends to {ActionLabel(action.Value).ToLowerInvariant()}.\n{SkillTooltip(displayPart, _species)}";
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
        _energyLabel.text = $"ENERGY {_species.CurrentEnergy}/{Species.EnergyPerTurn}";
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

    static string SkillTooltip(Part part, Species species)
    {
        if (part == null)
        {
            return $"ATTACK\nBASIC\nEnergy: {Species.BasicAttackEnergyCost} | Cooldown: 0 turns\nAttack the selected target.";
        }

        string type = part.IsPassive ? "PASSIVE" : ActionLabel(part.ActionType);
        string description = string.IsNullOrWhiteSpace(part.Description)
            ? "No description available."
            : part.Description;
        string resources = part.IsPassive
            ? "Passive: no energy cost or cooldown."
            : $"Energy: {Species.GetActionEnergyCost(part)} | Cooldown: {Mathf.Max(0, part.CooldownTurns)} turns\n"
                + $"Cooldown remaining: {species.GetRemainingCooldown(part)} turn starts.";
        return $"{ActionLabel(part)}\n{type}\n{resources}\n{description}";
    }
}
