using System;
using System.Collections.Generic;
using System.Linq;
using Runtime.Game;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// MonoBehaviour that drives the Species Editor UI panel.
/// Attach to a GameObject that also has a UIDocument component.
/// Assign a GameState reference (or let it create a default one).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class SpeciesEditorPanel : MonoBehaviour
{
    const float PartSpriteVisualScale = 1.1f;

    // ── Inspector ─────────────────────────────────────────────
    [Tooltip("Leave null to auto-create a default GameState on Awake.")]
    public GameState GameState;

    // ── Private references ────────────────────────────────────
    UIDocument _doc;
    VisualElement _root;

    // Library
    VisualElement _libraryPanel;
    VisualElement _libraryGrid;
    Image _playerPortrait;
    VisualElement _speciesRadius;
    VisualElement _partLayer;
    VisualElement _connectionLineLayer;
    TextField _searchField;
    string _activeFilter = "All";

    readonly Dictionary<Part, VisualElement> _equippedPartElements = new();

    // Anchored part detail popup
    VisualElement _detailPanel;
    VisualElement _detailPointer;
    VisualElement _detailAnchor;
    VisualElement _detailContent;
    Label _detailName;
    Label _detailRarity;
    Label _detailTypeTag;
    Label _detailDescription;
    VisualElement _detailStats;

    // Encounter-style skill preview (no combat actions are executed here)
    VisualElement _playerSkillButtons;
    VisualElement _skillTooltipContainer;
    VisualElement _skillTooltip;
    Label _skillTooltipTitle;
    Label _skillTooltipDescription;
    VisualElement _skillTooltipAnchor;

    // Stats panel
    Label _mutationPointsValue;
    Label _statSizeTotal;
    Label _statHealthTotal;
    Label _statAttackTotal;
    Label _statDefenseTotal;
    Label _statForageTotal;
    Button _beginButton;

    // Selection state
    Part _selectedPart;
    PartCardElement _selectedCard;
    Part _hoveredPart;
    VisualElement _hoveredPartElement;
    bool saveDirty = false;

    // All library card elements (kept for refresh/filter)
    readonly List<PartCardElement> _libraryCards = new();

    // Pointer drag state
    enum DragSource
    {
        Library,
        EquippedPart,
    }

    bool _isDraggingPart;
    bool _dragMoved;
    bool _suppressNextClick;
    int _dragPointerId = -1;
    Vector2 _dragStartPosition;
    Vector2 _dragPointerOffset;
    Part _draggedPart;
    DragSource _dragSource;
    VisualElement _dragSourceElement;
    VisualElement _placementPreview;
    VisualElement _activeDropTarget;

    // ── Unity lifecycle ───────────────────────────────────────

    void Awake()
    {
        GameState ??= new GameState();
    }

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        QueryElements();
        BindFilterButtons();
        BindBeginButton();
        _searchField.RegisterValueChangedCallback(_ => RefreshLibraryGrid());

        _root.UnregisterCallback<PointerMoveEvent>(OnPartDragMove);
        _root.UnregisterCallback<PointerUpEvent>(OnPartDragEnd);
        _root.UnregisterCallback<PointerCancelEvent>(OnPartDragCancel);
        _root.RegisterCallback<PointerMoveEvent>(OnPartDragMove);
        _root.RegisterCallback<PointerUpEvent>(OnPartDragEnd);
        _root.RegisterCallback<PointerCancelEvent>(OnPartDragCancel);
        _root.UnregisterCallback<GeometryChangedEvent>(OnDetailLayoutChanged);
        _root.RegisterCallback<GeometryChangedEvent>(OnDetailLayoutChanged);
        _detailPanel.UnregisterCallback<GeometryChangedEvent>(OnDetailLayoutChanged);
        _detailPanel.RegisterCallback<GeometryChangedEvent>(OnDetailLayoutChanged);
        _playerPortrait?.UnregisterCallback<GeometryChangedEvent>(OnDetailLayoutChanged);
        _playerPortrait?.RegisterCallback<GeometryChangedEvent>(OnDetailLayoutChanged);
        _partLayer?.UnregisterCallback<GeometryChangedEvent>(OnPartLayerGeometryChanged);
        _partLayer?.RegisterCallback<GeometryChangedEvent>(OnPartLayerGeometryChanged);

        Refresh();
    }

    void OnDisable()
    {
        if (_root == null)
            return;

        HideSkillTooltip();
        _root.UnregisterCallback<PointerMoveEvent>(OnPartDragMove);
        _root.UnregisterCallback<PointerUpEvent>(OnPartDragEnd);
        _root.UnregisterCallback<PointerCancelEvent>(OnPartDragCancel);
        _root.UnregisterCallback<GeometryChangedEvent>(OnDetailLayoutChanged);
        _detailPanel?.UnregisterCallback<GeometryChangedEvent>(OnDetailLayoutChanged);
        _playerPortrait?.UnregisterCallback<GeometryChangedEvent>(OnDetailLayoutChanged);
        _partLayer?.UnregisterCallback<GeometryChangedEvent>(OnPartLayerGeometryChanged);
        HideDetailPopup();
        CancelPartDrag();
    }

    // ── Public API ────────────────────────────────────────────

    /// <summary>Full refresh — call after any GameState change.</summary>
    public void Refresh()
    {
        RefreshPlayerPortrait();
        RefreshLibraryGrid();
        RefreshPlacedParts();
        RefreshStats();
        RefreshSkillBar();
        RefreshDetailPane(GetActiveDetailPart());
        SetBeginButtonEnabled(HasAssignedParts());
    }

    void SetBeginButtonEnabled(bool enabled)
    {
        if (_beginButton == null)
            return;

        _beginButton.SetEnabled(enabled);
        _beginButton.pickingMode = enabled ? PickingMode.Position : PickingMode.Ignore;
    }

    public void Show()
    {
        if (_root == null) return;

        _root.style.display = DisplayStyle.Flex;
        Refresh();
    }

    public void Hide()
    {
        HideDetailPopup();
        HideSkillTooltip();
        _hoveredPart = null;
        _hoveredPartElement = null;
        if (_root != null)
            _root.style.display = DisplayStyle.None;
    }

    // ── Wiring helpers ────────────────────────────────────────

    void QueryElements()
    {
        // Library
        _libraryPanel = _root.Q("library-panel");
        _libraryGrid = _root.Q("library-grid");
        _playerPortrait = _root.Q<Image>("fish-art");
        _speciesRadius = _root.Q("species-radius");
        _partLayer = _root.Q("part-layer");
        _connectionLineLayer = _root.Q("part-connection-lines");
        if (_connectionLineLayer != null)
        {
            _connectionLineLayer.pickingMode = PickingMode.Ignore;
        }
        if (_speciesRadius != null)
        {
            _speciesRadius.pickingMode = PickingMode.Ignore;
            _speciesRadius.style.display = DisplayStyle.None;
        }
        if (_playerPortrait != null)
        {
            _playerPortrait.scaleMode = ScaleMode.ScaleToFit;
            _playerPortrait.pickingMode = PickingMode.Ignore;
        }
        _searchField = _root.Q<TextField>("search-field");

        // Detail popup
        _detailPanel = _root.Q("detail-panel");
        _detailPointer = _root.Q("detail-pointer");
        _detailContent = _root.Q("detail-content");
        _detailName = _root.Q<Label>("detail-name");
        _detailRarity = _root.Q<Label>("detail-rarity");
        _detailTypeTag = _root.Q<Label>("detail-type-tag");
        _detailDescription = _root.Q<Label>("detail-description");
        _detailStats = _root.Q("detail-stats");

        _playerSkillButtons = _root.Q("player-skill-buttons");
        InitializeSkillTooltip();
        var battleStyles = Resources.Load<StyleSheet>("UI/BattlePanel");
        if (battleStyles != null && !_playerSkillButtons.styleSheets.Contains(battleStyles))
        {
            // Scope the encounter stylesheet to the preview, not the entire editor.
            _playerSkillButtons.styleSheets.Add(battleStyles);
        }

        // Stats panel
        _mutationPointsValue = _root.Q<Label>("mutation-points-value");
        _statSizeTotal = _root.Q<Label>("stat-size-total");
        _statHealthTotal = _root.Q<Label>("stat-health-total");
        _statAttackTotal = _root.Q<Label>("stat-attack-total");
        _statDefenseTotal = _root.Q<Label>("stat-defense-total");
        _statForageTotal = _root.Q<Label>("stat-forage-total");
        _beginButton = _root.Q<Button>("begin-button");
    }

    void RefreshPlayerPortrait()
    {
        if (_playerPortrait == null)
        {
            return;
        }

        Sprite portrait = GameState?.PlayerSpecies?.Portrait
            ?? GlobalConstantsHandler.Constants?.defaultSpeciesPortrait;
        if (portrait != null)
            _playerPortrait.sprite = portrait;
        _playerPortrait.style.display = DisplayStyle.Flex;
    }

    void BindFilterButtons()
    {
        string[] filters = { "All", "Attack", "Feeding", "Defense", "Behavior", "Mutation" };
        foreach (var f in filters)
        {
            var btn = _root.Q<Button>($"filter-{f.ToLower()}");
            if (btn == null) continue;
            string captured = f;
            btn.clicked += () =>
            {
                _activeFilter = captured;
                // Toggle active class on all filter buttons
                foreach (var ff in filters)
                {
                    var b = _root.Q<Button>($"filter-{ff.ToLower()}");
                    Utility.UI.EnableClass(ff == _activeFilter, b, "sep-filter-btn--active");
                }
                RefreshLibraryGrid();
            };
        }
    }

    void BindBeginButton()
    {
        _beginButton?.RegisterCallback<ClickEvent>(_ => OnBegin());
    }

    // ── Refresh sub-sections ──────────────────────────────────

    void RefreshLibraryGrid()
    {
        _libraryGrid.Clear();
        _libraryCards.Clear();

        string search = _searchField?.value?.ToLower() ?? "";

        foreach (var part in GameState.Inventory.AvailableParts)
        {
            if (!MatchesFilter(part))
                continue;
            if (search.Length > 0 && !part.Name.ToLower().Contains(search))
                continue;

            var card = new PartCardElement(part);
            card.SetEquipped(false);
            if (part == _selectedPart)
                card.SetSelected(true);
            card.OnSelected += OnLibraryCardSelected;
            card.RegisterCallback<PointerDownEvent>(e => BeginLibraryPartDrag(card, e));
            _libraryGrid.Add(card);
            _libraryCards.Add(card);
        }
    }

    void RefreshPlacedParts()
    {
        if (_partLayer == null)
        {
            return;
        }

        foreach (VisualElement circle in _equippedPartElements.Values)
        {
            circle.RemoveFromHierarchy();
        }
        _equippedPartElements.Clear();
        if (GameState?.PlayerSpecies != null)
        {
            GameState.PlayerSpecies.RebuildConnectedParts();
            RefreshConnectionLines(GameState.PlayerSpecies.ConnectedParts, GameState.PlayerSpecies.Parts);
        }
        float width = _partLayer.resolvedStyle.width;
        float height = _partLayer.resolvedStyle.height;
        if (width <= 0f || height <= 0f)
        {
            _hoveredPartElement = null;
            return;
        }

        Vector2 layerCenter = new Vector2(width * 0.5f, height * 0.5f);
        float bodyRadius = GetBodyRadiusPixels();
        foreach (Part part in GameState.Inventory.EquippedParts)
        {
            float diameter = part.Radius * bodyRadius * 2f * PartSpriteVisualScale;
            var circle = CreatePartCircle(part);
            circle.style.width = diameter;
            circle.style.height = diameter;
            circle.style.left = layerCenter.x + part.PlacementPosition.x * bodyRadius - diameter * 0.5f;
            circle.style.top = layerCenter.y + part.PlacementPosition.y * bodyRadius - diameter * 0.5f;
            circle.tooltip = $"{part.Name} — drag to reposition or return to the library";
            circle.RegisterCallback<PointerDownEvent>(e => BeginEquippedPartDrag(part, circle, e));
            circle.RegisterCallback<PointerEnterEvent>(_ => OnEquippedPartHoverEnter(part, circle));
            circle.RegisterCallback<PointerLeaveEvent>(_ => OnEquippedPartHoverLeave(part, circle));
            _partLayer.Add(circle);
            _equippedPartElements[part] = circle;
        }

        _hoveredPartElement = _hoveredPart != null
            && _equippedPartElements.TryGetValue(_hoveredPart, out VisualElement hoveredElement)
            ? hoveredElement
            : null;
    }

    void RefreshConnectionLines(
        Dictionary<Part, HashSet<Part>> connections,
        IReadOnlyList<Part> parts)
    {
        if (_connectionLineLayer == null)
        {
            return;
        }

        _connectionLineLayer.Clear();
        if (connections == null || parts == null)
        {
            return;
        }

        var partIndexes = new Dictionary<Part, int>();
        for (int index = 0; index < parts.Count; index++)
        {
            if (parts[index] != null)
            {
                partIndexes[parts[index]] = index;
            }
        }

        foreach (Part part in parts)
        {
            if (part == null || !connections.TryGetValue(part, out HashSet<Part> connectedParts))
            {
                continue;
            }

            foreach (Part connectedPart in connectedParts)
            {
                if (partIndexes.TryGetValue(connectedPart, out int connectedIndex)
                    && partIndexes[part] < connectedIndex)
                {
                    AddConnectionLine(part, connectedPart);
                }
            }
        }
    }

    void AddConnectionLine(Part first, Part second)
    {
        float width = _partLayer.resolvedStyle.width;
        float height = _partLayer.resolvedStyle.height;
        float bodyRadius = GetBodyRadiusPixels();
        if (width <= 0f || height <= 0f || bodyRadius <= 0f)
        {
            return;
        }

        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        Vector2 firstCenter = center + first.PlacementPosition * bodyRadius;
        Vector2 secondCenter = center + second.PlacementPosition * bodyRadius;
        Vector2 centerDelta = secondCenter - firstCenter;
        float centerDistance = centerDelta.magnitude;
        if (centerDistance <= 0.001f)
        {
            return;
        }

        Vector2 direction = centerDelta / centerDistance;
        float lineExtraLength = 10f; // extend the line a bit beyond the circles for visual clarity
        float firstRadiusPixels = first.Radius * bodyRadius - lineExtraLength;
        float secondRadiusPixels = second.Radius * bodyRadius - lineExtraLength;
        Vector2 lineStart = firstCenter + direction * firstRadiusPixels;
        Vector2 lineEnd = secondCenter - direction * secondRadiusPixels;
        Vector2 lineDelta = lineEnd - lineStart;
        float length = lineDelta.magnitude;
        if (length <= 1f)
        {
            return;
        }

        Vector2 midpoint = (lineStart + lineEnd) * 0.5f;
        var line = new VisualElement
        {
            pickingMode = PickingMode.Ignore,
        };
        line.AddToClassList("sep-part-connection-line");
        line.style.width = length;
        line.style.left = midpoint.x - length * 0.5f;
        line.style.top = midpoint.y - 1.5f;
        line.style.rotate = new Rotate(Angle.Degrees(Mathf.Atan2(lineDelta.y, lineDelta.x) * Mathf.Rad2Deg));
        _connectionLineLayer.Add(line);
    }

    void UpdatePartSelectionVisuals()
    {
        foreach (var pair in _equippedPartElements)
        {
            pair.Value.EnableInClassList("sep-equipped-part--selected", pair.Key == _selectedPart);
        }
    }

    Image CreatePartCircle(Part part)
    {
        var image = new Image
        {
            sprite = part.PartSprite != null ? part.PartSprite : part.ActionIcon,
            scaleMode = ScaleMode.ScaleToFit,
        };
        image.AddToClassList("sep-equipped-part");
        image.AddToClassList(RarityClass(part.Rarity));
        image.AddToClassList(GetPartCircleTypeClass(part));
        if (part == _selectedPart)
            image.AddToClassList("sep-equipped-part--selected");

        if (image.sprite == null)
        {
            var glyph = new Label(GetEquipPartGlyph(part));
            glyph.AddToClassList("sep-equipped-part__fallback");
            glyph.pickingMode = PickingMode.Ignore;
            image.Add(glyph);
        }

        return image;
    }

    float GetBodyRadiusPixels()
    {
        float width = _partLayer.resolvedStyle.width;
        float height = _partLayer.resolvedStyle.height;
        float size = Mathf.Min(width, height) * 0.95f; // leave a little padding around the edges
        return Mathf.Max(0f, size / 2.0f * GlobalConstantsHandler.Constants.BodyRadiusMultiplier);
    }

    void OnPartLayerGeometryChanged(GeometryChangedEvent evt)
    {
        RefreshPlacedParts();
    }

    void RefreshStats()
    {
        var s = GameState.PlayerSpecies;
        var inv = GameState.Inventory;

        _mutationPointsValue.text = inv.MutationPoints.ToString();

        _statSizeTotal.text = s.Size.ToString();
        _statHealthTotal.text = s.MaxHealth.ToString();
        _statAttackTotal.text = s.Attack.ToString();
        _statDefenseTotal.text = s.Defense.ToString();
        _statForageTotal.text = s.Forage.ToString();

        SetStatDeltaClass(_statSizeTotal, s.Size - s.BaseSize);
        SetStatDeltaClass(_statHealthTotal, s.MaxHealth - s.BaseHealth);
        SetStatDeltaClass(_statAttackTotal, s.Attack - s.BaseAttack);
        SetStatDeltaClass(_statDefenseTotal, s.Defense - s.BaseDefense);
        SetStatDeltaClass(_statForageTotal, s.Forage - s.BaseForage);
    }

    static void SetStatDeltaClass(Label label, int delta)
    {
        Utility.UI.EnableClass(delta > 0, label, "sep-stat-value--buffed");
        Utility.UI.EnableClass(delta < 0, label, "sep-stat-value--debuffed");
    }

    void RefreshSkillBar()
    {
        HideSkillTooltip();
        _playerSkillButtons.Clear();
        var species = GameState.PlayerSpecies;
        foreach (var part in SpeciesHealthBarElement.GetAvailableActions(species))
        {
            var button = SpeciesHealthBarElement.CreateActionButton(species, part);
            // Preserve encounter visuals/tooltips without allowing combat from the editor.
            button.focusable = false;
            button.RegisterCallback<PointerEnterEvent>(_ => ShowSkillTooltip(button));
            button.RegisterCallback<PointerLeaveEvent>(_ => HideSkillTooltip(button));
            _playerSkillButtons.Add(button);
        }
    }

    void InitializeSkillTooltip()
    {
        _skillTooltipContainer = _root.Q("sep-root");
        _skillTooltip = _skillTooltipContainer.Q<VisualElement>("sep-skill-tooltip");
        if (_skillTooltip == null)
        {
            _skillTooltip = new VisualElement { name = "sep-skill-tooltip" };
            _skillTooltip.AddToClassList("sep-skill-tooltip");
            _skillTooltip.pickingMode = PickingMode.Ignore;

            _skillTooltipTitle = new Label { name = "sep-skill-tooltip-title" };
            _skillTooltipTitle.AddToClassList("sep-skill-tooltip__title");
            _skillTooltipTitle.pickingMode = PickingMode.Ignore;
            _skillTooltip.Add(_skillTooltipTitle);

            _skillTooltipDescription = new Label { name = "sep-skill-tooltip-description" };
            _skillTooltipDescription.AddToClassList("sep-skill-tooltip__description");
            _skillTooltipDescription.pickingMode = PickingMode.Ignore;
            _skillTooltip.Add(_skillTooltipDescription);

            _skillTooltipContainer.Add(_skillTooltip);
        }
        else
        {
            _skillTooltipTitle = _skillTooltip.Q<Label>("sep-skill-tooltip-title");
            _skillTooltipDescription = _skillTooltip.Q<Label>("sep-skill-tooltip-description");
        }

        _skillTooltip.style.display = DisplayStyle.None;
    }

    void ShowSkillTooltip(Button button)
    {
        if (button == null || _skillTooltip == null || _root == null)
        {
            return;
        }

        string tooltip = button.tooltip ?? string.Empty;
        int separator = tooltip.IndexOf('\n');
        _skillTooltipTitle.text = separator < 0 ? tooltip : tooltip.Substring(0, separator);
        _skillTooltipDescription.text = separator < 0 ? string.Empty : tooltip.Substring(separator + 1);
        _skillTooltipAnchor = button;
        _skillTooltip.style.display = DisplayStyle.Flex;
        _root.schedule.Execute(() =>
        {
            if (_skillTooltipAnchor == button)
            {
                PositionSkillTooltip(button);
            }
        });
    }

    void HideSkillTooltip(VisualElement anchor = null)
    {
        if (anchor != null && _skillTooltipAnchor != anchor)
        {
            return;
        }

        _skillTooltipAnchor = null;
        if (_skillTooltip != null)
        {
            _skillTooltip.style.display = DisplayStyle.None;
        }
    }

    void PositionSkillTooltip(VisualElement anchor)
    {
        if (anchor == null || _skillTooltip == null || _skillTooltipContainer == null)
        {
            return;
        }

        Rect bounds = anchor.worldBound;
        Vector2 topLeft = _skillTooltipContainer.WorldToLocal(bounds.position);
        Vector2 bottomRight = _skillTooltipContainer.WorldToLocal(new Vector2(bounds.xMax, bounds.yMax));
        float tooltipWidth = Mathf.Max(1f, _skillTooltip.resolvedStyle.width);
        float tooltipHeight = Mathf.Max(1f, _skillTooltip.resolvedStyle.height);
        float containerWidth = Mathf.Max(1f, _skillTooltipContainer.resolvedStyle.width);
        float containerHeight = Mathf.Max(1f, _skillTooltipContainer.resolvedStyle.height);
        const float edgePadding = 8f;
        const float gap = 8f;

        float left = Mathf.Clamp((topLeft.x + bottomRight.x - tooltipWidth) * 0.5f,
            edgePadding, Mathf.Max(edgePadding, containerWidth - tooltipWidth - edgePadding));
        float top = topLeft.y - tooltipHeight - gap;
        if (top < edgePadding)
        {
            top = bottomRight.y + gap;
        }
        top = Mathf.Clamp(top, edgePadding,
            Mathf.Max(edgePadding, containerHeight - tooltipHeight - edgePadding));

        _skillTooltip.style.left = left;
        _skillTooltip.style.top = top;
    }

    void RefreshDetailPane(Part part)
    {
        _detailAnchor = GetDetailAnchor(part);
        if (part == null || _detailAnchor == null || (_isDraggingPart && _dragMoved))
        {
            HideDetailPopup();
            return;
        }

        _detailPanel.style.display = DisplayStyle.Flex;
        _detailContent.style.display = DisplayStyle.Flex;

        _detailName.text = part.Name.ToUpper();
        _detailRarity.text = part.Rarity.ToString().ToUpper();
        _detailTypeTag.text = GetTypeLabel(part);
        _detailDescription.text = GetPartDescription(part);

        // Apply tag colour class
        foreach (var cls in new[] { "sep-tag--attack","sep-tag--defense","sep-tag--feeding",
                                    "sep-tag--mutation","sep-tag--behavior" })
            _detailTypeTag.RemoveFromClassList(cls);
        _detailTypeTag.AddToClassList(GetTypeTagClass(part));
        _detailTypeTag.AddToClassList("sep-detail-type-tag");

        // Apply rarity colour to name
        foreach (var cls in new[] { "sep-rarity--common","sep-rarity--uncommon","sep-rarity--rare",
                                    "sep-rarity--epic","sep-rarity--legendary" })
            _detailRarity.RemoveFromClassList(cls);
        _detailRarity.AddToClassList(RarityClass(part.Rarity));

        // Stat lines
        _detailStats.Clear();
        AddDetailStatIfNonZero("⚔ Attack", part.Attack);
        AddDetailStatIfNonZero("🛡 Defense", part.Defense);
        AddDetailStatIfNonZero("🌿 Forage", part.Forage);
        AddDetailStatIfNonZero("❤ Health", part.Health);
        AddDetailStatIfNonZero("📏 Size", part.Size);
        AddDetailStat($"◯ Placement Radius: {part.Radius:P0}");
        if (part.HasAction)
        {
            if (part.IsPassive)
            {
                AddDetailStat("Passive skill: no energy cost or cooldown");
            }
            else
            {
                AddDetailStat($"Energy Cost: {Species.GetActionEnergyCost(part)} (5 per turn)");
                AddDetailStat($"Cooldown: {Mathf.Max(0, part.CooldownTurns)} turn(s)");
                AddDetailStat(part.CooldownTurns <= 0
                    ? "Can be reused in the same turn"
                    : $"Reusable after {part.CooldownTurns} turn start(s)");
            }
        }
        if (part.MutationCost > 0)
            AddDetailStat($"🧬 Remove Cost: {part.MutationCost} MP");

        PositionDetailPopup();
        _detailPanel.schedule.Execute(PositionDetailPopup);
    }

    VisualElement GetDetailAnchor(Part part)
    {
        if (part == null)
        {
            return null;
        }

        if (_hoveredPart == part && _hoveredPartElement != null)
            return _hoveredPartElement;

        if (_equippedPartElements.TryGetValue(part, out VisualElement equippedElement))
            return equippedElement;

        return _libraryCards.FirstOrDefault(card => card.Part == part);
    }

    void HideDetailPopup()
    {
        _detailAnchor = null;
        if (_detailPanel != null)
        {
            _detailPanel.style.display = DisplayStyle.None;
        }
    }

    void OnDetailLayoutChanged(GeometryChangedEvent evt)
    {
        if (evt.oldRect.size != evt.newRect.size)
        {
            PositionDetailPopup();
        }
    }

    void PositionDetailPopup()
    {
        if (_detailAnchor == null || _detailPanel == null)
        {
            return;
        }

        var container = _detailPanel.parent;
        float containerWidth = container.resolvedStyle.width;
        float containerHeight = container.resolvedStyle.height;
        float popupWidth = _detailPanel.resolvedStyle.width;
        float popupHeight = _detailPanel.resolvedStyle.height;
        if (float.IsNaN(popupWidth) || float.IsNaN(popupHeight)
            || containerWidth <= 0f || containerHeight <= 0f)
        {
            return;
        }

        const float gap = 12f;
        const float edge = 8f;
        Rect anchor = _detailAnchor.worldBound;
        Vector2 topLeft = container.WorldToLocal(anchor.position);
        Vector2 bottomRight = container.WorldToLocal(new Vector2(anchor.xMax, anchor.yMax));
        Vector2 center = (topLeft + bottomRight) * 0.5f;
        // Anchor the popup to the side with available space around the hovered part.
        bool placeOnRight = _detailAnchor is PartCardElement
            || anchor.center.x <= _root.Q("fish-preview").worldBound.center.x;
        float left = placeOnRight ? bottomRight.x + gap : topLeft.x - popupWidth - gap;
        float top = Mathf.Clamp(center.y - popupHeight * 0.5f,
            edge, Mathf.Max(edge, containerHeight - popupHeight - edge));

        _detailPanel.style.left = Mathf.Clamp(left, edge,
            Mathf.Max(edge, containerWidth - popupWidth - edge));
        _detailPanel.style.top = top;
        _detailPointer.EnableInClassList("sep-detail-pointer--right", !placeOnRight);
        _detailPointer.style.top = Mathf.Clamp(center.y - top - 6f,
            12f, Mathf.Max(12f, popupHeight - 24f));
    }

    void AddDetailStatIfNonZero(string label, int value)
    {
        if (value == 0) return;
        AddDetailStat($"{label}: {(value > 0 ? "+" : "")}{value}");
    }

    void AddDetailStat(string text)
    {
        var lbl = new Label(text);
        lbl.pickingMode = PickingMode.Ignore;
        lbl.AddToClassList("sep-detail-stat");
        _detailStats.Add(lbl);
    }

    // ── Interaction handlers ──────────────────────────────────

    void BeginLibraryPartDrag(PartCardElement card, PointerDownEvent e)
    {
        BeginPartDrag(card?.Part, DragSource.Library, card, e);
    }

    void BeginEquippedPartDrag(Part part, VisualElement element, PointerDownEvent e)
    {
        BeginPartDrag(part, DragSource.EquippedPart, element, e);
        if (!_isDraggingPart)
            return;

        Vector2 pointerPosition = _partLayer.WorldToLocal(e.position);
        Vector2 layerCenter = new Vector2(
            _partLayer.resolvedStyle.width * 0.5f,
            _partLayer.resolvedStyle.height * 0.5f);
        Vector2 partCenter = layerCenter + part.PlacementPosition * GetBodyRadiusPixels();
        _dragPointerOffset = pointerPosition - partCenter;
        UpdatePlacementPreview(e.position);
    }

    void BeginPartDrag(
        Part part,
        DragSource source,
        VisualElement sourceElement,
        PointerDownEvent e)
    {
        if (_isDraggingPart || part == null || _root == null || _partLayer == null)
            return;

        _isDraggingPart = true;
        _dragMoved = false;
        _suppressNextClick = false;
        if (_speciesRadius != null)
            _speciesRadius.style.display = DisplayStyle.Flex;
        _dragPointerId = e.pointerId;
        _dragStartPosition = e.position;
        _draggedPart = part;
        _dragSource = source;
        _dragPointerOffset = Vector2.zero;
        _dragSourceElement = sourceElement;
        _dragSourceElement?.AddToClassList("sep-drag-source");

        _placementPreview = CreatePartCircle(part);
        _placementPreview.name = "part-placement-preview";
        _placementPreview.AddToClassList("sep-part-preview");
        _placementPreview.pickingMode = PickingMode.Ignore;
        _root.Add(_placementPreview);
        UpdatePlacementPreview(e.position);
        _root.CapturePointer(_dragPointerId);
    }

    void OnPartDragMove(PointerMoveEvent e)
    {
        if (!_isDraggingPart || e.pointerId != _dragPointerId)
            return;

        Vector2 pointerPosition = e.position;
        if ((pointerPosition - _dragStartPosition).sqrMagnitude >= 16f)
        {
            _dragMoved = true;
            HideDetailPopup();
        }

        UpdatePlacementPreview(pointerPosition);
        SetActiveDropTarget(_libraryPanel?.worldBound.Contains(pointerPosition) == true
            ? _libraryPanel
            : null);
    }

    void OnPartDragEnd(PointerUpEvent e)
    {
        if (!_isDraggingPart || e.pointerId != _dragPointerId)
            return;

        bool moved = _dragMoved;
        var source = _dragSource;
        var part = _draggedPart;
        var sourceElement = _dragSourceElement;
        if (moved)
            ApplyPartDrop(e.position);

        FinishPartDrag();
        if (!moved)
        {
            _suppressNextClick = false;
            if (source == DragSource.Library && sourceElement is PartCardElement card)
                OnLibraryCardSelected(card);
            else if (source == DragSource.EquippedPart)
                OnEquippedPartSelected(part);
        }

        _suppressNextClick = true;
        _root.schedule.Execute(() => _suppressNextClick = false);
    }

    void OnPartDragCancel(PointerCancelEvent e)
    {
        if (!_isDraggingPart || e.pointerId != _dragPointerId)
            return;

        CancelPartDrag();
    }

    void UpdatePlacementPreview(Vector2 worldPosition)
    {
        if (_placementPreview == null || _partLayer == null || _root == null)
            return;

        bool insideLayer = _partLayer.worldBound.Contains(worldPosition);
        Vector2 localCenter = _partLayer.WorldToLocal(worldPosition) - _dragPointerOffset;
        Vector2 worldCenter = _partLayer.LocalToWorld(localCenter);
        Vector2 rootCenter = _root.WorldToLocal(worldCenter);
        Vector2 layerCenter = new Vector2(
            _partLayer.resolvedStyle.width * 0.5f,
            _partLayer.resolvedStyle.height * 0.5f);
        float bodyRadius = GetBodyRadiusPixels();
        Vector2 normalizedPosition = bodyRadius <= 0f
            ? Vector2.zero
            : (localCenter - layerCenter) / bodyRadius;
        float diameter = Mathf.Max(
            44f,
            _draggedPart.Radius * bodyRadius * 2f * PartSpriteVisualScale);
        _placementPreview.style.display = DisplayStyle.Flex;
        _placementPreview.style.width = diameter;
        _placementPreview.style.height = diameter;
        _placementPreview.style.left = rootCenter.x - diameter * 0.5f;
        _placementPreview.style.top = rootCenter.y - diameter * 0.5f;

        Part ignoredPart = _dragSource == DragSource.EquippedPart ? _draggedPart : null;
        bool valid = insideLayer && PartCirclePacking.IsValidPlacement(
            normalizedPosition,
            _draggedPart.Radius,
            GameState.Inventory.EquippedParts,
            ignoredPart);
        _placementPreview.EnableInClassList("sep-part-preview--invalid", !valid);

        if (valid)
        {
            Part previewPart = _draggedPart.CreatePlacementPreview(normalizedPosition);
            var parts = GameState.Inventory.EquippedParts
                .Where(part => _dragSource != DragSource.EquippedPart || part != _draggedPart)
                .ToList();
            parts.Add(previewPart);
            RefreshConnectionLines(PartCirclePacking.BuildConnectedParts(parts), parts);
        }
        else if (GameState?.PlayerSpecies != null)
        {
            RefreshConnectionLines(GameState.PlayerSpecies.ConnectedParts, GameState.PlayerSpecies.Parts);
        }
    }

    void SetActiveDropTarget(VisualElement target)
    {
        if (_activeDropTarget == target)
            return;

        ClearActiveDropTarget();
        _activeDropTarget = target;
        if (_activeDropTarget == null)
            return;

        if (_activeDropTarget == _libraryPanel)
            _activeDropTarget.AddToClassList("sep-library-panel--drop-target");
    }

    void ClearActiveDropTarget()
    {
        if (_activeDropTarget == null)
            return;

        _activeDropTarget.RemoveFromClassList("sep-library-panel--drop-target");
        _activeDropTarget = null;
    }

    bool TryGetPlacementPosition(Vector2 worldPosition, out Vector2 position)
    {
        position = Vector2.zero;
        if (_partLayer == null || !_partLayer.worldBound.Contains(worldPosition))
            return false;

        Vector2 localCenter = _partLayer.WorldToLocal(worldPosition) - _dragPointerOffset;
        Vector2 layerCenter = new Vector2(
            _partLayer.resolvedStyle.width * 0.5f,
            _partLayer.resolvedStyle.height * 0.5f);
        float bodyRadius = GetBodyRadiusPixels();
        if (bodyRadius <= 0f)
            return false;

        position = (localCenter - layerCenter) / bodyRadius;
        return true;
    }

    void ApplyPartDrop(Vector2 worldPosition)
    {
        bool changed = false;
        bool overLibrary = _libraryPanel?.worldBound.Contains(worldPosition) == true;
        if (_dragSource == DragSource.Library
            && TryGetPlacementPosition(worldPosition, out Vector2 position))
        {
            changed = GameState.Inventory.EquipPart(_draggedPart, position);
        }
        else if (_dragSource == DragSource.EquippedPart && overLibrary)
        {
            changed = GameState.Inventory.RemovePart(_draggedPart);
        }
        else if (_dragSource == DragSource.EquippedPart
            && TryGetPlacementPosition(worldPosition, out Vector2 movedPosition))
        {
            changed = GameState.Inventory.MoveEquippedPart(_draggedPart, movedPosition);
        }

        if (!changed)
            return;

        GameState.Inventory.ApplyToSpecies(GameState.PlayerSpecies);
        _selectedPart = null;
        _selectedCard = null;
        _hoveredPart = null;
        _hoveredPartElement = null;
        saveDirty = true;
        Refresh();
    }

    void FinishPartDrag()
    {
        int pointerId = _dragPointerId;
        bool releasePointer = _root != null && pointerId >= 0 && _root.HasPointerCapture(pointerId);
        _dragPointerId = -1;
        _isDraggingPart = false;
        if (releasePointer)
            _root.ReleasePointer(pointerId);

        if (_speciesRadius != null)
            _speciesRadius.style.display = DisplayStyle.None;
        if (GameState?.PlayerSpecies != null)
        {
            RefreshConnectionLines(GameState.PlayerSpecies.ConnectedParts, GameState.PlayerSpecies.Parts);
        }
        ClearActiveDropTarget();
        _dragSourceElement?.RemoveFromClassList("sep-drag-source");
        _placementPreview?.RemoveFromHierarchy();
        _placementPreview = null;
        _dragSourceElement = null;
        _draggedPart = null;
    }

    void CancelPartDrag()
    {
        FinishPartDrag();
        _dragMoved = false;
        _suppressNextClick = false;
    }

    void OnLibraryCardSelected(PartCardElement card)
    {
        if (_suppressNextClick)
        {
            _suppressNextClick = false;
            return;
        }

        _selectedCard?.SetSelected(false);
        _hoveredPart = null;
        _hoveredPartElement = null;

        if (_selectedCard == card)
        {
            _selectedCard = null;
            _selectedPart = null;
        }
        else
        {
            _selectedCard = card;
            _selectedPart = card.Part;
            card.SetSelected(true);
        }

        UpdatePartSelectionVisuals();
        RefreshDetailPane(_selectedPart);
    }

    void OnEquippedPartSelected(Part part)
    {
        if (_suppressNextClick)
        {
            _suppressNextClick = false;
            return;
        }

        _selectedCard?.SetSelected(false);
        _selectedCard = null;
        _selectedPart = _selectedPart == part ? null : part;
        UpdatePartSelectionVisuals();
        RefreshDetailPane(GetActiveDetailPart());
    }

    void OnEquippedPartHoverEnter(Part part, VisualElement element)
    {
        _hoveredPart = part;
        _hoveredPartElement = element;
        RefreshDetailPane(part);
    }

    void OnEquippedPartHoverLeave(Part part, VisualElement element)
    {
        if (_hoveredPart == part && _hoveredPartElement == element)
        {
            _hoveredPart = null;
            _hoveredPartElement = null;
        }

        RefreshDetailPane(GetActiveDetailPart());
    }

    void OnSave()
    {
        GameState.Inventory.ApplyToSpecies(GameState.PlayerSpecies);
        Debug.Log($"[SpeciesEditor] Species saved: {GameState.PlayerSpecies.Name} " +
                  $"| ATK {GameState.PlayerSpecies.Attack} " +
                  $"| DEF {GameState.PlayerSpecies.Defense} " +
                  $"| HP {GameState.PlayerSpecies.MaxHealth}");
        if (saveDirty)
        {
            Runtime.Events.RequestGlobalSave.Trigger(new Runtime.Events.RequestGlobalSave());
            saveDirty = false;
        }
    }

    void OnBegin()
    {
        if (!HasAssignedParts())
            return;

        OnSave();
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.ShowWorldMap();
        else
            Debug.LogWarning("[SpeciesEditorPanel] ScreenManager.Instance is missing; cannot open World Map.");
    }

    bool HasAssignedParts()
    {
        return GameState?.OwnedSpecies?.Any(species =>
            species?.Parts?.Any(part => part != null) == true) == true;
    }

    bool MatchesFilter(Part part)
    {
        return _activeFilter switch
        {
            "Attack" => part.Attack > 0,
            "Defense" => part.Defense > 0 || part.Health > 0,
            "Feeding" => part.Forage > 0,
            "Mutation" => part.MutationCost > 0,
            "Behavior" => part.Attack == 0 && part.Defense == 0 &&
                          part.Forage == 0 && part.Health == 0 && part.Size == 0,
            _ => true, // "All"
        };
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

    Part GetActiveDetailPart()
    {
        return _hoveredPart ?? _selectedPart;
    }

    static string GetEquipPartGlyph(Part part)
    {
        if (part.Attack > 0) return "ATK";
        if (part.Defense > 0 || part.Health > 0) return "DEF";
        if (part.Forage > 0) return "FOR";
        return "MUT";
    }

    static string GetPartCircleTypeClass(Part part)
    {
        if (part.Attack > 0) return "sep-equipped-part--attack";
        if (part.Defense > 0 || part.Health > 0) return "sep-equipped-part--defense";
        if (part.Forage > 0) return "sep-equipped-part--feeding";
        return "sep-equipped-part--mutation";
    }

    static string RarityClass(PartRarity r) => r switch
    {
        PartRarity.Common => "sep-rarity--common",
        PartRarity.Uncommon => "sep-rarity--uncommon",
        PartRarity.Rare => "sep-rarity--rare",
        PartRarity.Epic => "sep-rarity--epic",
        PartRarity.Legendary => "sep-rarity--legendary",
        _ => "sep-rarity--common",
    };

    /// <summary>Override per-part to provide flavour text; falls back to stats summary.</summary>
    static string GetPartDescription(Part part)
    {
        return part.Description;
    }
}
