using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DamageNumbersPro;
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
///   • <b>Begin button</b> — centred in the arena; hidden once combat starts.
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

    /// <summary>Fired when the player confirms a battle step request.</summary>
    public event Action<BattleStepRequest> OnBeginClicked;

    // ── Private state ─────────────────────────────────────────────────────────

    UIDocument _doc;
    VisualElement _root;

    // Arena
    VisualElement _arena;
    VisualElement _healthBarLayer;
    VisualElement _speciesVisualLayer;
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
    Label _playerTotalSizeLabel;
    Label _enemyTotalSizeLabel;
    Label _sizeLeadLabel;
    ScrollView _logScroll;
    VisualElement _logList;

    BattleData _data;
    Species _selectedPlayerSpecies;
    readonly List<Species> _selectablePlayerSpecies = new();
    readonly Dictionary<Species, VisualElement> _speciesChips = new();
    readonly Dictionary<Species, Label> _speciesChipHpLabels = new();
    readonly Dictionary<Species, Label> _speciesChipSizeLabels = new();
    ActionManager _actionManager;
    Species _enemyIntentSpecies;
    SpeciesActionType? _enemyIntentAction;
    bool _stepControlsEnabled = true;

    [Header("Damage Numbers Pro")]
    [SerializeField] DamageNumber _damageNumberPrefab;
    [SerializeField] float _damagePopupDistanceFromCamera = 10f;
    [SerializeField] Vector2 _damagePopupScreenJitter = new(18f, 10f);

    [Header("Battle Fish Visuals")]
    [Tooltip("Optional shared sprite used when a species has no portrait assigned.")]
    [SerializeField] Sprite _defaultSpeciesSprite;
    [SerializeField] Vector2 _speciesVisualBaseSize = new(112f, 76f);
    [Tooltip("Species size rendered at the smallest visual scale.")]
    [SerializeField] float _speciesVisualMinimumSize = 1f;
    [Tooltip("Species size rendered at the largest visual scale.")]
    [SerializeField] float _speciesVisualMaximumSize = 12f;
    [SerializeField] float _speciesVisualMaximumScale = 4f;

    const int DefaultMaxActions = 1;

    // Health bar registry
    readonly Dictionary<Species, SpeciesHealthBarElement> _healthBars = new();
    readonly Dictionary<Species, Image> _speciesVisuals = new();
    readonly Dictionary<Species, Vector2> _speciesVisualCenters = new();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        _arena = _root.Q("bp-arena");
        _healthBarLayer = _root.Q("bp-health-bar-layer");
        _speciesVisualLayer = _root.Q("bp-species-visual-layer");
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
        _playerTotalSizeLabel = _root.Q<Label>("bp-player-total-size");
        _enemyTotalSizeLabel = _root.Q<Label>("bp-enemy-total-size");
        _sizeLeadLabel = _root.Q<Label>("bp-size-lead");
        _logScroll = _root.Q<ScrollView>("bp-log-scroll");
        _logList = _root.Q("bp-log-list");

        _beginBtn.clicked += EmitStepRequest;
        _arena.RegisterCallback<GeometryChangedEvent>(OnArenaGeometryChanged);

        if (_damageNumberPrefab == null)
            Debug.LogWarning("[BattlePanel] Damage Number Prefab is not assigned; attack damage popups are disabled.", this);

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
        _selectedPlayerSpecies = null;
        _selectablePlayerSpecies.Clear();
        _enemyIntentSpecies = null;
        _enemyIntentAction = null;
        _stepControlsEnabled = true;

        ClearHealthBars();
        ClearSpeciesVisuals();
        BuildSpeciesStrips(data);
        BuildSpeciesVisuals(data);
        SetRoundAndTurn(1, true);
        SetSelectableSpecies(data.PlayerGroup);
        ClearCombatLog();
        RefreshSpeciesVisuals();
        _beginBtn.text = "CONFIRM";

        _root.style.display = DisplayStyle.Flex;
        ShowBeginButton();
        _root.schedule.Execute(RefreshSpeciesVisuals);
    }

    /// <summary>Hides the entire panel without clearing state.</summary>
    public void Hide() => _root.style.display = DisplayStyle.None;

    void OnArenaGeometryChanged(GeometryChangedEvent _)
    {
        RefreshArenaSpeciesVisuals();
    }

    // ── Begin button ──────────────────────────────────────────────────────────

    public void ShowBeginButton() => _beginBtn.style.display = DisplayStyle.Flex;
    public void HideBeginButton() => _beginBtn.style.display = DisplayStyle.None;
    const int MaxBattleRounds = 10;

    public void SetRoundAndTurn(int round, bool isPlayerTurn)
    {
        if (_roundLabel == null) return;
        _roundLabel.text = $"ROUND {round} / {MaxBattleRounds} - {(isPlayerTurn ? "PLAYER TURN" : "ENEMY TURN")}";
    }

    public void SetEnemyIntent(Species species, SpeciesActionType? action)
    {
        _enemyIntentSpecies = species;
        _enemyIntentAction = action;

        foreach (var kv in _healthBars)
        {
            bool isIntentSpecies = kv.Key == _enemyIntentSpecies;
            kv.Value.SetIntentAction(isIntentSpecies ? _enemyIntentAction : null);
        }
    }

    public void SetSelectableSpecies(IReadOnlyList<Species> species)
    {
        _selectablePlayerSpecies.Clear();
        if (species != null)
            _selectablePlayerSpecies.AddRange(species.Where(s => s != null && s.IsAlive));

        if (!_selectablePlayerSpecies.Contains(_selectedPlayerSpecies))
        {
            _selectedPlayerSpecies = _selectablePlayerSpecies.Count == 1
                ? _selectablePlayerSpecies[0]
                : null;
        }

        if (_actionManager != null && _actionManager.Actions.Count > 0)
        {
            var selectedAction = _actionManager.Actions[0];
            bool actorIsSelectable = _selectablePlayerSpecies.Contains(selectedAction.Actor);
            bool actionIsAvailable = actorIsSelectable
                && selectedAction.SourcePart != null
                && selectedAction.Actor.CanUseAction(selectedAction.SourcePart, selectedAction.Type);

            if (!actionIsAvailable)
                _actionManager.Clear();
        }

        RefreshSpeciesSelectionVisuals();
        RefreshActionChoiceVisuals();
    }

    /// <summary>Writes the current species/action selection prompt to the combat log.</summary>
    public void AppendSelectionPrompt()
    {
        AppendCombatLog(_selectedPlayerSpecies == null ? "Select a species" : "Select an action");
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
        foreach (var kv in _speciesChips)
        {
            Utility.UI.EnableClass(!kv.Key.IsAlive, kv.Value, "bp-species-chip--dead");

            if (_speciesChipHpLabels.TryGetValue(kv.Key, out var hp))
                hp.text = $"HP {Mathf.Max(0, kv.Key.CurrentHealth)}/{Mathf.Max(1, kv.Key.MaxHealth)}";

            if (_speciesChipSizeLabels.TryGetValue(kv.Key, out var size))
                size.text = $"SIZE {kv.Key.Size}";
        }

        foreach (var kv in _speciesVisuals)
            kv.Value.style.opacity = kv.Key.IsAlive ? 1f : 0.3f;

        RefreshArenaSpeciesVisuals();
        RefreshSpeciesSelectionVisuals();
        RefreshSizeScoreboard();
        RefreshActionChoiceVisuals();
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

    void RefreshSizeScoreboard()
    {
        int playerTotal = GetLivingTotalSize(_data?.PlayerGroup);
        int enemyTotal = GetLivingTotalSize(_data?.EnemyGroup);

        if (_playerTotalSizeLabel != null)
            _playerTotalSizeLabel.text = playerTotal.ToString();
        if (_enemyTotalSizeLabel != null)
            _enemyTotalSizeLabel.text = enemyTotal.ToString();
        if (_sizeLeadLabel == null)
            return;

        _sizeLeadLabel.RemoveFromClassList("bp-size-lead--player");
        _sizeLeadLabel.RemoveFromClassList("bp-size-lead--enemy");
        _sizeLeadLabel.RemoveFromClassList("bp-size-lead--tie");

        if (playerTotal > enemyTotal)
        {
            _sizeLeadLabel.text = $"YOU +{playerTotal - enemyTotal}";
            _sizeLeadLabel.AddToClassList("bp-size-lead--player");
        }
        else if (enemyTotal > playerTotal)
        {
            _sizeLeadLabel.text = $"ENEMY +{enemyTotal - playerTotal}";
            _sizeLeadLabel.AddToClassList("bp-size-lead--enemy");
        }
        else
        {
            _sizeLeadLabel.text = "TIED";
            _sizeLeadLabel.AddToClassList("bp-size-lead--tie");
        }
    }

    static int GetLivingTotalSize(IEnumerable<Species> species)
    {
        return species?.Where(s => s != null && s.IsAlive).Sum(s => s.Size) ?? 0;
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
            image.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(species));
            image.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            if (isPlayer)
                image.RegisterCallback<ClickEvent>(_ => SelectPlayerSpecies(species));

            _speciesVisualLayer.Add(image);
            _speciesVisuals[species] = image;
            AddHealthBar(species);
        }
    }

    void SetSpeciesPortrait(Image image, Species species)
    {
        if (species?.Portrait != null)
            image.sprite = species.Portrait;
        else if (_defaultSpeciesSprite != null)
            image.sprite = _defaultSpeciesSprite;
        else
            image.image = GetFallbackPortrait(species?.Name);
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

            float scale = GetSpeciesVisualScale(species);
            float width = Mathf.Max(1f, _speciesVisualBaseSize.x * scale);
            float height = Mathf.Max(1f, _speciesVisualBaseSize.y * scale);
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

            if (_healthBars.TryGetValue(species, out var bar))
            {
                float barWidth = 120f;
                bar.style.left = centerX - barWidth * 0.5f;
                bar.style.top = imageTop + height + 8f;
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

        if (!isEnemySpecies)
            bar.OnActionSelected += sourcePart => HandleSpeciesActionSelected(species, sourcePart);

        bar.SetActionControlsVisible(!isEnemySpecies && species == _selectedPlayerSpecies);
        bar.SetIntentVisible(isEnemySpecies);
        if (isEnemySpecies && species == _enemyIntentSpecies)
            bar.SetIntentAction(_enemyIntentAction);

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

        VisualElement targetVisual = null;
        if (_healthBars.TryGetValue(target, out var bar))
            targetVisual = bar;
        else if (_speciesChips.TryGetValue(target, out var chip))
            targetVisual = chip;

        if (targetVisual == null)
            return;

        var bounds = targetVisual.worldBound;
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

        var size = new Label(species == null ? "SIZE --" : $"SIZE {species.Size}");
        size.AddToClassList("bp-species-chip__size");
        chip.Add(size);

        if (species != null)
        {
            _speciesChips[species] = chip;
            _speciesChipHpLabels[species] = hp;
            _speciesChipSizeLabels[species] = size;

            if (isPlayer)
            {
                chip.AddToClassList("bp-species-chip--selectable");
                chip.RegisterCallback<ClickEvent>(_ => SelectPlayerSpecies(species));
            }
        }

        return chip;
    }

    Texture2D GetFallbackPortrait(string seed)
    {
        seed ??= string.Empty;
        var texture = new Texture2D(32, 20, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
        };

        Color c = Color.HSVToRGB(Mathf.Abs(seed.GetHashCode() % 97) / 97f, 0.55f, 0.65f);
        var transparent = new Color(0f, 0f, 0f, 0f);
        var pixels = Enumerable.Repeat(transparent, 32 * 20).ToArray();
        for (int y = 0; y < 20; y++)
            for (int x = 0; x < 32; x++)
            {
                float bodyX = (x - 17f) / 12f;
                float bodyY = (y - 10f) / 7f;
                bool body = bodyX * bodyX + bodyY * bodyY <= 1f;
                bool tail = x <= 9 && Mathf.Abs(y - 10f) <= (9f - x) * 0.65f;
                bool eye = x >= 23 && x <= 24 && y >= 7 && y <= 8;
                if (body || tail || eye)
                    pixels[y * 32 + x] = eye ? Color.white : c;
            }
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

    void HandleSpeciesActionSelected(Species species, Part sourcePart)
    {
        if (!_stepControlsEnabled)
            return;

        if (species == null || species != _selectedPlayerSpecies
            || !species.IsAlive || _actionManager == null)
            return;

        var actionType = sourcePart == null ? SpeciesActionType.Attack : sourcePart.ActionType;
        if (sourcePart != null && !species.CanUseAction(sourcePart, actionType))
            return;

        List<Species> targets = null;
        if (actionType == SpeciesActionType.Attack || actionType == SpeciesActionType.Blind)
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
            SourcePart = sourcePart,
            Targets = targets,
        });

        RefreshActionChoiceVisuals();
    }

    void SelectPlayerSpecies(Species species)
    {
        if (!_stepControlsEnabled || species == null || !species.IsAlive || !_selectablePlayerSpecies.Contains(species))
            return;

        if (_selectedPlayerSpecies == species)
            return;

        _selectedPlayerSpecies = species;
        _actionManager?.Clear();
        RefreshSpeciesSelectionVisuals();
        RefreshActionChoiceVisuals();
        AppendCombatLog("Select an action");
    }

    IReadOnlyList<Part> GetAvailableActions(Species species)
    {
        if (species == null || !species.IsAlive)
            return Array.Empty<Part>();

        var actions = new List<Part> { null };
        actions.AddRange(species.GetActionParts());
        return actions;
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

                bar.SetActionControlsVisible(species == _selectedPlayerSpecies);
                bar.ConfigureActionButtons(GetAvailableActions(species));
                bar.SetActionButtonsEnabled(_stepControlsEnabled);

                if (!species.IsAlive)
                    _actionManager?.RemoveActionForActor(species);

                if (_actionManager != null && _actionManager.TryGetActionForActor(species, out var selected))
                    bar.SetSelectedAction(selected.SourcePart, selected.Type);
                else
                    bar.SetSelectedAction(null, SpeciesActionType.None);
            }
        }

    }

    static string ActionLabel(Part part)
    {
        if (part == null)
            return "ATTACK";
        if (!string.IsNullOrWhiteSpace(part.ActionName))
            return part.ActionName.ToUpperInvariant();
        return ActionLabel(part.ActionType);
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
        readonly VisualElement _statusEffectsRow;
        readonly VisualElement _actionButtonsRow;
        readonly Label _actionIcon;
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

        public void ConfigureActionButtons(IReadOnlyList<Part> actions)
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

                if (localPart.ActionIcon != null)
                {
                    var icon = new Image
                    {
                        sprite = localPart.ActionIcon,
                        scaleMode = ScaleMode.ScaleToFit,
                    };
                    icon.AddToClassList("bp-health-bar__action-btn-icon");
                    button.Add(icon);
                }
                else
                {
                    var iconFallback = new Label(ActionGlyph(actionType));
                    iconFallback.AddToClassList("bp-health-bar__action-btn-icon");
                    button.Add(iconFallback);
                }

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

        public void SetIntentAction(SpeciesActionType? action)
        {
            if (!_intentVisible)
                return;

            _actionButtonsRow.style.display = DisplayStyle.None;

            if (!action.HasValue)
            {
                _actionIcon.RemoveFromClassList("bp-health-bar__action-icon--enemy");
                _actionIcon.tooltip = string.Empty;
                _actionIcon.style.display = DisplayStyle.None;
                return;
            }

            _actionIcon.text = ActionLabel(action.Value);
            _actionIcon.tooltip = $"Enemy intends to {ActionLabel(action.Value).ToLowerInvariant()}.";
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
                SpeciesActionType.Forage => "FOR",
                SpeciesActionType.Defend => "DEF",
                SpeciesActionType.Blind => "BLD",
                _ => "ATK",
            };
        }

        static string ActionLabel(Part part) => BattlePanel.ActionLabel(part);
    }

}
