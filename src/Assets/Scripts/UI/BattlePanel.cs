using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DamageNumbersPro;
using Runtime.Game;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Full-screen battle scene UI.
///
/// Layout overview:
///   • <b>Arena</b> (centre, flex-fills the screen above the tray) — displays
///     the player and enemy species visuals with floating health bars.
///   • <b>Species info overlay</b> (top-right, absolute) — shown when hovering
///     a species sprite; displays stats and active behaviour names.
///   • <b>Action targeting</b> — drag an action toward a target and release to
///     resolve it immediately.
///   • <b>Action tray</b> (bottom) — turn controls and the combat log.
///
/// Usage:
/// <code>
///   battlePanel.Show(new BattleData { PlayerGroup = …, EnemyGroup = … });
///   battlePanel.OnBeginClicked += StartCombat;
///   // Each combat tick:
///   battlePanel.RefreshHealthBars();
/// </code>
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class BattlePanel : MonoBehaviour
{
    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the player selects an action and any required target.</summary>
    public event Action<BattleStepRequest> OnBeginClicked;
    public event Action OnEndTurnClicked;

    // ── Private state ─────────────────────────────────────────────────────────

    UIDocument _doc;
    VisualElement _root;
    VisualElement _panelRoot;
    VisualElement _actionTooltip;
    Label _actionTooltipTitle;
    Label _actionTooltipDescription;
    VisualElement _actionTooltipAnchor;

    // Arena
    VisualElement _arena;
    VisualElement _healthBarLayer;
    VisualElement _speciesVisualLayer;
    Button _endTurnBtn;
    VisualElement _playerSpeciesStrip;
    VisualElement _enemySpeciesStrip;

    // Species info overlay
    VisualElement _speciesInfo;
    Label _infoName;
    Label _infoHp;
    Label _infoAtk;
    Label _infoDef;
    Label _infoForage;
    Label _infoSize;
    Label _infoStrength;
    VisualElement _infoBehaviors;
    Species _tooltipSpecies;
    VisualElement _tooltipTrigger;

    // Action tray
    Label _roundLabel;
    Label _energyValueLabel;
    Label _energySpeciesLabel;
    ScrollView _logScroll;
    VisualElement _logList;
    RectTransform _damageNumberCanvas;
    Canvas _damageNumberCanvasComponent;

    BattleData _data;
    Species _selectedPlayerSpecies;
    readonly List<Species> _selectablePlayerSpecies = new();
    readonly Dictionary<Species, VisualElement> _speciesChips = new();
    readonly Dictionary<Species, Label> _speciesChipHpLabels = new();
    readonly Dictionary<Species, Label> _speciesChipSizeLabels = new();
    ActionManager _actionManager;
    Species _enemyIntentSpecies;
    SpeciesActionType? _enemyIntentAction;
    Species _enemyIntentTarget;
    Part _enemyIntentPart;
    bool _stepControlsEnabled = true;
    Species _targetingActor;
    Species _targetingTarget;
    Part _targetingSourcePart;
    SpeciesActionType _targetingActionType;
    Vector2 _targetingStartPosition;
    Vector2 _targetingPointerPosition;
    VisualElement _targetLine;
    Label _targetArrow;
    bool _isActionPointerActive;
    int _actionPointerId = -1;
    VisualElement _actionPointerElement;
    Species _pendingActionActor;
    Part _pendingActionSourcePart;
    SpeciesActionType _pendingActionType;
    readonly HashSet<Species> _damageEventSpecies = new();

    [Header("Damage Numbers Pro")]
    [SerializeField] DamageNumber _damageNumberPrefab;
    [SerializeField] Vector2 _damagePopupScreenJitter = new(18f, 10f);

    [Header("Battle Fish Visuals")]
    [SerializeField] Vector2 _speciesVisualBaseSize = new(112f, 76f);
    [Tooltip("Species size rendered at the smallest visual scale.")]
    [SerializeField] float _speciesVisualMinimumSize = 1f;
    [Tooltip("Species size rendered at the largest visual scale.")]
    [SerializeField] float _speciesVisualMaximumSize = 12f;
    [SerializeField] float _speciesVisualMaximumScale = 4f;

    const int DefaultMaxActions = 5;

    // Health bar registry
    readonly Dictionary<Species, SpeciesHealthBarElement> _healthBars = new();
    readonly Dictionary<Species, Image> _speciesVisuals = new();
    readonly Dictionary<Species, Vector2> _speciesVisualCenters = new();
    readonly Dictionary<Species, VisualElement> _blockIndicators = new();
    readonly Dictionary<Species, Label> _blockIndicatorCounts = new();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;
        _panelRoot = _root.Q("bp-root");

        _arena = _root.Q("bp-arena");
        _healthBarLayer = _root.Q("bp-health-bar-layer");
        _speciesVisualLayer = _root.Q("bp-species-visual-layer");
        _endTurnBtn = _root.Q<Button>("bp-end-turn-btn");
        _playerSpeciesStrip = _root.Q("bp-player-species-strip");
        _enemySpeciesStrip = _root.Q("bp-enemy-species-strip");
        _speciesInfo = _root.Q("bp-species-info");
        _infoName = _root.Q<Label>("bp-info-name");
        _infoHp = _root.Q<Label>("bp-info-hp");
        _infoAtk = _root.Q<Label>("bp-info-atk");
        _infoDef = _root.Q<Label>("bp-info-def");
        _infoForage = _root.Q<Label>("bp-info-forage");
        _infoSize = _root.Q<Label>("bp-info-size");
        _infoStrength = _root.Q<Label>("bp-info-strength");
        _infoBehaviors = _root.Q("bp-info-behaviors");
        BuildActionTooltip();
        _roundLabel = _root.Q<Label>("bp-round-label");
        _energyValueLabel = _root.Q<Label>("bp-energy-value");
        _energySpeciesLabel = _root.Q<Label>("bp-energy-species");
        _logScroll = _root.Q<ScrollView>("bp-log-scroll");
        _logList = _root.Q("bp-log-list");
        BuildDamageNumberCanvas();

        _endTurnBtn.clicked += () =>
        {
            if (_stepControlsEnabled)
            {
                OnEndTurnClicked?.Invoke();
            }
        };
        _arena.RegisterCallback<GeometryChangedEvent>(OnArenaGeometryChanged);

        _targetLine = new VisualElement
        {
            name = "bp-target-line",
            pickingMode = PickingMode.Ignore,
        };
        _targetLine.AddToClassList("bp-target-line");
        _targetArrow = new Label(">");
        _targetArrow.AddToClassList("bp-target-line__arrow");
        _targetArrow.pickingMode = PickingMode.Ignore;
        _targetLine.Add(_targetArrow);
        _arena.Add(_targetLine);
        HideTargetLine();

        if (_damageNumberPrefab == null)
            Debug.LogWarning("[BattlePanel] Damage Number Prefab is not assigned; attack damage popups are disabled.", this);

        HideTooltip();
        Hide();
    }

    void BuildDamageNumberCanvas()
    {
        var canvasObject = new GameObject(
            "BattleDamageNumbersCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;
        _damageNumberCanvasComponent = canvas;

        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1200f, 800f);

        _damageNumberCanvas = canvasObject.GetComponent<RectTransform>();
    }

    void BuildActionTooltip()
    {
        _actionTooltip = new VisualElement
        {
            name = "bp-action-tooltip",
            pickingMode = PickingMode.Ignore,
        };
        _actionTooltip.AddToClassList("bp-action-tooltip");

        _actionTooltipTitle = new Label { name = "bp-action-tooltip-title" };
        _actionTooltipTitle.AddToClassList("bp-action-tooltip__title");
        _actionTooltipTitle.pickingMode = PickingMode.Ignore;
        _actionTooltip.Add(_actionTooltipTitle);

        _actionTooltipDescription = new Label { name = "bp-action-tooltip-description" };
        _actionTooltipDescription.AddToClassList("bp-action-tooltip__description");
        _actionTooltipDescription.pickingMode = PickingMode.Ignore;
        _actionTooltip.Add(_actionTooltipDescription);

        _panelRoot.Add(_actionTooltip);
        _actionTooltip.style.display = DisplayStyle.None;
    }

    void ShowActionTooltip(VisualElement anchor)
    {
        if (_isActionPointerActive || anchor == null || _actionTooltip == null || _panelRoot == null)
        {
            return;
        }

        string tooltip = anchor.tooltip ?? string.Empty;
        int separator = tooltip.IndexOf('\n');
        _actionTooltipTitle.text = separator < 0 ? tooltip : tooltip.Substring(0, separator);
        _actionTooltipDescription.text = separator < 0 ? string.Empty : tooltip.Substring(separator + 1);
        _actionTooltipAnchor = anchor;
        _actionTooltip.style.display = DisplayStyle.Flex;
        _panelRoot.schedule.Execute(() =>
        {
            if (_actionTooltipAnchor == anchor)
            {
                PositionActionTooltip(anchor);
            }
        });
    }

    void HideActionTooltip(VisualElement anchor = null)
    {
        if (anchor != null && _actionTooltipAnchor != anchor)
        {
            return;
        }

        _actionTooltipAnchor = null;
        if (_actionTooltip != null)
        {
            _actionTooltip.style.display = DisplayStyle.None;
        }
    }

    void PositionActionTooltip(VisualElement anchor)
    {
        if (anchor == null || _actionTooltip == null || _panelRoot == null)
        {
            return;
        }

        Rect bounds = anchor.worldBound;
        Vector2 topLeft = _panelRoot.WorldToLocal(bounds.position);
        Vector2 bottomRight = _panelRoot.WorldToLocal(new Vector2(bounds.xMax, bounds.yMax));
        float tooltipWidth = Mathf.Max(1f, _actionTooltip.resolvedStyle.width);
        float tooltipHeight = Mathf.Max(1f, _actionTooltip.resolvedStyle.height);
        float panelWidth = Mathf.Max(1f, _panelRoot.resolvedStyle.width);
        float panelHeight = Mathf.Max(1f, _panelRoot.resolvedStyle.height);
        const float edgePadding = 8f;
        const float gap = 8f;

        float left = Mathf.Clamp((topLeft.x + bottomRight.x - tooltipWidth) * 0.5f,
            edgePadding, Mathf.Max(edgePadding, panelWidth - tooltipWidth - edgePadding));
        float top = topLeft.y - tooltipHeight - gap;
        if (top < edgePadding)
        {
            top = bottomRight.y + gap;
        }
        top = Mathf.Clamp(top, edgePadding,
            Mathf.Max(edgePadding, panelHeight - tooltipHeight - edgePadding));

        _actionTooltip.style.left = left;
        _actionTooltip.style.top = top;
    }

    // ── Panel visibility ──────────────────────────────────────────────────────

    /// <summary>Populates and displays the battle UI.</summary>
    public void Show(BattleData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        ClearDamageEventSubscriptions();
        _data = data;
        _actionManager = new ActionManager(DefaultMaxActions);
        _selectedPlayerSpecies = null;
        _selectablePlayerSpecies.Clear();
        _enemyIntentSpecies = null;
        _enemyIntentAction = null;
        _enemyIntentTarget = null;
        _enemyIntentPart = null;
        _stepControlsEnabled = true;
        CancelActionGesture();
        HideActionTooltip();

        ClearHealthBars();
        ClearSpeciesVisuals();
        BuildSpeciesStrips(data);
        BuildSpeciesVisuals(data);
        SubscribeToDamageEvents();
        SetRoundAndTurn(1, true);
        SetSelectableSpecies(data.PlayerGroup);
        ClearCombatLog();
        RefreshSpeciesVisuals();
        _root.style.display = DisplayStyle.Flex;
        _damageNumberCanvasComponent.enabled = true;
        _root.schedule.Execute(RefreshSpeciesVisuals);
    }

    /// <summary>Hides the entire panel without clearing state.</summary>
    public void Hide()
    {
        CancelActionGesture();
        HideActionTooltip();
        ClearDamageEventSubscriptions();
        if (_damageNumberCanvasComponent != null)
        {
            _damageNumberCanvasComponent.enabled = false;
        }
        _root.style.display = DisplayStyle.None;
    }

    void OnDisable()
    {
        CancelActionGesture();
        HideActionTooltip();
        ClearDamageEventSubscriptions();
        if (_damageNumberCanvasComponent != null)
        {
            _damageNumberCanvasComponent.enabled = false;
        }
    }

    void OnArenaGeometryChanged(GeometryChangedEvent _)
    {
        RefreshArenaSpeciesVisuals();
    }

    // ── Turn controls ────────────────────────────────────────────────────────

    public void SetRoundAndTurn(int round, bool isPlayerTurn)
    {
        _roundLabel.text = $"TURN {round} - {(isPlayerTurn ? "PLAYER TURN" : "ENEMY TURN")}";
    }

    public void SetEnemyIntent(
        Species species,
        SpeciesActionType? action,
        Species target = null,
        Part sourcePart = null)
    {
        _enemyIntentSpecies = species;
        _enemyIntentAction = action;
        _enemyIntentTarget = target;
        _enemyIntentPart = sourcePart;

        foreach (var kv in _healthBars)
        {
            bool isIntentSpecies = kv.Key == _enemyIntentSpecies;
            kv.Value.SetIntentAction(
                isIntentSpecies ? _enemyIntentAction : null,
                isIntentSpecies ? _enemyIntentTarget : null,
                isIntentSpecies ? _enemyIntentPart : null);
        }
    }

    public void SetSelectableSpecies(IReadOnlyList<Species> species)
    {
        _selectablePlayerSpecies.Clear();
        if (species != null)
            _selectablePlayerSpecies.AddRange(species.Where(s => s != null && s.IsAlive));

        if (!_selectablePlayerSpecies.Contains(_selectedPlayerSpecies))
        {
            _selectedPlayerSpecies = _selectablePlayerSpecies.FirstOrDefault();
        }

        if (_pendingActionActor != null && !_selectablePlayerSpecies.Contains(_pendingActionActor))
            CancelActionGesture();

        if (_actionManager != null && _actionManager.Actions.Count > 0)
        {
            var selectedAction = _actionManager.Actions[0];
            bool actorIsSelectable = _selectablePlayerSpecies.Contains(selectedAction.Actor);
            bool actionIsAvailable = actorIsSelectable
                && selectedAction.Actor.CanUseAction(selectedAction.SourcePart, selectedAction.Type);

            if (!actionIsAvailable)
                _actionManager.Clear();
        }

        RefreshSpeciesSelectionVisuals();
        RefreshActionChoiceVisuals();
        RefreshPlayerEnergyCounter();
    }

    /// <summary>Writes the current species/action selection prompt to the combat log.</summary>
    public void AppendSelectionPrompt()
    {
        AppendCombatLog(_selectedPlayerSpecies == null
            ? "Select a species, or end your turn."
            : $"Select an action ({_selectedPlayerSpecies.CurrentEnergy}/{Species.EnergyPerTurn} energy), or end your turn.");
    }

    public void SetStepControlsEnabled(bool enabled)
    {
        _stepControlsEnabled = enabled;

        if (!enabled)
            CancelActionGesture();

        foreach (var healthBar in _healthBars.Values)
            healthBar.SetActionButtonsEnabled(enabled);

        _endTurnBtn?.SetEnabled(enabled);
    }

    public void ClearActionSelection()
    {
        _actionManager?.Clear();
        CancelActionGesture();
        RefreshActionChoiceVisuals();
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
        foreach (var kv in _speciesChips)
        {
            Utility.UI.EnableClass(!kv.Key.IsAlive, kv.Value, "bp-species-chip--dead");

            if (_speciesChipHpLabels.TryGetValue(kv.Key, out var hp))
                hp.text = $"HP {Mathf.Max(0, kv.Key.CurrentHealth)}/{Mathf.Max(1, kv.Key.MaxHealth)}";

            if (_speciesChipSizeLabels.TryGetValue(kv.Key, out var size))
            {
                size.text = $"SIZE {kv.Key.Size}  STR {kv.Key.Strength}";
            }
        }

        foreach (var kv in _speciesVisuals)
            kv.Value.style.opacity = kv.Key.IsAlive ? 1f : 0.3f;

        RefreshDefenseIndicators();
        RefreshArenaSpeciesVisuals();
        RefreshSpeciesSelectionVisuals();
        RefreshActionChoiceVisuals();
        RefreshPlayerEnergyCounter();
    }

    void RefreshSpeciesSelectionVisuals()
    {
        foreach (var kv in _speciesChips)
        {
            bool isSelectable = _selectablePlayerSpecies.Contains(kv.Key);
            Utility.UI.EnableClass(isSelectable, kv.Value, "bp-species-chip--selectable");
            Utility.UI.EnableClass(kv.Key == _selectedPlayerSpecies, kv.Value, "bp-species-chip--selected");
        }
    }

    void RefreshPlayerEnergyCounter()
    {
        if (_energyValueLabel == null || _energySpeciesLabel == null)
        {
            return;
        }

        if (_selectedPlayerSpecies == null)
        {
            _energyValueLabel.text = "—";
            _energySpeciesLabel.text = "SELECT FISH";
            return;
        }

        _energyValueLabel.text = _selectedPlayerSpecies.CurrentEnergy.ToString();
        _energySpeciesLabel.text = _selectedPlayerSpecies.Name?.ToUpperInvariant() ?? "SELECTED FISH";
    }

    static int GetLivingTotalSize(IEnumerable<Species> species)
    {
        return species?.Where(s => s != null && s.IsAlive).Sum(s => s.Size) ?? 0;
    }

    // ── Species info tooltip ──────────────────────────────────────────────────

    /// <summary>
    /// Populates and shows the species info overlay with <paramref name="species"/> data.
    /// Uses the matching arena visual or species chip as the popup anchor when available.
    /// </summary>
    public void ShowTooltip(Species species)
    {
        if (species == null) return;

        VisualElement trigger = null;
        if (_speciesVisuals.TryGetValue(species, out var visual))
            trigger = visual;
        else if (_speciesChips.TryGetValue(species, out var chip))
            trigger = chip;

        ShowTooltip(species, trigger, _data?.EnemyGroup?.Contains(species) == true);
    }

    void ShowTooltip(Species species, VisualElement trigger, bool isEnemy)
    {
        if (species == null) return;

        _tooltipSpecies = species;
        _tooltipTrigger = trigger;

        _infoName.text = species.Name?.ToUpper() ?? "UNKNOWN";
        _infoHp.text = $"{species.CurrentHealth} / {species.MaxHealth}";
        _infoAtk.text = species.Attack.ToString();
        _infoDef.text = species.Defense.ToString();
        _infoForage.text = species.Forage.ToString();
        _infoSize.text = species.Size.ToString();
        _infoStrength.text = species.Strength.ToString();

        _infoBehaviors.Clear();
        foreach (var part in species.Parts)
            foreach (var behavior in part.Behaviors)
            {
                var tag = new Label(FormatBehaviorName(behavior));
                tag.AddToClassList("bp-behavior-tag");
                _infoBehaviors.Add(tag);
            }

        _speciesInfo.RemoveFromClassList("bp-hidden");
        _root.schedule.Execute(() =>
        {
            if (!_speciesInfo.ClassListContains("bp-hidden")
                && _tooltipSpecies == species
                && _tooltipTrigger == trigger)
                PositionSpeciesInfo(trigger, isEnemy);
        });
    }

    /// <summary>Hides the species info overlay.</summary>
    public void HideTooltip()
    {
        _tooltipSpecies = null;
        _tooltipTrigger = null;
        _speciesInfo.AddToClassList("bp-hidden");
    }

    void PositionSpeciesInfo(VisualElement trigger, bool isEnemy)
    {
        if (trigger == null || _speciesInfo == null || _root == null)
            return;

        Rect triggerWorldBounds = trigger.worldBound;
        Vector2 triggerTopLeft = _root.WorldToLocal(triggerWorldBounds.position);
        Vector2 triggerBottomRight = _root.WorldToLocal(
            new Vector2(triggerWorldBounds.xMax, triggerWorldBounds.yMax));
        Rect triggerBounds = Rect.MinMaxRect(
            triggerTopLeft.x,
            triggerTopLeft.y,
            triggerBottomRight.x,
            triggerBottomRight.y);

        float popupWidth = Mathf.Max(1f, _speciesInfo.resolvedStyle.width);
        float popupHeight = Mathf.Max(1f, _speciesInfo.resolvedStyle.height);
        float gap = 12f;
        float rootWidth = Mathf.Max(1f, _root.resolvedStyle.width);
        float rootHeight = Mathf.Max(1f, _root.resolvedStyle.height);

        float left = isEnemy
            ? triggerBounds.x - popupWidth - gap
            : triggerBounds.xMax + gap;
        float top = triggerBounds.center.y - popupHeight * 0.5f;

        left = Mathf.Clamp(left, 8f, Mathf.Max(8f, rootWidth - popupWidth - 8f));
        top = Mathf.Clamp(top, 8f, Mathf.Max(8f, rootHeight - popupHeight - 8f));

        _speciesInfo.style.left = left;
        _speciesInfo.style.right = new StyleLength(StyleKeyword.Auto);
        _speciesInfo.style.top = top;
    }

    // ── Arena species visuals and health bars ─────────────────────────────────

    void BuildSpeciesVisuals(BattleData data)
    {
        BuildSpeciesVisuals(data?.PlayerGroup, true);
        BuildSpeciesVisuals(data?.EnemyGroup, false);
    }

    void BuildSpeciesVisuals(IReadOnlyList<Species> speciesGroup, bool isPlayer)
    {
        if (speciesGroup == null) return;

        foreach (var species in speciesGroup)
        {
            if (species == null) continue;

            var image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Position,
            };
            image.AddToClassList("bp-species-visual");
            SetSpeciesPortrait(image, species);
            image.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(species, image, !isPlayer));
            image.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            if (isPlayer)
                image.RegisterCallback<ClickEvent>(_ => SelectPlayerSpecies(species));

            var blockIndicator = new VisualElement
            {
                name = "bp-defense-indicator",
                pickingMode = PickingMode.Ignore,
            };
            blockIndicator.AddToClassList("bp-defense-indicator");
            blockIndicator.style.display = DisplayStyle.None;

            var blockIcon = new Label("🛡")
            {
                pickingMode = PickingMode.Ignore,
            };
            blockIcon.AddToClassList("bp-defense-indicator__icon");
            blockIndicator.Add(blockIcon);

            var blockCount = new Label
            {
                name = "bp-defense-indicator-count",
                pickingMode = PickingMode.Ignore,
            };
            blockCount.AddToClassList("bp-defense-indicator__count");
            blockIndicator.Add(blockCount);
            image.Add(blockIndicator);
            _blockIndicators[species] = blockIndicator;
            _blockIndicatorCounts[species] = blockCount;

            _speciesVisualLayer.Add(image);
            _speciesVisuals[species] = image;
            AddHealthBar(species);
        }
    }

    void SetSpeciesPortrait(Image image, Species species)
    {
        if (species?.Portrait != null)
            image.sprite = species.Portrait;
        else
            image.sprite = GlobalConstantsHandler.Constants.defaultSpeciesPortrait;
    }

    void RefreshArenaSpeciesVisuals()
    {
        if (_arena == null || _speciesVisuals.Count == 0)
            return;

        float arenaWidth = _arena.resolvedStyle.width;
        float arenaHeight = _arena.resolvedStyle.height;
        if (arenaWidth <= 0f || arenaHeight <= 0f)
            return;

        LayoutSpeciesVisuals(_data?.PlayerGroup, true, arenaWidth, arenaHeight);
        LayoutSpeciesVisuals(_data?.EnemyGroup, false, arenaWidth, arenaHeight);
        RefreshTargetLine();
    }

    void LayoutSpeciesVisuals(IReadOnlyList<Species> speciesGroup, bool isPlayer, float arenaWidth, float arenaHeight)
    {
        if (speciesGroup == null) return;

        var livingSpecies = speciesGroup.Where(s => s != null).ToList();
        for (int index = 0; index < livingSpecies.Count; index++)
        {
            var species = livingSpecies[index];
            if (!_speciesVisuals.TryGetValue(species, out var image))
                continue;

            float width;
            float height;
            if (isPlayer)
            {
                var constants = GlobalConstantsHandler.Constants;
                float pixelsPerSize = constants?.PlayerSizePixelsPerUnit ?? 12f;
                float radius = Mathf.Max(0f, species.Size)
                    * pixelsPerSize
                    * (constants?.BodyRadiusMultiplier ?? 1f);
                width = Mathf.Max(1f, radius * 2f);
                float aspectRatio = _speciesVisualBaseSize.y / Mathf.Max(1f, _speciesVisualBaseSize.x);
                height = Mathf.Max(1f, width * aspectRatio);
            }
            else
            {
                float scale = GetSpeciesVisualScale(species);
                width = Mathf.Max(1f, _speciesVisualBaseSize.x * scale);
                height = Mathf.Max(1f, _speciesVisualBaseSize.y * scale);
            }

            float sideCenter = arenaWidth * (isPlayer ? 0.27f : 0.73f);
            float sideSpread = Mathf.Min(arenaWidth * 0.18f, 150f);
            float normalizedIndex = livingSpecies.Count == 1
                ? 0.5f
                : (float)index / (livingSpecies.Count - 1);
            float centerX = sideCenter + Mathf.Lerp(-sideSpread, sideSpread, normalizedIndex);
            float centerY = arenaHeight * 0.62f;
            float imageTop = Mathf.Clamp(centerY - height * 0.5f, 78f, Mathf.Max(78f, arenaHeight - height - 8f));

            image.style.width = width;
            image.style.height = height;
            image.style.left = centerX - width * 0.5f;
            image.style.top = imageTop;
            _speciesVisualCenters[species] = new Vector2(centerX, imageTop + height * 0.5f);

            if (_blockIndicators.TryGetValue(species, out var blockIndicator))
            {
                blockIndicator.style.left = isPlayer ? width + 8f : -66f;
                blockIndicator.style.top = (height - 28f) * 0.5f;
            }

            if (_healthBars.TryGetValue(species, out var bar))
            {
                float barWidth = 400f;
                float barHeight = Mathf.Max(40f, bar.resolvedStyle.height);
                float barTop = imageTop - barHeight - 8f;
                bar.style.left = centerX - barWidth * 0.5f;
                bar.style.top = barTop;
                bar.SetActionControlsPosition(
                    (barWidth - 260f) * 0.5f,
                    imageTop + height + 8f - barTop);
                bar.SetStatusEffectsPosition(width * 0.5f + barWidth * 0.5f + 10f, -height * 0.5f - 21f);
            }
        }
    }

    float GetSpeciesVisualScale(Species species)
    {
        float minimumSize = Mathf.Min(_speciesVisualMinimumSize, _speciesVisualMaximumSize);
        float maximumSize = Mathf.Max(minimumSize + 1f, _speciesVisualMaximumSize);
        float maximumScale = Mathf.Max(1f, _speciesVisualMaximumScale);
        return Mathf.Lerp(1f, maximumScale, Mathf.InverseLerp(minimumSize, maximumSize, species.Size));
    }

    void ClearSpeciesVisuals()
    {
        _speciesVisualLayer?.Clear();
        _speciesVisuals.Clear();
        _speciesVisualCenters.Clear();
        _blockIndicators.Clear();
        _blockIndicatorCounts.Clear();
    }

    public void RefreshDefenseIndicators()
    {
        foreach (var indicator in _blockIndicators)
        {
            indicator.Value.style.display = DisplayStyle.Flex;
            if (_blockIndicatorCounts.TryGetValue(indicator.Key, out var count))
            {
                count.text = indicator.Key.Block.ToString();
            }
        }
    }

    public IEnumerator PlayAttackAnimation(Species attacker, Species target)
    {
        if (attacker == null || target == null
            || !_speciesVisuals.TryGetValue(attacker, out var image)
            || !_speciesVisualCenters.TryGetValue(attacker, out var attackerCenter)
            || !_speciesVisualCenters.TryGetValue(target, out var targetCenter))
            yield break;

        Vector2 direction = targetCenter - attackerCenter;
        if (direction.sqrMagnitude <= 0.01f)
            yield break;

        float distance = direction.magnitude;
        Vector2 offset = direction / distance * Mathf.Min(distance * 0.45f, 140f);
        var animationHost = new GameObject("BattleAttackAnimation");
        animationHost.transform.SetParent(transform, false);

        try
        {
            yield return MoveImageWithTransform(image, attackerCenter, animationHost.transform, offset, 0.12f);
            yield return MoveImageWithTransform(image, attackerCenter, animationHost.transform, Vector2.zero, 0.16f);
        }
        finally
        {
            Destroy(animationHost);
        }
    }

    IEnumerator MoveImageWithTransform(Image image, Vector2 imageCenter, Transform animationTransform, Vector2 offset, float duration)
    {
        var interpolation = Utility.InterpolatePosition(
            animationTransform,
            new Vector3(offset.x, offset.y, 0f),
            duration,
            localPosition: true);

        while (interpolation.MoveNext())
        {
            Vector3 movement = animationTransform.localPosition;
            image.style.left = imageCenter.x - image.resolvedStyle.width * 0.5f + movement.x;
            image.style.top = imageCenter.y - image.resolvedStyle.height * 0.5f + movement.y;
            yield return interpolation.Current;
        }

        image.style.left = imageCenter.x - image.resolvedStyle.width * 0.5f + offset.x;
        image.style.top = imageCenter.y - image.resolvedStyle.height * 0.5f + offset.y;
    }

    // ── Health bars ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a floating health bar for <paramref name="species"/> and adds it
    /// to the arena's health-bar layer.
    /// Position defaults to (0,0); call <see cref="SetHealthBarPosition"/> to
    /// anchor it below the species' sprite.
    /// </summary>
    public SpeciesHealthBarElement AddHealthBar(Species species)
    {
        if (_healthBars.TryGetValue(species, out var existing))
            return existing;

        var bar = new SpeciesHealthBarElement(species);
        bool isEnemySpecies = _data?.EnemyGroup?.Contains(species) == true;
        bar.OnActionHovered += ShowActionTooltip;
        bar.OnActionHoverEnded += HideActionTooltip;

        if (!isEnemySpecies)
        {
            bar.OnActionSelected += (sourcePart, actionButton, pointerPosition, pointerId) =>
                HandleSpeciesActionSelected(species, sourcePart, actionButton, pointerPosition, pointerId);
            bar.OnActionPointerMoved += HandleActionPointerMoved;
            bar.OnActionPointerReleased += HandleActionPointerReleased;
            bar.OnActionPointerCancelled += HandleActionPointerCancelled;
        }

        bar.SetActionControlsVisible(!isEnemySpecies && species == _selectedPlayerSpecies);
        bar.SetIntentVisible(isEnemySpecies);
        bar.SetEnergyVisible(!isEnemySpecies);
        if (isEnemySpecies && species == _enemyIntentSpecies)
            bar.SetIntentAction(_enemyIntentAction, _enemyIntentTarget, _enemyIntentPart);

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
        if (_damageNumberPrefab == null || target == null || damage <= 0 || _root?.panel == null)
            return;

        VisualElement targetVisual = null;
        if (_speciesVisuals.TryGetValue(target, out var speciesVisual))
            targetVisual = speciesVisual;
        else if (_speciesChips.TryGetValue(target, out var chip))
            targetVisual = chip;
        else if (_healthBars.TryGetValue(target, out var bar))
            targetVisual = bar;

        if (targetVisual == null)
            return;

        var bounds = targetVisual.worldBound;
        Vector2 panelPosition = new(bounds.center.x, bounds.yMin);
        Vector2 screenPosition = PanelPositionToScreen(panelPosition);
        screenPosition.x += UnityEngine.Random.Range(-_damagePopupScreenJitter.x, _damagePopupScreenJitter.x);
        screenPosition.y += UnityEngine.Random.Range(-_damagePopupScreenJitter.y, _damagePopupScreenJitter.y);

        if (_damageNumberCanvas == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _damageNumberCanvas,
            screenPosition,
                null,
                out var anchoredPosition))
        {
            return;
        }

        _damageNumberPrefab.SpawnGUI(_damageNumberCanvas, anchoredPosition, damage);
    }

    Vector2 PanelPositionToScreen(Vector2 panelPosition)
    {
        Rect panelBounds = _root.worldBound;
        if (panelBounds.width <= 0f || panelBounds.height <= 0f)
        {
            return Vector2.zero;
        }

        float normalizedX = (panelPosition.x - panelBounds.xMin) / panelBounds.width;
        float normalizedY = (panelPosition.y - panelBounds.yMin) / panelBounds.height;
        return new Vector2(normalizedX * Screen.width, (1f - normalizedY) * Screen.height);
    }

    void SubscribeToDamageEvents()
    {
        foreach (var species in _speciesVisuals.Keys)
        {
            if (species == null)
            {
                continue;
            }

            species.DamageTaken += OnSpeciesDamageTaken;
            _damageEventSpecies.Add(species);
        }
    }

    void ClearDamageEventSubscriptions()
    {
        foreach (var species in _damageEventSpecies)
        {
            if (species != null)
            {
                species.DamageTaken -= OnSpeciesDamageTaken;
            }
        }

        _damageEventSpecies.Clear();
    }

    void OnSpeciesDamageTaken(Species species, int damage)
    {
        ShowDamageNumber(species, damage);
    }

    void ClearHealthBars()
    {
        _healthBarLayer?.Clear();
        _healthBars.Clear();
    }

    void BuildSpeciesStrips(BattleData data)
    {
        _speciesChips.Clear();
        _speciesChipHpLabels.Clear();
        _speciesChipSizeLabels.Clear();
        _playerSpeciesStrip?.Clear();
        _enemySpeciesStrip?.Clear();

        if (data?.PlayerGroup != null)
        {
            foreach (var species in data.PlayerGroup)
                _playerSpeciesStrip?.Add(BuildSpeciesChip(species, true));
        }

        if (data?.EnemyGroup != null)
        {
            foreach (var species in data.EnemyGroup)
                _enemySpeciesStrip?.Add(BuildSpeciesChip(species, false));
        }
    }

    VisualElement BuildSpeciesChip(Species species, bool isPlayer)
    {
        var chip = new VisualElement();
        chip.AddToClassList("bp-species-chip");

        var portrait = new Image();
        portrait.AddToClassList("bp-species-chip__portrait");

        SetSpeciesPortrait(portrait, species);

        chip.Add(portrait);

        var name = new Label(species?.Name?.ToUpper() ?? "SPECIES");
        name.AddToClassList("bp-species-chip__name");
        chip.Add(name);

        var hp = new Label(species == null ? "HP --" : $"HP {species.CurrentHealth}/{Mathf.Max(1, species.MaxHealth)}");
        hp.AddToClassList("bp-species-chip__hp");
        chip.Add(hp);

        var size = new Label(species == null
            ? "SIZE --"
            : $"SIZE {species.Size}  STR {species.Strength}");
        size.AddToClassList("bp-species-chip__size");
        chip.Add(size);

        if (species != null)
        {
            _speciesChips[species] = chip;
            _speciesChipHpLabels[species] = hp;
            _speciesChipSizeLabels[species] = size;
            chip.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(species, chip, !isPlayer));
            chip.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());

            if (isPlayer)
            {
                chip.AddToClassList("bp-species-chip--selectable");
                chip.RegisterCallback<ClickEvent>(_ => SelectPlayerSpecies(species));
            }
        }

        return chip;
    }

    void EmitStepRequest()
    {
        if (!_stepControlsEnabled)
        {
            return;
        }

        if (_actionManager == null || _actionManager.Actions.Count == 0)
        {
            AppendCombatLog("Select an action first.");
            return;
        }

        var selected = _actionManager.Actions[0];
        if (selected.Actor == null || !selected.Actor.IsAlive
            || !selected.Actor.CanUseAction(selected.SourcePart, selected.Type))
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

    void HandleSpeciesActionSelected(
        Species species,
        Part sourcePart,
        Button actionButton,
        Vector2 pointerPosition,
        int pointerId)
    {
        if (_isActionPointerActive || !_stepControlsEnabled || species == null || actionButton == null
            || species != _selectedPlayerSpecies || !_selectablePlayerSpecies.Contains(species)
            || !species.IsAlive || _actionManager == null)
        {
            return;
        }

        var actionType = sourcePart == null ? SpeciesActionType.Attack : sourcePart.ActionType;
        if (!species.CanUseAction(sourcePart, actionType))
            return;

        CancelActionGesture();
        HideActionTooltip();
        HideTooltip();
        _actionManager.Clear();
        _isActionPointerActive = true;
        _actionPointerId = pointerId;
        _actionPointerElement = actionButton;
        _pendingActionActor = species;
        _pendingActionSourcePart = sourcePart;
        _pendingActionType = actionType;
        _actionPointerElement.CapturePointer(pointerId);
        if (_healthBars.TryGetValue(species, out var bar))
        {
            bar.SetSelectedAction(sourcePart, actionType);
        }

        BeginTargetSelection(species, sourcePart, actionType, actionButton, pointerPosition);
    }

    void SelectPlayerSpecies(Species species)
    {
        if (!_stepControlsEnabled || species == null || !species.IsAlive || !_selectablePlayerSpecies.Contains(species))
            return;

        if (_selectedPlayerSpecies == species)
            return;

        CancelActionGesture();
        _selectedPlayerSpecies = species;
        _actionManager?.Clear();
        RefreshSpeciesSelectionVisuals();
        RefreshActionChoiceVisuals();
        RefreshPlayerEnergyCounter();
        AppendCombatLog("Select an action");
    }

    IReadOnlyList<Part> GetAvailableActions(Species species)
    {
        if (species == null || !species.IsAlive)
            return Array.Empty<Part>();

        return SpeciesHealthBarElement.GetAvailableActions(species);
    }

    void RefreshActionChoiceVisuals()
    {
        HideActionTooltip();
        if (_isActionPointerActive)
        {
            // Do not detach or disable the element holding pointer capture mid-drag.
            if (_stepControlsEnabled && _pendingActionActor != null
                && _pendingActionActor.IsAlive
                && _selectablePlayerSpecies.Contains(_pendingActionActor)
                && _pendingActionActor.CanUseAction(_pendingActionSourcePart, _pendingActionType))
            {
                return;
            }

            CancelActionGesture();
        }

        var playerSpecies = _data?.PlayerGroup;
        if (playerSpecies != null)
        {
            foreach (var species in playerSpecies)
            {
                if (species == null || !_healthBars.TryGetValue(species, out var bar))
                    continue;

                bar.SetActionControlsVisible(species == _selectedPlayerSpecies);
                bar.ConfigureActionButtons(GetAvailableActions(species), GetAttackPreviewTarget(species), species == _selectedPlayerSpecies);
                bar.SetActionButtonsEnabled(_stepControlsEnabled && !IsActionSelectionLocked(species));

                if (!species.IsAlive)
                    _actionManager?.RemoveActionForActor(species);

                if (_actionManager != null && _actionManager.TryGetActionForActor(species, out var selected))
                    bar.SetSelectedAction(selected.SourcePart, selected.Type);
                else if (_targetingActor == species)
                    bar.SetSelectedAction(_targetingSourcePart, _targetingActionType);
                else
                    bar.SetSelectedAction(null, SpeciesActionType.None);
            }
        }

    }

    Species GetAttackPreviewTarget(Species species)
    {
        if (_targetingActor == species && _targetingTarget != null)
            return _targetingTarget;

        if (_actionManager != null
            && _actionManager.TryGetActionForActor(species, out var selected)
            && selected.Targets != null)
            return selected.Targets.FirstOrDefault(target => target != null && target.IsAlive);

        var enemies = _data?.EnemyGroup?.Where(enemy => enemy != null && enemy.IsAlive);
        return enemies == null ? null : species.PickTarget(enemies);
    }

    bool IsActionSelectionLocked(Species species)
    {
        return species != null
            && (_targetingActor == species
                || (_actionManager != null && _actionManager.TryGetActionForActor(species, out _)));
    }

    void BeginTargetSelection(
        Species actor,
        Part sourcePart,
        SpeciesActionType actionType,
        Button actionButton,
        Vector2 pointerPosition)
    {
        _targetingActor = actor;
        _targetingSourcePart = sourcePart;
        _targetingActionType = actionType;
        Vector2 actionPosition = actionButton != null ? actionButton.worldBound.center : pointerPosition;
        _targetingStartPosition = _arena.WorldToLocal(actionPosition);
        _targetingPointerPosition = _arena.WorldToLocal(pointerPosition);
        _targetLine.BringToFront();
        RefreshTargetingVisuals();
        RefreshTargetLine();
    }

    void HandleActionPointerReleased(Button actionButton, Vector2 releasePosition, int pointerId)
    {
        if (!_isActionPointerActive || actionButton != _actionPointerElement || pointerId != _actionPointerId)
            return;

        Species actor = _pendingActionActor;
        Part sourcePart = _pendingActionSourcePart;
        SpeciesActionType actionType = _pendingActionType;
        Species target = FindActionTargetAt(releasePosition);
        CancelActionGesture();

        if (!_stepControlsEnabled || actor == null || !actor.IsAlive
            || !_selectablePlayerSpecies.Contains(actor)
            || !actor.CanUseAction(sourcePart, actionType)
            || _data?.IsValidActionTarget(actor, actionType, target) != true)
        {
            RefreshActionChoiceVisuals();
            return;
        }

        _actionManager.SetAction(new SpeciesAction
        {
            Actor = actor,
            Type = actionType,
            SourcePart = sourcePart,
            Targets = new List<Species> { target },
        });
        RefreshActionChoiceVisuals();
        EmitStepRequest();
    }

    void HandleActionPointerCancelled(Button actionButton, int pointerId)
    {
        if (!_isActionPointerActive || actionButton != _actionPointerElement || pointerId != _actionPointerId)
            return;

        CancelActionGesture();
        RefreshActionChoiceVisuals();
    }

    void HandleActionPointerMoved(Button actionButton, Vector2 pointerPosition, int pointerId)
    {
        if (!_isActionPointerActive || actionButton != _actionPointerElement
            || pointerId != _actionPointerId || _arena == null)
            return;

        _targetingPointerPosition = _arena.WorldToLocal(pointerPosition);
        _targetingTarget = FindActionTargetAt(pointerPosition);
        RefreshTargetingVisuals();
        RefreshTargetLine();
    }

    Species FindActionTargetAt(Vector2 worldPosition)
    {
        foreach (var kv in _speciesVisuals.Reverse())
        {
            if (_data?.IsValidActionTarget(_targetingActor, _targetingActionType, kv.Key) != true)
            {
                continue;
            }

            if (kv.Value.worldBound.Contains(worldPosition)
                || (_speciesChips.TryGetValue(kv.Key, out var chip) && chip.worldBound.Contains(worldPosition)))
            {
                return kv.Key;
            }
        }

        return null;
    }

    void RefreshTargetingVisuals()
    {
        foreach (var kv in _speciesVisuals)
        {
            bool targetable = _isActionPointerActive
                && _data?.IsValidActionTarget(_targetingActor, _targetingActionType, kv.Key) == true;
            bool targeted = _targetingTarget == kv.Key;
            Utility.UI.EnableClass(targetable, kv.Value, "bp-species-visual--targetable");
            Utility.UI.EnableClass(targeted, kv.Value, "bp-species-visual--targeted");
        }
    }

    void RefreshTargetLine()
    {
        if (_targetLine == null || _targetingActor == null)
        {
            HideTargetLine();
            return;
        }

        Vector2 endPosition = _targetingPointerPosition;
        if (_isActionPointerActive && _actionPointerElement != null)
        {
            _targetingStartPosition = _arena.WorldToLocal(_actionPointerElement.worldBound.center);
        }

        Vector2 direction = endPosition - _targetingStartPosition;
        float length = direction.magnitude;
        if (length <= 1f)
        {
            HideTargetLine();
            return;
        }

        Vector2 midpoint = (_targetingStartPosition + endPosition) * 0.5f;
        _targetLine.style.display = DisplayStyle.Flex;
        _targetLine.style.left = midpoint.x - length * 0.5f;
        _targetLine.style.top = midpoint.y - 2f;
        _targetLine.style.width = length;
        _targetLine.style.rotate = new Rotate(Angle.Degrees(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
    }

    void HideTargetLine()
    {
        if (_targetLine != null)
            _targetLine.style.display = DisplayStyle.None;
    }

    void ClearTargetSelection()
    {
        _targetingActor = null;
        _targetingTarget = null;
        _targetingSourcePart = null;
        _targetingActionType = SpeciesActionType.None;
        _targetingStartPosition = Vector2.zero;
        RefreshTargetingVisuals();
        HideTargetLine();
    }

    void ReleaseActionPointer()
    {
        VisualElement pointerElement = _actionPointerElement;
        int pointerId = _actionPointerId;
        _isActionPointerActive = false;
        _actionPointerId = -1;
        _actionPointerElement = null;
        _pendingActionActor = null;
        _pendingActionSourcePart = null;
        _pendingActionType = SpeciesActionType.None;

        // Clear state before release: PointerCaptureOutEvent also invokes cancellation.
        if (pointerElement != null && pointerElement.HasPointerCapture(pointerId))
        {
            pointerElement.ReleasePointer(pointerId);
        }
    }

    void CancelActionGesture()
    {
        ReleaseActionPointer();
        ClearTargetSelection();
    }

    static BattleStepAction ToBattleStepAction(SpeciesActionType action)
    {
        return action switch
        {
            SpeciesActionType.Forage => BattleStepAction.Forage,
            SpeciesActionType.Defend => BattleStepAction.Defend,
            SpeciesActionType.Blind => BattleStepAction.Blind,
            _ => BattleStepAction.Attack,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static string FormatBehaviorName(PartBehaviorBase b) =>
        b.GetType().Name.Replace("Behavior", string.Empty);

}
