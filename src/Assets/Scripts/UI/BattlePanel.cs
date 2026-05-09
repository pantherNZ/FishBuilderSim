using System;
using System.Collections.Generic;
using System.Linq;
using DamageNumbersPro;
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
    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the player confirms a battle step request.</summary>
    public event Action<BattleStepRequest> OnBeginClicked;

    /// <summary>Fired when the player activates an action card. Argument is the played part.</summary>
    public event Action<Part> OnCardPlayed;

    // ── Private state ─────────────────────────────────────────────────────────

    UIDocument _doc;
    VisualElement _root;

    // Arena
    VisualElement _healthBarLayer;
    Button _beginBtn;
    VisualElement _playerSpeciesStrip;
    VisualElement _enemySpeciesStrip;

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
    Label _roundLabel;
    VisualElement _legacyStepControlsRow;
    ScrollView _logScroll;
    VisualElement _logList;
    VisualElement _cardRow;

    BattleData _data;
    readonly List<Species> _selectablePlayerSpecies = new();
    readonly Dictionary<Species, VisualElement> _speciesChips = new();
    readonly Dictionary<Species, Label> _speciesChipHpLabels = new();
    ActionManager _actionManager;
    bool _stepControlsEnabled = true;

    [Header("Damage Numbers Pro")]
    [SerializeField] DamageNumber _damageNumberPrefab;
    [SerializeField] float _damagePopupDistanceFromCamera = 10f;
    [SerializeField] Vector2 _damagePopupScreenJitter = new(18f, 10f);

    const int DefaultMaxActions = 1;

    // Health bar registry
    readonly Dictionary<Species, SpeciesHealthBarElement> _healthBars = new();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        _healthBarLayer = _root.Q("bp-health-bar-layer");
        _beginBtn = _root.Q<Button>("bp-begin-btn");
        _playerSpeciesStrip = _root.Q("bp-player-species-strip");
        _enemySpeciesStrip = _root.Q("bp-enemy-species-strip");
        _speciesInfo = _root.Q("bp-species-info");
        _infoName = _root.Q<Label>("bp-info-name");
        _infoHp = _root.Q<Label>("bp-info-hp");
        _infoAtk = _root.Q<Label>("bp-info-atk");
        _infoDef = _root.Q<Label>("bp-info-def");
        _infoSize = _root.Q<Label>("bp-info-size");
        _infoForage = _root.Q<Label>("bp-info-forage");
        _infoBehaviors = _root.Q("bp-info-behaviors");
        _roundLabel = _root.Q<Label>("bp-round-label");
        _legacyStepControlsRow = _root.Q(className: "bp-step-controls-row");
        _logScroll = _root.Q<ScrollView>("bp-log-scroll");
        _logList = _root.Q("bp-log-list");
        _cardRow = _root.Q("bp-card-row");

        _beginBtn.clicked += EmitStepRequest;

        if (_legacyStepControlsRow != null)
            _legacyStepControlsRow.style.display = DisplayStyle.None;

        HideTooltip();
        Hide();
    }

    // ── Panel visibility ──────────────────────────────────────────────────────

    /// <summary>Populates and displays the battle UI.</summary>
    public void Show(BattleData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        _data = data;
        _actionManager = new ActionManager(DefaultMaxActions);
        _stepControlsEnabled = true;

        ClearHealthBars();
        BuildSpeciesStrips(data);
        BuildActionCards(data.ActionCards);
        SetRoundAndTurn(1, true);
        SetSelectableSpecies(data.PlayerGroup);
        ClearCombatLog();
        RefreshSpeciesVisuals();
        _beginBtn.text = "CONFIRM STEP";

        _root.style.display = DisplayStyle.Flex;
        ShowBeginButton();
    }

    /// <summary>Hides the entire panel without clearing state.</summary>
    public void Hide() => _root.style.display = DisplayStyle.None;

    // ── Begin button ──────────────────────────────────────────────────────────

    public void ShowBeginButton() => _beginBtn.style.display = DisplayStyle.Flex;
    public void HideBeginButton() => _beginBtn.style.display = DisplayStyle.None;

    public void SetRoundAndTurn(int round, bool isPlayerTurn)
    {
        if (_roundLabel == null) return;
        _roundLabel.text = $"ROUND {round} - {(isPlayerTurn ? "PLAYER TURN" : "ENEMY TURN")}";
    }

    public void SetSelectableSpecies(IReadOnlyList<Species> species)
    {
        _selectablePlayerSpecies.Clear();
        if (species != null)
            _selectablePlayerSpecies.AddRange(species.Where(s => s != null && s.IsAlive));

        _actionManager?.Clear();
        RefreshActionChoiceVisuals();
    }

    public void SetStepControlsEnabled(bool enabled)
    {
        _stepControlsEnabled = enabled;

        foreach (var healthBar in _healthBars.Values)
            healthBar.SetActionButtonsEnabled(enabled);

        _beginBtn?.SetEnabled(enabled);
    }

    public void AppendCombatLog(string message)
    {
        if (_logList == null || string.IsNullOrWhiteSpace(message)) return;

        var label = new Label(message);
        label.AddToClassList("bp-log-entry");
        _logList.Add(label);

        _logScroll?.ScrollTo(label);
    }

    public void ClearCombatLog()
    {
        _logList?.Clear();
    }

    public void RefreshSpeciesVisuals()
    {
        if (_speciesChips.Count == 0) return;

        foreach (var kv in _speciesChips)
        {
            Utility.UI.EnableClass(!kv.Key.IsAlive, kv.Value, "bp-species-chip--dead");

            if (_speciesChipHpLabels.TryGetValue(kv.Key, out var hp))
                hp.text = $"HP {Mathf.Max(0, kv.Key.CurrentHealth)}/{Mathf.Max(1, kv.Key.MaxHealth)}";
        }

        RefreshActionChoiceVisuals();
    }

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
        bool isPlayerSpecies = _data?.PlayerGroup?.Contains(species) == true;
        bool isEnemySpecies = _data?.EnemyGroup?.Contains(species) == true;

        bar.SetActionControlsVisible(isPlayerSpecies);
        bar.SetIntentVisible(isEnemySpecies);
        if (isPlayerSpecies)
        {
            bar.OnActionSelected += action => HandleSpeciesActionSelected(species, action);
            bar.ConfigureActionButtons(GetAvailableActions(species));
            bar.SetActionButtonsEnabled(_stepControlsEnabled);
        }
        else if (isEnemySpecies)
        {
            bar.SetIntentAction(GetEnemyIntent(species));
        }

        _healthBarLayer.Add(bar);
        _healthBars[species] = bar;
        RefreshActionChoiceVisuals();
        return bar;
    }

    /// <summary>Removes the health bar for <paramref name="species"/>, if present.</summary>
    public void RemoveHealthBar(Species species)
    {
        if (_healthBars.TryGetValue(species, out var bar))
        {
            bar.RemoveFromHierarchy();
            _healthBars.Remove(species);
            _actionManager?.RemoveActionForActor(species);
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

        RefreshActionChoiceVisuals();
    }

    public void ShowDamageNumber(Species target, int damage)
    {
        if (_damageNumberPrefab == null || target == null || damage <= 0)
            return;

        if (!_healthBars.TryGetValue(target, out var bar))
            return;

        var bounds = bar.worldBound;
        float screenX = bounds.center.x + UnityEngine.Random.Range(-_damagePopupScreenJitter.x, _damagePopupScreenJitter.x);
        float screenY = (Screen.height - bounds.center.y) + UnityEngine.Random.Range(-_damagePopupScreenJitter.y, _damagePopupScreenJitter.y);

        var cam = Camera.main;
        Vector3 spawnPosition;

        if (cam != null)
        {
            spawnPosition = cam.ScreenToWorldPoint(new Vector3(screenX, screenY, _damagePopupDistanceFromCamera));

            // Keep orthographic cameras on a stable Z plane in front of the scene.
            if (cam.orthographic)
                spawnPosition.z = 0f;
        }
        else
        {
            spawnPosition = new Vector3(screenX, screenY, 0f);
        }

        _damageNumberPrefab.Spawn(spawnPosition, damage);
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

    void BuildSpeciesStrips(BattleData data)
    {
        _speciesChips.Clear();
        _speciesChipHpLabels.Clear();
        _playerSpeciesStrip?.Clear();
        _enemySpeciesStrip?.Clear();

        if (data?.PlayerGroup != null)
        {
            foreach (var species in data.PlayerGroup)
                _playerSpeciesStrip?.Add(BuildSpeciesChip(species));
        }

        if (data?.EnemyGroup != null)
        {
            foreach (var species in data.EnemyGroup)
                _enemySpeciesStrip?.Add(BuildSpeciesChip(species));
        }
    }

    VisualElement BuildSpeciesChip(Species species)
    {
        var chip = new VisualElement();
        chip.AddToClassList("bp-species-chip");

        var portrait = new Image();
        portrait.AddToClassList("bp-species-chip__portrait");

        if (species?.Portrait != null)
            portrait.sprite = species.Portrait;
        else
            portrait.image = GetFallbackPortrait(species?.Name ?? "?");

        chip.Add(portrait);

        var name = new Label(species?.Name?.ToUpper() ?? "?");
        name.AddToClassList("bp-species-chip__name");
        chip.Add(name);

        var hp = new Label(species == null ? "HP --" : $"HP {species.CurrentHealth}/{Mathf.Max(1, species.MaxHealth)}");
        hp.AddToClassList("bp-species-chip__hp");
        chip.Add(hp);

        if (species != null)
        {
            _speciesChips[species] = chip;
            _speciesChipHpLabels[species] = hp;
        }

        return chip;
    }

    Texture2D GetFallbackPortrait(string seed)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
        };

        Color c = Color.HSVToRGB(Mathf.Abs(seed.GetHashCode() % 97) / 97f, 0.55f, 0.65f);
        var pixels = texture.GetPixels();
        for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    void EmitStepRequest()
    {
        if (_actionManager == null || _actionManager.Actions.Count == 0)
        {
            AppendCombatLog("Select an action above a species before confirming.");
            return;
        }

        var selected = _actionManager.Actions[0];
        if (selected.Actor == null || !selected.Actor.IsAlive)
        {
            _actionManager.RemoveActionForActor(selected.Actor);
            RefreshActionChoiceVisuals();
            AppendCombatLog("Selected species is no longer valid.");
            return;
        }

        var request = new BattleStepRequest
        {
            Actor = selected.Actor,
            Action = ToBattleStepAction(selected.Type),
            ActionManager = _actionManager,
        };

        OnBeginClicked?.Invoke(request);
    }

    void HandleSpeciesActionSelected(Species species, SpeciesActionType actionType)
    {
        if (!_stepControlsEnabled)
            return;

        if (species == null || !species.IsAlive || _actionManager == null)
            return;

        List<Species> targets = null;
        if (actionType == SpeciesActionType.Attack)
        {
            var enemyCandidates = _data?.EnemyGroup?.Where(s => s != null && s.IsAlive) ?? Enumerable.Empty<Species>();
            var target = species.PickTarget(enemyCandidates);
            if (target == null)
                return;
            targets = new List<Species> { target };
        }

        _actionManager.SetAction(new SpeciesAction
        {
            Actor = species,
            Type = actionType,
            Targets = targets,
        });

        RefreshActionChoiceVisuals();
    }

    IReadOnlyList<SpeciesActionType> GetAvailableActions(Species species)
    {
        if (species == null || !species.IsAlive)
            return Array.Empty<SpeciesActionType>();

        var actions = new List<SpeciesActionType>();

        var enemyCandidates = _data?.EnemyGroup?.Where(s => s != null && s.IsAlive) ?? Enumerable.Empty<Species>();
        bool hasEnemyTarget = species.PickTarget(enemyCandidates) != null;
        if (species.Attack > 0 && species.CanAttack && hasEnemyTarget)
            actions.Add(SpeciesActionType.Attack);

        if (species.Forage > 0 && species.CanForage)
            actions.Add(SpeciesActionType.Forage);

        if (species.CanDefend)
            actions.Add(SpeciesActionType.Defend);

        return actions;
    }

    SpeciesActionType? GetEnemyIntent(Species species)
    {
        if (species == null || !species.IsAlive)
            return null;

        var playerCandidates = _data?.PlayerGroup?.Where(s => s != null && s.IsAlive) ?? Enumerable.Empty<Species>();
        bool hasPlayerTarget = species.PickTarget(playerCandidates) != null;

        if (species.Attack > 0 && species.CanAttack && hasPlayerTarget)
            return SpeciesActionType.Attack;
        if (species.Forage > 0 && species.CanForage)
            return SpeciesActionType.Forage;
        if (species.CanDefend)
            return SpeciesActionType.Defend;

        return null;
    }

    void RefreshActionChoiceVisuals()
    {
        var playerSpecies = _data?.PlayerGroup;
        if (playerSpecies != null)
        {
            foreach (var species in playerSpecies)
            {
                if (species == null || !_healthBars.TryGetValue(species, out var bar))
                    continue;

                bar.ConfigureActionButtons(GetAvailableActions(species));
                bar.SetActionButtonsEnabled(_stepControlsEnabled);

                if (!species.IsAlive)
                    _actionManager?.RemoveActionForActor(species);

                if (_actionManager != null && _actionManager.TryGetActionForActor(species, out var selected))
                    bar.SetSelectedAction(selected.Type);
                else
                    bar.SetSelectedAction(null);
            }
        }

        var enemySpecies = _data?.EnemyGroup;
        if (enemySpecies != null)
        {
            foreach (var species in enemySpecies)
            {
                if (species == null || !_healthBars.TryGetValue(species, out var bar))
                    continue;

                bar.SetIntentAction(GetEnemyIntent(species));
            }
        }
    }

    static BattleStepAction ToBattleStepAction(SpeciesActionType action)
    {
        return action switch
        {
            SpeciesActionType.Forage => BattleStepAction.Forage,
            SpeciesActionType.Defend => BattleStepAction.Defend,
            _ => BattleStepAction.Attack,
        };
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
        readonly VisualElement _actionButtonsRow;
        readonly Label _actionIcon;
        readonly List<Button> _actionButtons = new();
        bool _actionControlsVisible;
        bool _intentVisible;

        public event Action<SpeciesActionType> OnActionSelected;

        public SpeciesHealthBarElement(Species species)
        {
            _species = species;

            AddToClassList("bp-health-bar");
            style.position = Position.Absolute;

            _actionButtonsRow = new VisualElement();
            _actionButtonsRow.AddToClassList("bp-health-bar__actions");
            Add(_actionButtonsRow);

            _actionIcon = new Label();
            _actionIcon.AddToClassList("bp-health-bar__action-icon");
            _actionIcon.style.display = DisplayStyle.None;
            Add(_actionIcon);

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

            _actionIcon.RemoveFromClassList("bp-health-bar__action-icon--enemy");

            if (_actionIcon.style.display == DisplayStyle.None)
                _actionButtonsRow.style.display = _actionButtons.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetIntentVisible(bool visible)
        {
            _intentVisible = visible;

            if (!visible)
            {
                _actionIcon.RemoveFromClassList("bp-health-bar__action-icon--enemy");
                if (!_actionControlsVisible)
                    _actionIcon.style.display = DisplayStyle.None;
            }
        }

        public void ConfigureActionButtons(IReadOnlyList<SpeciesActionType> actions)
        {
            _actionButtonsRow.Clear();
            _actionButtons.Clear();

            if (!_actionControlsVisible || actions == null || actions.Count == 0)
            {
                _actionButtonsRow.style.display = DisplayStyle.None;
                return;
            }

            foreach (var action in actions)
            {
                var localAction = action;
                var button = new Button(() => OnActionSelected?.Invoke(localAction))
                {
                    text = ActionLabel(localAction),
                };
                button.AddToClassList("bp-health-bar__action-btn");
                _actionButtonsRow.Add(button);
                _actionButtons.Add(button);
            }

            if (_actionIcon.style.display == DisplayStyle.None)
                _actionButtonsRow.style.display = DisplayStyle.Flex;
        }

        public void SetActionButtonsEnabled(bool enabled)
        {
            foreach (var button in _actionButtons)
                button.SetEnabled(enabled);
        }

        public void SetSelectedAction(SpeciesActionType? action)
        {
            if (!_actionControlsVisible)
                return;

            if (!action.HasValue)
            {
                _actionIcon.RemoveFromClassList("bp-health-bar__action-icon--enemy");
                _actionIcon.style.display = DisplayStyle.None;
                _actionButtonsRow.style.display = _actionButtons.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                return;
            }

            _actionIcon.RemoveFromClassList("bp-health-bar__action-icon--enemy");
            _actionIcon.text = ActionLabel(action.Value);
            _actionIcon.style.display = DisplayStyle.Flex;
            _actionButtonsRow.style.display = DisplayStyle.None;
        }

        public void SetIntentAction(SpeciesActionType? action)
        {
            if (!_intentVisible)
                return;

            _actionButtonsRow.style.display = DisplayStyle.None;

            if (!action.HasValue)
            {
                _actionIcon.style.display = DisplayStyle.None;
                return;
            }

            _actionIcon.text = ActionLabel(action.Value);
            _actionIcon.AddToClassList("bp-health-bar__action-icon--enemy");
            _actionIcon.style.display = DisplayStyle.Flex;
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

        static string ActionLabel(SpeciesActionType action)
        {
            return action switch
            {
                SpeciesActionType.Forage => "FOR",
                SpeciesActionType.Defend => "DEF",
                _ => "ATK",
            };
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
