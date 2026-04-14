using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Full-screen battle scene UI.
///
/// Layout overview:
///   • <b>Arena</b> (centre, flex-fills the screen above the tray) — left empty
///     for the scene camera to render species sprites. Health bars float here.
///   • <b>Species info overlay</b> (top-right, absolute) — shown when hovering
///     a species sprite; displays stats and active behaviour names.
///   • <b>Begin button</b> — centred in the arena; hidden once combat starts.
///   • <b>Action card tray</b> (bottom) — horizontally scrollable row of the
///     player's parts that can be played during an encounter.
///
/// Usage:
/// <code>
///   battlePanel.Show(new BattleData { PlayerGroup = …, EnemyGroup = …, ActionCards = … });
///   battlePanel.OnBeginClicked += StartCombat;
///   battlePanel.OnCardPlayed   += part => ApplyPartEffect(part);
///
///   // From a sprite pointer-enter callback:
///   battlePanel.ShowTooltip(species);
///   // From pointer-exit:
///   battlePanel.HideTooltip();
///
///   // After spawning sprites, anchor each health bar:
///   var bar = battlePanel.AddHealthBar(species);
///   battlePanel.SetHealthBarPosition(species, arenaLocalPos);
///   // Each combat tick:
///   battlePanel.RefreshHealthBars();
/// </code>
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class BattlePanel : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Tooltip("Leave null to load from Resources/UI/BattlePanel.")]
    public VisualTreeAsset OverrideUxml;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the player presses the Begin button.</summary>
    public event Action OnBeginClicked;

    /// <summary>Fired when the player activates an action card. Argument is the played part.</summary>
    public event Action<Part> OnCardPlayed;

    // ── Private state ─────────────────────────────────────────────────────────

    UIDocument _doc;
    VisualElement _root;

    // Arena
    VisualElement _healthBarLayer;
    Button _beginBtn;

    // Species info overlay
    VisualElement _speciesInfo;
    Label _infoName;
    Label _infoHp;
    Label _infoAtk;
    Label _infoDef;
    Label _infoSize;
    Label _infoForage;
    VisualElement _infoBehaviors;

    // Action tray
    VisualElement _cardRow;

    // Health bar registry
    readonly Dictionary<Species, SpeciesHealthBarElement> _healthBars = new();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _doc = GetComponent<UIDocument>();

        var asset = OverrideUxml
            ? OverrideUxml
            : Resources.Load<VisualTreeAsset>("UI/BattlePanel");

        if (asset == null)
        {
            Debug.LogError("[BattlePanel] Could not load BattlePanel.uxml from Resources/UI/");
            return;
        }

        _root = asset.CloneTree();
        _doc.rootVisualElement.Add(_root);

        _healthBarLayer = _root.Q("bp-health-bar-layer");
        _beginBtn = _root.Q<Button>("bp-begin-btn");
        _speciesInfo = _root.Q("bp-species-info");
        _infoName = _root.Q<Label>("bp-info-name");
        _infoHp = _root.Q<Label>("bp-info-hp");
        _infoAtk = _root.Q<Label>("bp-info-atk");
        _infoDef = _root.Q<Label>("bp-info-def");
        _infoSize = _root.Q<Label>("bp-info-size");
        _infoForage = _root.Q<Label>("bp-info-forage");
        _infoBehaviors = _root.Q("bp-info-behaviors");
        _cardRow = _root.Q("bp-card-row");

        _beginBtn.clicked += () => OnBeginClicked?.Invoke();

        HideTooltip();
        Hide();
    }

    // ── Panel visibility ──────────────────────────────────────────────────────

    /// <summary>Populates and displays the battle UI.</summary>
    public void Show(BattleData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        ClearHealthBars();
        BuildActionCards(data.ActionCards);

        _root.style.display = DisplayStyle.Flex;
        ShowBeginButton();
    }

    /// <summary>Hides the entire panel without clearing state.</summary>
    public void Hide() => _root.style.display = DisplayStyle.None;

    // ── Begin button ──────────────────────────────────────────────────────────

    public void ShowBeginButton() => _beginBtn.style.display = DisplayStyle.Flex;
    public void HideBeginButton() => _beginBtn.style.display = DisplayStyle.None;

    // ── Species info tooltip ──────────────────────────────────────────────────

    /// <summary>
    /// Populates and shows the species info overlay with <paramref name="species"/> data.
    /// Intended to be called from a sprite pointer-enter callback.
    /// </summary>
    public void ShowTooltip(Species species)
    {
        if (species == null) return;

        _infoName.text = species.Name?.ToUpper() ?? "—";
        _infoHp.text = $"{species.CurrentHealth} / {species.MaxHealth}";
        _infoAtk.text = species.Attack.ToString();
        _infoDef.text = species.Defense.ToString();
        _infoSize.text = species.Size.ToString();
        _infoForage.text = species.Forage.ToString();

        _infoBehaviors.Clear();
        foreach (var part in species.Parts)
            foreach (var behavior in part.Behaviors)
            {
                var tag = new Label(FormatBehaviorName(behavior));
                tag.AddToClassList("bp-behavior-tag");
                _infoBehaviors.Add(tag);
            }

        _speciesInfo.RemoveFromClassList("bp-hidden");
    }

    /// <summary>Hides the species info overlay.</summary>
    public void HideTooltip() => _speciesInfo.AddToClassList("bp-hidden");

    // ── Health bars ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a floating health bar for <paramref name="species"/> and adds it
    /// to the arena's health-bar layer.
    /// Position defaults to (0,0); call <see cref="SetHealthBarPosition"/> to
    /// anchor it above the species' sprite.
    /// </summary>
    public SpeciesHealthBarElement AddHealthBar(Species species)
    {
        if (_healthBars.TryGetValue(species, out var existing))
            return existing;

        var bar = new SpeciesHealthBarElement(species);
        _healthBarLayer.Add(bar);
        _healthBars[species] = bar;
        return bar;
    }

    /// <summary>Removes the health bar for <paramref name="species"/>, if present.</summary>
    public void RemoveHealthBar(Species species)
    {
        if (_healthBars.TryGetValue(species, out var bar))
        {
            bar.RemoveFromHierarchy();
            _healthBars.Remove(species);
        }
    }

    /// <summary>
    /// Moves the health bar for <paramref name="species"/> to
    /// <paramref name="arenaPosition"/> (arena-layer local pixels, origin top-left).
    /// Typically called each frame to track the species' sprite position.
    /// </summary>
    public void SetHealthBarPosition(Species species, Vector2 arenaPosition)
    {
        if (!_healthBars.TryGetValue(species, out var bar)) return;
        bar.style.left = arenaPosition.x;
        bar.style.top = arenaPosition.y;
    }

    /// <summary>Reads current health from every registered species and redraws all bars.</summary>
    public void RefreshHealthBars()
    {
        foreach (var bar in _healthBars.Values)
            bar.Refresh();
    }

    void ClearHealthBars()
    {
        _healthBarLayer.Clear();
        _healthBars.Clear();
    }

    // ── Action card tray ──────────────────────────────────────────────────────

    void BuildActionCards(IReadOnlyList<Part> parts)
    {
        _cardRow.Clear();
        if (parts == null) return;

        foreach (var part in parts)
        {
            var card = new ActionCardElement(part);
            card.OnPlayed += p => OnCardPlayed?.Invoke(p);
            _cardRow.Add(card);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static string FormatBehaviorName(PartBehaviorBase b) =>
        b.GetType().Name.Replace("Behavior", string.Empty);

    // ═════════════════════════════════════════════════════════════════════════
    // Inner type: SpeciesHealthBarElement
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A floating health bar that displays a species' name and current HP as a
    /// coloured fill bar.  Add to the arena health-bar layer via
    /// <see cref="AddHealthBar"/>; reposition via <see cref="SetHealthBarPosition"/>.
    /// Call <see cref="Refresh"/> after any health change.
    /// </summary>
    public class SpeciesHealthBarElement : VisualElement
    {
        readonly Species _species;
        readonly VisualElement _fill;
        readonly Label _hpLabel;

        public SpeciesHealthBarElement(Species species)
        {
            _species = species;

            AddToClassList("bp-health-bar");
            style.position = Position.Absolute;

            // Species name
            var nameLabel = new Label(species.Name?.ToUpper() ?? "?");
            nameLabel.AddToClassList("bp-health-bar__name");
            Add(nameLabel);

            // Track + fill
            var track = new VisualElement();
            track.AddToClassList("bp-health-bar__track");
            _fill = new VisualElement();
            _fill.AddToClassList("bp-health-bar__fill");
            track.Add(_fill);
            Add(track);

            // HP fraction text
            _hpLabel = new Label();
            _hpLabel.AddToClassList("bp-health-bar__hp-text");
            Add(_hpLabel);

            Refresh();
        }

        /// <summary>
        /// Reads <see cref="Species.CurrentHealth"/> and <see cref="Species.MaxHealth"/>
        /// and updates the fill width and colour.
        /// </summary>
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
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Inner type: ActionCardElement
    // ═════════════════════════════════════════════════════════════════════════

    class ActionCardElement : VisualElement
    {
        public event Action<Part> OnPlayed;

        readonly Part _part;
        bool _played;

        public ActionCardElement(Part part)
        {
            _part = part;

            AddToClassList("bp-action-card");

            var icon = new VisualElement();
            icon.AddToClassList("bp-action-card__icon");
            Add(icon);

            var nameLabel = new Label(part.Name?.ToUpper() ?? "—");
            nameLabel.AddToClassList("bp-action-card__name");
            Add(nameLabel);

            var statsLabel = new Label(BuildStatLine(part));
            statsLabel.AddToClassList("bp-action-card__stats");
            Add(statsLabel);

            RegisterCallback<ClickEvent>(_ => Play());
        }

        void Play()
        {
            if (_played) return;
            _played = true;
            AddToClassList("bp-action-card--played");
            OnPlayed?.Invoke(_part);
        }

        static string BuildStatLine(Part p)
        {
            var parts = new System.Text.StringBuilder();
            if (p.Attack != 0) Append(parts, $"ATK {p.Attack:+0;-0}");
            if (p.Defense != 0) Append(parts, $"DEF {p.Defense:+0;-0}");
            if (p.Health != 0) Append(parts, $"HP {p.Health:+0;-0}");
            if (p.Forage != 0) Append(parts, $"FOR {p.Forage:+0;-0}");
            return parts.Length > 0 ? parts.ToString() : "—";
        }

        static void Append(System.Text.StringBuilder sb, string segment)
        {
            if (sb.Length > 0) sb.Append("  ");
            sb.Append(segment);
        }
    }
}
