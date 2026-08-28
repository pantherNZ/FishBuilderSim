using System;
using System.Collections.Generic;
using System.Linq;
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
    // ── Inspector ─────────────────────────────────────────────
    [Tooltip("Leave null to auto-create a default GameState on Awake.")]
    public GameState GameState;

    // ── Private references ────────────────────────────────────
    UIDocument _doc;
    VisualElement _root;

    // Library
    VisualElement _libraryPanel;
    VisualElement _libraryGrid;
    TextField _searchField;
    Label _drawPileLabel;
    string _activeFilter = "All";

    // Equip slots
    readonly VisualElement[] _equipSlots = new VisualElement[PlayerInventory.EquipSlots];
    readonly VisualElement[] _equipSlotIcons = new VisualElement[PlayerInventory.EquipSlots];
    readonly Label[] _equipSlotIconGlyphs = new Label[PlayerInventory.EquipSlots];

    // Detail pane
    VisualElement _detailIcon;
    VisualElement _detailContent;
    Label _detailEmptyLabel;
    Label _detailName;
    Label _detailRarity;
    Label _detailTypeTag;
    Label _detailDescription;
    VisualElement _detailStats;

    // Stats panel
    Label _mutationPointsValue;
    Label _statSizeBase, _statSizeTotal;
    Label _statHealthBase, _statHealthTotal;
    Label _statAttackBase, _statAttackTotal;
    Label _statDefenseBase, _statDefenseTotal;
    Label _statForageBase, _statForageTotal;

    // Selection state
    Part _selectedPart;
    PartCardElement _selectedCard;
    int _selectedSlotIndex = -1;
    int _hoveredSlotIndex = -1;
    bool saveDirty = false;

    // All library card elements (kept for refresh/filter)
    readonly List<PartCardElement> _libraryCards = new();

    // Pointer drag state
    enum DragSource
    {
        Library,
        EquipSlot,
    }

    bool _isDraggingPart;
    bool _dragMoved;
    bool _suppressNextClick;
    int _dragPointerId = -1;
    int _dragSourceSlot = -1;
    Vector2 _dragStartPosition;
    Part _draggedPart;
    DragSource _dragSource;
    VisualElement _dragSourceElement;
    VisualElement _dragGhost;
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
        BindEquipSlots();
        BindBeginButton();
        _searchField.RegisterValueChangedCallback(_ => RefreshLibraryGrid());

        _root.UnregisterCallback<PointerMoveEvent>(OnPartDragMove);
        _root.UnregisterCallback<PointerUpEvent>(OnPartDragEnd);
        _root.UnregisterCallback<PointerCancelEvent>(OnPartDragCancel);
        _root.RegisterCallback<PointerMoveEvent>(OnPartDragMove);
        _root.RegisterCallback<PointerUpEvent>(OnPartDragEnd);
        _root.RegisterCallback<PointerCancelEvent>(OnPartDragCancel);

        Refresh();
    }

    void OnDisable()
    {
        if (_root == null)
            return;

        _root.UnregisterCallback<PointerMoveEvent>(OnPartDragMove);
        _root.UnregisterCallback<PointerUpEvent>(OnPartDragEnd);
        _root.UnregisterCallback<PointerCancelEvent>(OnPartDragCancel);
        CancelPartDrag();
    }

    // ── Public API ────────────────────────────────────────────

    /// <summary>Full refresh — call after any GameState change.</summary>
    public void Refresh()
    {
        RefreshLibraryGrid();
        RefreshEquipSlots();
        RefreshStats();
        RefreshDetailPane(GetActiveDetailPart());
    }

    public void Show()
    {
        if (_root == null) return;

        _root.style.display = DisplayStyle.Flex;
        Refresh();
    }

    public void Hide()
    {
        if (_root != null)
            _root.style.display = DisplayStyle.None;
    }

    // ── Wiring helpers ────────────────────────────────────────

    void QueryElements()
    {
        // Library
        _libraryPanel = _root.Q("library-panel");
        _libraryGrid = _root.Q("library-grid");
        _searchField = _root.Q<TextField>("search-field");
        _drawPileLabel = _root.Q<Label>("draw-pile-label");

        // Equip slots
        for (int i = 0; i < PlayerInventory.EquipSlots; i++)
        {
            _equipSlots[i] = _root.Q($"equip-slot-{i}");
            _equipSlotIcons[i] = _root.Q($"equip-slot-{i}-icon");

            if (_equipSlotIcons[i] != null)
            {
                var glyph = _equipSlotIcons[i].Q<Label>(className: "sep-equip-slot__icon-glyph");
                if (glyph == null)
                {
                    glyph = new Label();
                    glyph.AddToClassList("sep-equip-slot__icon-glyph");
                    _equipSlotIcons[i].Add(glyph);
                }

                _equipSlotIconGlyphs[i] = glyph;
            }
        }

        // Detail pane
        _detailIcon = _root.Q("detail-icon");
        _detailContent = _root.Q("detail-content");
        _detailEmptyLabel = _root.Q<Label>("detail-empty-label");
        _detailName = _root.Q<Label>("detail-name");
        _detailRarity = _root.Q<Label>("detail-rarity");
        _detailTypeTag = _root.Q<Label>("detail-type-tag");
        _detailDescription = _root.Q<Label>("detail-description");
        _detailStats = _root.Q("detail-stats");

        // Stats panel
        _mutationPointsValue = _root.Q<Label>("mutation-points-value");
        _statSizeBase = _root.Q<Label>("stat-size-base");
        _statSizeTotal = _root.Q<Label>("stat-size-total");
        _statHealthBase = _root.Q<Label>("stat-health-base");
        _statHealthTotal = _root.Q<Label>("stat-health-total");
        _statAttackBase = _root.Q<Label>("stat-attack-base");
        _statAttackTotal = _root.Q<Label>("stat-attack-total");
        _statDefenseBase = _root.Q<Label>("stat-defense-base");
        _statDefenseTotal = _root.Q<Label>("stat-defense-total");
        _statForageBase = _root.Q<Label>("stat-forage-base");
        _statForageTotal = _root.Q<Label>("stat-forage-total");
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

    void BindEquipSlots()
    {
        for (int i = 0; i < PlayerInventory.EquipSlots; i++)
        {
            int idx = i;
            _equipSlots[i]?.RegisterCallback<ClickEvent>(_ => OnEquipSlotClicked(idx));
            _equipSlots[i]?.RegisterCallback<PointerEnterEvent>(_ => OnEquipSlotHoverEnter(idx));
            _equipSlots[i]?.RegisterCallback<PointerLeaveEvent>(_ => OnEquipSlotHoverLeave(idx));
            _equipSlots[i]?.RegisterCallback<PointerDownEvent>(e => BeginEquipSlotDrag(idx, e));
        }
    }

    void BindBeginButton()
    {
        _root.Q<Button>("begin-button")?.RegisterCallback<ClickEvent>(_ => OnBegin());
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

        _drawPileLabel.text = $"Draw Pile  {GameState.Inventory.AvailableParts.Count}";
    }

    void RefreshEquipSlots()
    {
        for (int i = 0; i < PlayerInventory.EquipSlots; i++)
        {
            var part = GameState.Inventory.EquippedParts[i];
            var slot = _equipSlots[i];
            if (slot == null) continue;

            bool occupied = part != null;
            Utility.UI.EnableClass(occupied, slot, "sep-equip-slot--occupied");
            Utility.UI.EnableClass(i == _selectedSlotIndex, slot, "sep-equip-slot--selected");

            // Show/hide icon placeholder
            var icon = _equipSlotIcons[i];
            if (icon != null)
            {
                icon.style.display = occupied ? DisplayStyle.Flex : DisplayStyle.None;

                icon.RemoveFromClassList("sep-equip-slot__icon--attack");
                icon.RemoveFromClassList("sep-equip-slot__icon--defense");
                icon.RemoveFromClassList("sep-equip-slot__icon--feeding");
                icon.RemoveFromClassList("sep-equip-slot__icon--mutation");

                if (occupied)
                    icon.AddToClassList(GetEquipSlotIconClass(part));
            }

            if (_equipSlotIconGlyphs[i] != null)
                _equipSlotIconGlyphs[i].text = occupied ? GetEquipSlotGlyph(part) : string.Empty;

            slot.tooltip = occupied ? part.Name : $"Slot {i + 1}";
        }
    }

    void RefreshStats()
    {
        var s = GameState.PlayerSpecies;
        var inv = GameState.Inventory;

        _mutationPointsValue.text = inv.MutationPoints.ToString();

        _statSizeBase.text = s.BaseSize.ToString();
        _statSizeTotal.text = s.Size.ToString();
        _statHealthBase.text = s.BaseHealth.ToString();
        _statHealthTotal.text = s.MaxHealth.ToString();
        _statAttackBase.text = s.BaseAttack.ToString();
        _statAttackTotal.text = s.Attack.ToString();
        _statDefenseBase.text = s.BaseDefense.ToString();
        _statDefenseTotal.text = s.Defense.ToString();
        _statForageBase.text = s.BaseForage.ToString();
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

    void RefreshDetailPane(Part part)
    {
        if (part == null)
        {
            _detailEmptyLabel.style.display = DisplayStyle.Flex;
            _detailContent.style.display = DisplayStyle.None;
            return;
        }

        _detailEmptyLabel.style.display = DisplayStyle.None;
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
        if (part.MutationCost > 0)
            AddDetailStat($"🧬 Remove Cost: {part.MutationCost} MP");
    }

    void AddDetailStatIfNonZero(string label, int value)
    {
        if (value == 0) return;
        AddDetailStat($"{label}: {(value > 0 ? "+" : "")}{value}");
    }

    void AddDetailStat(string text)
    {
        var lbl = new Label(text);
        lbl.AddToClassList("sep-detail-stat");
        _detailStats.Add(lbl);
    }

    // ── Interaction handlers ──────────────────────────────────

    void BeginLibraryPartDrag(PartCardElement card, PointerDownEvent e)
    {
        BeginPartDrag(card?.Part, DragSource.Library, -1, card, e);
    }

    void BeginEquipSlotDrag(int slotIndex, PointerDownEvent e)
    {
        if (slotIndex < 0 || slotIndex >= PlayerInventory.EquipSlots)
            return;

        BeginPartDrag(
            GameState.Inventory.EquippedParts[slotIndex],
            DragSource.EquipSlot,
            slotIndex,
            _equipSlots[slotIndex],
            e);
    }

    void BeginPartDrag(
        Part part,
        DragSource source,
        int sourceSlot,
        VisualElement sourceElement,
        PointerDownEvent e)
    {
        if (_isDraggingPart || part == null || _root == null)
            return;

        _isDraggingPart = true;
        _dragMoved = false;
        _suppressNextClick = false;
        _dragPointerId = e.pointerId;
        _dragStartPosition = e.position;
        _draggedPart = part;
        _dragSource = source;
        _dragSourceSlot = sourceSlot;
        _dragSourceElement = sourceElement;
        _dragSourceElement?.AddToClassList("sep-drag-source");

        _dragGhost = BuildDragGhost(part);
        _root.Add(_dragGhost);
        PositionDragGhost(e.position);
        _root.CapturePointer(_dragPointerId);
    }

    VisualElement BuildDragGhost(Part part)
    {
        var ghost = new VisualElement();
        ghost.name = "part-drag-ghost";
        ghost.AddToClassList("sep-drag-ghost");
        ghost.AddToClassList(RarityClass(part.Rarity));
        ghost.pickingMode = PickingMode.Ignore;

        var name = new Label(part.Name?.ToUpper() ?? "PART");
        name.AddToClassList("sep-drag-ghost__name");
        ghost.Add(name);

        var type = new Label(GetTypeLabel(part));
        type.AddToClassList("sep-drag-ghost__type");
        ghost.Add(type);

        return ghost;
    }

    void OnPartDragMove(PointerMoveEvent e)
    {
        if (!_isDraggingPart || e.pointerId != _dragPointerId)
            return;

        Vector2 pointerPosition = e.position;
        if ((pointerPosition - _dragStartPosition).sqrMagnitude >= 16f)
            _dragMoved = true;

        PositionDragGhost(pointerPosition);
        SetActiveDropTarget(FindDropTarget(pointerPosition));
    }

    void OnPartDragEnd(PointerUpEvent e)
    {
        if (!_isDraggingPart || e.pointerId != _dragPointerId)
            return;

        bool moved = _dragMoved;
        var source = _dragSource;
        int sourceSlot = _dragSourceSlot;
        var sourceElement = _dragSourceElement;
        if (moved)
            ApplyPartDrop(FindDropTarget(e.position));

        FinishPartDrag();
        if (!moved)
        {
            _suppressNextClick = false;
            if (source == DragSource.Library && sourceElement is PartCardElement card)
                OnLibraryCardSelected(card);
            else if (source == DragSource.EquipSlot)
                OnEquipSlotClicked(sourceSlot);
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

    void PositionDragGhost(Vector2 panelPosition)
    {
        if (_dragGhost == null || _root == null)
            return;

        Vector2 rootPosition = _root.WorldToLocal(panelPosition);
        _dragGhost.style.left = rootPosition.x - 44f;
        _dragGhost.style.top = rootPosition.y - 34f;
    }

    VisualElement FindDropTarget(Vector2 panelPosition)
    {
        for (int i = 0; i < _equipSlots.Length; i++)
        {
            if (_equipSlots[i]?.worldBound.Contains(panelPosition) == true)
                return _equipSlots[i];
        }

        return _libraryPanel?.worldBound.Contains(panelPosition) == true
            ? _libraryPanel
            : null;
    }

    void SetActiveDropTarget(VisualElement target)
    {
        if (_activeDropTarget == target)
            return;

        ClearActiveDropTarget();
        _activeDropTarget = target;
        if (_activeDropTarget == null)
            return;

        if (IsEquipSlot(_activeDropTarget))
            _activeDropTarget.AddToClassList("sep-equip-slot--drop-target");
        else if (_activeDropTarget == _libraryPanel)
            _activeDropTarget.AddToClassList("sep-library-panel--drop-target");
    }

    void ClearActiveDropTarget()
    {
        if (_activeDropTarget == null)
            return;

        _activeDropTarget.RemoveFromClassList("sep-equip-slot--drop-target");
        _activeDropTarget.RemoveFromClassList("sep-library-panel--drop-target");
        _activeDropTarget = null;
    }

    bool IsEquipSlot(VisualElement element)
    {
        for (int i = 0; i < _equipSlots.Length; i++)
            if (element == _equipSlots[i])
                return true;
        return false;
    }

    int GetEquipSlotIndex(VisualElement element)
    {
        for (int i = 0; i < _equipSlots.Length; i++)
            if (element == _equipSlots[i])
                return i;
        return -1;
    }

    void ApplyPartDrop(VisualElement target)
    {
        bool changed = false;
        int targetSlot = GetEquipSlotIndex(target);

        if (_dragSource == DragSource.Library && targetSlot >= 0)
        {
            var targetPart = GameState.Inventory.EquippedParts[targetSlot];
            changed = targetPart == null
                ? GameState.Inventory.EquipPart(_draggedPart, targetSlot)
                : GameState.Inventory.SwapPart(targetSlot, _draggedPart);
        }
        else if (_dragSource == DragSource.EquipSlot)
        {
            if (targetSlot >= 0 && targetSlot != _dragSourceSlot)
            {
                var equipped = GameState.Inventory.EquippedParts;
                (equipped[_dragSourceSlot], equipped[targetSlot]) =
                    (equipped[targetSlot], equipped[_dragSourceSlot]);
                changed = true;
            }
            else if (target == _libraryPanel)
            {
                changed = GameState.Inventory.RemovePart(_dragSourceSlot);
            }
        }

        if (!changed)
            return;

        GameState.Inventory.ApplyToSpecies(GameState.PlayerSpecies);
        _selectedPart = null;
        _selectedCard = null;
        _selectedSlotIndex = -1;
        _hoveredSlotIndex = -1;
        saveDirty = true;
        Refresh();
    }

    void FinishPartDrag()
    {
        if (_root != null && _dragPointerId >= 0 && _root.HasPointerCapture(_dragPointerId))
            _root.ReleasePointer(_dragPointerId);

        ClearActiveDropTarget();
        _dragSourceElement?.RemoveFromClassList("sep-drag-source");
        _dragGhost?.RemoveFromHierarchy();
        _dragGhost = null;
        _dragSourceElement = null;
        _draggedPart = null;
        _dragSourceSlot = -1;
        _dragPointerId = -1;
        _isDraggingPart = false;
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

        // Deselect previous
        _selectedCard?.SetSelected(false);
        _selectedSlotIndex = -1;
        RefreshEquipSlots();

        if (_selectedCard == card)
        {
            // Second click on same card — deselect
            _selectedCard = null;
            _selectedPart = null;
        }
        else
        {
            _selectedCard = card;
            _selectedPart = card.Part;
            card.SetSelected(true);

            // If a slot was previously focused, equip immediately
        }

        RefreshDetailPane(_selectedPart);
    }

    void OnEquipSlotClicked(int slotIndex)
    {
        if (_suppressNextClick)
        {
            _suppressNextClick = false;
            return;
        }

        var currentPart = GameState.Inventory.EquippedParts[slotIndex];

        // If the selected part is already equipped, treat this as a slot move/swap
        // instead of an inventory equip/swap.
        int selectedEquippedIndex = _selectedPart == null
            ? -1
            : Array.IndexOf(GameState.Inventory.EquippedParts, _selectedPart);

        if (selectedEquippedIndex >= 0)
        {
            if (selectedEquippedIndex == slotIndex)
            {
                _selectedSlotIndex = -1;
                _selectedPart = null;
                RefreshEquipSlots();
                RefreshDetailPane(GetActiveDetailPart());
                return;
            }

            var equipped = GameState.Inventory.EquippedParts;
            (equipped[selectedEquippedIndex], equipped[slotIndex]) =
                (equipped[slotIndex], equipped[selectedEquippedIndex]);

            GameState.Inventory.ApplyToSpecies(GameState.PlayerSpecies);
            _selectedCard?.SetSelected(false);
            _selectedCard = null;
            _hoveredSlotIndex = -1;
            _selectedSlotIndex = slotIndex;
            _selectedPart = equipped[slotIndex];
            Refresh();
            saveDirty = true;
            return;
        }

        if (_selectedPart != null && currentPart == null)
        {
            // Equip selected library part into this empty slot
            if (GameState.Inventory.EquipPart(_selectedPart, slotIndex))
            {
                GameState.Inventory.ApplyToSpecies(GameState.PlayerSpecies);
                _selectedCard?.SetSelected(false);
                _selectedCard = null;
                _selectedPart = null;
                _selectedSlotIndex = -1;
                Refresh();
                saveDirty = true;
            }
        }
        else if (_selectedPart != null && currentPart != null)
        {
            // Swap — remove current (costs MP) then equip new part
            if (GameState.Inventory.SwapPart(slotIndex, _selectedPart))
            {
                GameState.Inventory.ApplyToSpecies(GameState.PlayerSpecies);
                _selectedCard?.SetSelected(false);
                _selectedCard = null;
                _selectedPart = null;
                _selectedSlotIndex = -1;
                Refresh();
                saveDirty = true;
            }
        }
        else if (currentPart != null)
        {
            // No library selection — select the equipped part for detail view
            _selectedSlotIndex = slotIndex;
            _hoveredSlotIndex = -1;
            _selectedCard?.SetSelected(false);
            _selectedCard = null;
            _selectedPart = currentPart;
            RefreshEquipSlots();
            RefreshDetailPane(_selectedPart);
            saveDirty = true;
        }
        else
        {
            // Empty slot, nothing selected — just highlight it
            _selectedSlotIndex = (_selectedSlotIndex == slotIndex) ? -1 : slotIndex;
            RefreshEquipSlots();
            RefreshDetailPane(GetActiveDetailPart());
        }
    }

    void OnEquipSlotHoverEnter(int slotIndex)
    {
        _hoveredSlotIndex = slotIndex;

        var hoveredPart = GameState.Inventory.EquippedParts[slotIndex];
        if (hoveredPart != null)
            RefreshDetailPane(hoveredPart);
    }

    void OnEquipSlotHoverLeave(int slotIndex)
    {
        if (_hoveredSlotIndex == slotIndex)
            _hoveredSlotIndex = -1;

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
        OnSave();
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.ShowWorldMap();
        else
            Debug.LogWarning("[SpeciesEditorPanel] ScreenManager.Instance is missing; cannot open World Map.");
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
        if (_hoveredSlotIndex >= 0 && _hoveredSlotIndex < PlayerInventory.EquipSlots)
        {
            var hovered = GameState.Inventory.EquippedParts[_hoveredSlotIndex];
            if (hovered != null)
                return hovered;
        }

        return _selectedPart;
    }

    static string GetEquipSlotGlyph(Part part)
    {
        if (part.Attack > 0) return "ATK";
        if (part.Defense > 0 || part.Health > 0) return "DEF";
        if (part.Forage > 0) return "FOR";
        return "MUT";
    }

    static string GetEquipSlotIconClass(Part part)
    {
        if (part.Attack > 0) return "sep-equip-slot__icon--attack";
        if (part.Defense > 0 || part.Health > 0) return "sep-equip-slot__icon--defense";
        if (part.Forage > 0) return "sep-equip-slot__icon--feeding";
        return "sep-equip-slot__icon--mutation";
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
