using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Full-screen world map screen.  Reads from <see cref="WorldMapData"/> to place
/// encounter nodes on a draggable infinite-ocean canvas.
///
/// Usage:
///   worldMapPanel.Show(gameState.WorldMap);
///   worldMapPanel.OnTravelRequested += node => { … start the encounter … };
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class WorldMapPanel : MonoBehaviour
{
    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the player clicks "Travel Here" on an accessible node.</summary>
    public event Action<WorldMapNode> OnTravelRequested;

    // ── Layout constants ──────────────────────────────────────────────────────

    /// Half the node diameter, used to centre nodes on their grid position.
    const int NodeHalf = 32;

    /// Extra padding around the furthest node so edge nodes are not clipped.
    const int CanvasPadding = 180;

    /// Minimum map dimensions so short runs do not create a cramped viewport.
    const int CanvasMinWidth = 1400;
    const int CanvasMinHeight = 1000;

    /// Pixels per grid-unit when placing nodes relative to the canvas centre.
    const int NodeSpread = 80;

    /// Maximum random pixel offset applied per node for a scattered look.
    const int NodeJitter = 50;

    // ── Private state ─────────────────────────────────────────────────────────

    UIDocument _doc;
    VisualElement _root;

    // Map area
    VisualElement _mapArea;
    VisualElement _canvas;
    VisualElement _playerMarker;
    VisualElement _hoverLineLayer;

    // Hover popup
    VisualElement _hoverPopup;
    Label _hoverName;
    Label _hoverDescription;
    Label _hoverDifficulty;
    Label _hoverCost;
    Button _hoverEnterBtn;
    bool _isHoverPopupHovered;
    IVisualElementScheduledItem _hoverHideTask;

    // Info sidebar
    VisualElement _infoEmpty;
    VisualElement _infoDetail;
    Label _infoName;
    Label _infoType;
    VisualElement _infoEnemyList;
    Label _infoStatus;
    Button _travelBtn;
    Button _openSpeciesBtn;

    // Pan state
    bool _isDragging;
    Vector2 _dragPointerStart;
    Vector2 _panOffset;
    int _canvasWidth;
    int _canvasHeight;

    // Map data
    WorldMapData _mapData;
    WorldMapNode _selectedNode;
    WorldMapNode _hoveredNode;
    WorldMapNode _pinnedNode;

    // Lookup: grid position → visual element
    readonly Dictionary<MapPoint, VisualElement> _nodeElements = new();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        // Map area
        _mapArea = _root.Q<VisualElement>("wmp-map-area");

        // Info panel
        _infoEmpty = _root.Q<VisualElement>("wmp-info-empty");
        _infoDetail = _root.Q<VisualElement>("wmp-info-detail");
        _infoName = _root.Q<Label>("wmp-info-name");
        _infoType = _root.Q<Label>("wmp-info-type");
        _infoEnemyList = _root.Q<VisualElement>("wmp-info-enemy-list");
        _infoStatus = _root.Q<Label>("wmp-info-status");
        _travelBtn = _root.Q<Button>("wmp-travel-btn");
        _openSpeciesBtn = _root.Q<Button>("wmp-open-species-btn");

        _travelBtn.clicked += OnTravelClicked;
        if (_openSpeciesBtn != null)
            _openSpeciesBtn.clicked += OnOpenSpeciesEditorClicked;

        ShowEmpty();
        Hide();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Populate the map from <paramref name="mapData"/> and make it visible.
    /// </summary>
    public void Show(WorldMapData mapData)
    {
        _mapData = mapData ?? throw new ArgumentNullException(nameof(mapData));
        _selectedNode = null;
        _hoveredNode = null;
        _pinnedNode = null;
        ShowEmpty();

        BuildCanvas();
        CenterOnPlayerNode();

        _root.style.display = DisplayStyle.Flex;
    }

    public void Hide() => _root.style.display = DisplayStyle.None;

    // ── Canvas construction ───────────────────────────────────────────────────

    void BuildCanvas()
    {
        // Remove previous canvas if rebuilding.
        _canvas?.RemoveFromHierarchy();
        _nodeElements.Clear();
        _hoveredNode = null;
        _pinnedNode = null;

        RecalculateCanvasSize();

        _canvas = new VisualElement { name = "wmp-canvas" };
        _canvas.AddToClassList("wmp-canvas");
        _canvas.style.width = _canvasWidth;
        _canvas.style.height = _canvasHeight;
        _mapArea.Add(_canvas);

        // Hover path line layer.
        _hoverLineLayer = new VisualElement { name = "wmp-hover-line-layer" };
        _hoverLineLayer.AddToClassList("wmp-hover-line-layer");
        _hoverLineLayer.style.width = _canvasWidth;
        _hoverLineLayer.style.height = _canvasHeight;
        _hoverLineLayer.generateVisualContent += DrawHoverLine;
        _canvas.Add(_hoverLineLayer);

        // Place node elements.
        foreach (var node in _mapData.Nodes.Values)
        {
            var el = BuildNodeElement(node);
            _nodeElements[node.Position] = el;
            _canvas.Add(el);
        }

        // Player marker (on top).
        _playerMarker = BuildPlayerMarker();
        _canvas.Add(_playerMarker);
        PositionPlayerMarker();

        BuildHoverPopup();

        // Drag events on the map area (not the canvas itself, so we always
        // receive events even when the canvas doesn't fill the area).
        _mapArea.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        _mapArea.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        _mapArea.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        _mapArea.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
        _mapArea.RegisterCallback<PointerDownEvent>(OnPointerDown);
        _mapArea.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        _mapArea.RegisterCallback<PointerUpEvent>(OnPointerUp);
        _mapArea.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
    }

    void BuildHoverPopup()
    {
        _hoverPopup = new VisualElement { name = "wmp-hover-popup" };
        _hoverPopup.AddToClassList("wmp-hover-popup");

        _hoverName = new Label("NODE");
        _hoverName.AddToClassList("wmp-hover-popup__name");
        _hoverPopup.Add(_hoverName);

        _hoverDescription = new Label(string.Empty);
        _hoverDescription.AddToClassList("wmp-hover-popup__description");
        _hoverPopup.Add(_hoverDescription);

        _hoverDifficulty = new Label(string.Empty);
        _hoverDifficulty.AddToClassList("wmp-hover-popup__meta");
        _hoverPopup.Add(_hoverDifficulty);

        _hoverCost = new Label(string.Empty);
        _hoverCost.AddToClassList("wmp-hover-popup__meta");
        _hoverPopup.Add(_hoverCost);

        _hoverEnterBtn = new Button(OnHoverEnterClicked) { text = "ENTER" };
        _hoverEnterBtn.AddToClassList("wmp-hover-popup__enter-btn");
        _hoverPopup.Add(_hoverEnterBtn);

        _hoverPopup.RegisterCallback<PointerEnterEvent>(_ =>
        {
            _isHoverPopupHovered = true;
            CancelHoverHideTask();
        });

        _hoverPopup.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            _isHoverPopupHovered = false;
            ScheduleHoverHide();
        });

        _canvas.Add(_hoverPopup);
        HideHoverPopup(clearPinned: true);
    }

    void RecalculateCanvasSize()
    {
        float maxAbsX = 0f;
        float maxAbsY = 0f;

        foreach (var node in _mapData.Nodes.Values)
        {
            Vector2 relative = RelativeScatterOffset(node);
            int half = NodeHalfForType(node.Type);

            maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(relative.x) + half);
            maxAbsY = Mathf.Max(maxAbsY, Mathf.Abs(relative.y) + half);
        }

        _canvasWidth = Mathf.Max(CanvasMinWidth, Mathf.CeilToInt(maxAbsX * 2f) + CanvasPadding * 2);
        _canvasHeight = Mathf.Max(CanvasMinHeight, Mathf.CeilToInt(maxAbsY * 2f) + CanvasPadding * 2);
    }

    // ── Node element factory ──────────────────────────────────────────────────

    VisualElement BuildNodeElement(WorldMapNode node)
    {
        var el = new VisualElement();
        el.AddToClassList("wmp-node");
        el.AddToClassList(NodeTypeClass(node.Type));

        if (node.IsVisited)
            el.AddToClassList("wmp-node--visited");
        else if (!node.IsAccessible)
            el.AddToClassList("wmp-node--inaccessible");

        // Icon label (Unicode glyph as a stand-in for a sprite).
        var icon = new Label(NodeGlyph(node.Type));
        icon.AddToClassList("wmp-node__icon");
        el.Add(icon);

        // Name label positioned below the circle.
        var label = new Label(node.DisplayName);
        label.AddToClassList("wmp-node__label");
        el.Add(label);

        // Position on canvas.
        Vector2 pos = CanvasPos(node);
        int half = NodeHalfForType(node.Type);
        el.style.left = pos.x - half;
        el.style.top = pos.y - half;

        el.RegisterCallback<PointerEnterEvent>(_ => OnNodePointerEnter(node));
        el.RegisterCallback<PointerLeaveEvent>(_ => OnNodePointerLeave(node));
        el.RegisterCallback<ClickEvent>(_ => SelectNode(node));
        return el;
    }

    void OnNodePointerEnter(WorldMapNode node)
    {
        _hoveredNode = node;
        CancelHoverHideTask();
        RefreshHoverPresentation();
    }

    void OnNodePointerLeave(WorldMapNode node)
    {
        if (_hoveredNode != node) return;

        _hoveredNode = null;

        if (_pinnedNode != null)
        {
            RefreshHoverPresentation();
            return;
        }

        if (_isHoverPopupHovered) return;
        ScheduleHoverHide();
    }

    void ScheduleHoverHide()
    {
        CancelHoverHideTask();
        _hoverHideTask = _root.schedule.Execute(() =>
        {
            if (_isHoverPopupHovered) return;
            if (_pinnedNode != null)
            {
                RefreshHoverPresentation();
                return;
            }

            HideHoverPopup();
        });
        _hoverHideTask.ExecuteLater(90);
    }

    void CancelHoverHideTask()
    {
        _hoverHideTask?.Pause();
        _hoverHideTask = null;
    }

    void HideHoverPopup(bool clearPinned = false)
    {
        _hoveredNode = null;

        if (clearPinned)
            _pinnedNode = null;

        RefreshHoverPresentation();
    }

    void RefreshHoverPresentation()
    {
        WorldMapNode node = _hoveredNode ?? _pinnedNode;

        if (node == null)
        {
            if (_hoverPopup != null)
                _hoverPopup.style.display = DisplayStyle.None;
            _hoverLineLayer?.MarkDirtyRepaint();
            return;
        }

        PopulateHoverPopup(node);
        PositionHoverPopup(node);
        _hoverPopup.style.display = DisplayStyle.Flex;
        _hoverLineLayer?.MarkDirtyRepaint();
    }

    void PopulateHoverPopup(WorldMapNode node)
    {
        _hoverName.text = node.DisplayName;
        _hoverDescription.text = GetNodeDescription(node);
        _hoverDifficulty.text = $"Difficulty: {GetNodeDifficultyLabel(node)}";

        int hops = ComputeTravelHops(node);
        _hoverCost.text = hops < 0 ? "Cost to reach: Unreachable" : $"Cost to reach: {hops} hop{(hops == 1 ? string.Empty : "s")}";

        bool canTravel = node.IsAccessible && !node.IsVisited && node.Type != WorldMapNodeType.Start;
        _hoverEnterBtn.SetEnabled(canTravel);
    }

    void PositionHoverPopup(WorldMapNode node)
    {
        Vector2 center = CanvasPosCenter(node);
        const float offsetX = 46f;
        const float offsetY = -110f;
        const float popupW = 300f;
        const float popupH = 230f;

        float left = center.x + offsetX;
        float top = center.y + offsetY;

        left = Mathf.Clamp(left, 8f, _canvasWidth - popupW - 8f);
        top = Mathf.Clamp(top, 8f, _canvasHeight - popupH - 8f);

        _hoverPopup.style.left = left;
        _hoverPopup.style.top = top;
    }

    void OnHoverEnterClicked()
    {
        WorldMapNode target = _hoveredNode ?? _pinnedNode;
        if (target == null) return;

        SelectNode(target);

        bool canTravel = target.IsAccessible && !target.IsVisited && target.Type != WorldMapNodeType.Start;
        if (canTravel)
            OnTravelRequested?.Invoke(target);
    }

    VisualElement BuildPlayerMarker()
    {
        var marker = new VisualElement();
        marker.AddToClassList("wmp-player-marker");

        var lbl = new Label("▲");
        lbl.AddToClassList("wmp-player-marker__label");
        marker.Add(lbl);

        return marker;
    }

    void PositionPlayerMarker()
    {
        if (_mapData?.PlayerNode == null) return;

        Vector2 center = CanvasPosCenter(_mapData.PlayerNode);
        // Offset slightly above the node center so the triangle apex points at it.
        _playerMarker.style.left = center.x - 18;
        _playerMarker.style.top = center.y - 50;
    }

    // ── Node selection + info panel ───────────────────────────────────────────

    void SelectNode(WorldMapNode node)
    {
        // Deselect previous.
        if (_selectedNode != null && _nodeElements.TryGetValue(_selectedNode.Position, out var prev))
            prev.RemoveFromClassList("wmp-node--selected");

        _selectedNode = node;
        _pinnedNode = node;

        if (_nodeElements.TryGetValue(node.Position, out var el))
            el.AddToClassList("wmp-node--selected");

        PopulateInfoPanel(node);
        RefreshHoverPresentation();
    }

    void PopulateInfoPanel(WorldMapNode node)
    {
        _infoEmpty.style.display = DisplayStyle.None;
        _infoDetail.style.display = DisplayStyle.Flex;
        _infoDetail.RemoveFromClassList("wmp-info-detail--hidden");

        _infoName.text = node.DisplayName;

        // Type badge.
        _infoType.text = node.IsVisited ? "CLEARED" : node.Type.ToString().ToUpper();
        _infoType.ClearClassList();
        _infoType.AddToClassList("wmp-info-type-badge");
        _infoType.AddToClassList(node.IsVisited ? "wmp-info-badge--visited" : TypeBadgeClass(node.Type));

        // Enemy list.
        _infoEnemyList.Clear();
        if (node.Encounter?.EnemyGroup?.Members != null)
        {
            foreach (var species in node.Encounter.EnemyGroup.Members)
            {
                var row = new VisualElement();
                row.AddToClassList("wmp-info-enemy-entry");

                var dot = new VisualElement();
                dot.AddToClassList("wmp-info-enemy-dot");
                row.Add(dot);

                var nameLbl = new Label(species.Name);
                nameLbl.AddToClassList("wmp-info-enemy-name");
                row.Add(nameLbl);

                var stats = new Label($"HP {species.BaseHealth}  ATK {species.BaseAttack}");
                stats.AddToClassList("wmp-info-enemy-stats");
                row.Add(stats);

                _infoEnemyList.Add(row);
            }
        }
        else if (node.Type == WorldMapNodeType.Start)
        {
            var hint = new Label("Safe zone — no combat here.");
            hint.AddToClassList("wmp-info-enemy-name");
            _infoEnemyList.Add(hint);
        }

        // Status text.
        if (node.IsVisited)
        {
            _infoStatus.text = "✓ Already cleared";
            _infoStatus.RemoveFromClassList("wmp-info-status--inaccessible");
        }
        else if (!node.IsAccessible)
        {
            _infoStatus.text = "✗ Not yet reachable";
            _infoStatus.AddToClassList("wmp-info-status--inaccessible");
        }
        else
        {
            _infoStatus.text = "";
        }

        // Travel button.
        bool canTravel = node.IsAccessible && !node.IsVisited && node.Type != WorldMapNodeType.Start;
        _travelBtn.SetEnabled(canTravel);
    }

    void ShowEmpty()
    {
        _infoEmpty.style.display = DisplayStyle.Flex;
        _infoDetail.AddToClassList("wmp-info-detail--hidden");
        _infoDetail.style.display = DisplayStyle.None;
    }

    void OnTravelClicked()
    {
        if (_selectedNode == null) return;
        if (!_selectedNode.IsAccessible || _selectedNode.IsVisited) return;

        OnTravelRequested?.Invoke(_selectedNode);
    }

    void OnOpenSpeciesEditorClicked()
    {
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.ShowSpeciesEditor();
        else
            Debug.LogWarning("[WorldMapPanel] ScreenManager.Instance is missing; cannot open Species Editor.");
    }

    void DrawHoverLine(MeshGenerationContext ctx)
    {
        WorldMapNode target = _hoveredNode ?? _pinnedNode;
        if (target == null || _mapData?.PlayerNode == null) return;
        if (target == _mapData.PlayerNode) return;

        var painter = ctx.painter2D;
        Vector2 from = CanvasPosCenter(_mapData.PlayerNode);
        Vector2 to = CanvasPosCenter(target);

        painter.strokeColor = new Color(0.50f, 0.82f, 0.97f, 0.90f);
        painter.lineWidth = 3f;
        painter.BeginPath();
        painter.MoveTo(from);
        painter.LineTo(to);
        painter.Stroke();
    }

    // ── Drag / Pan ────────────────────────────────────────────────────────────

    void OnPointerDown(PointerDownEvent e)
    {
        _isDragging = true;
        _dragPointerStart = e.position;
        _mapArea.CapturePointer(e.pointerId);
        e.StopPropagation();
    }

    void OnPointerMove(PointerMoveEvent e)
    {
        if (!_isDragging) return;

        Vector2 current = e.position;
        Vector2 delta = current - _dragPointerStart;
        _dragPointerStart = current;

        _panOffset += delta;
        ClampPan();
        ApplyPan();
    }

    void OnPointerUp(PointerUpEvent e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        _mapArea.ReleasePointer(e.pointerId);
    }

    void OnPointerCancel(PointerCancelEvent e)
    {
        _isDragging = false;
        _mapArea.ReleasePointer(e.pointerId);
    }

    void ClampPan()
    {
        float vpW = _mapArea.resolvedStyle.width;
        float vpH = _mapArea.resolvedStyle.height;

        // Allow scrolling to any part of the canvas but no further.
        _panOffset.x = Mathf.Clamp(_panOffset.x, vpW - _canvasWidth, 0f);
        _panOffset.y = Mathf.Clamp(_panOffset.y, vpH - _canvasHeight, 0f);
    }

    void ApplyPan()
    {
        _canvas.style.left = _panOffset.x;
        _canvas.style.top = _panOffset.y;
    }

    void CenterOnPlayerNode()
    {
        // Defer until the map area has resolved its layout.
        _mapArea.RegisterCallback<GeometryChangedEvent>(OnFirstLayout);
    }

    void OnFirstLayout(GeometryChangedEvent e)
    {
        _mapArea.UnregisterCallback<GeometryChangedEvent>(OnFirstLayout);

        if (_mapData?.PlayerNode == null) return;

        Vector2 nodeCenter = CanvasPosCenter(_mapData.PlayerNode);
        float vpW = _mapArea.resolvedStyle.width;
        float vpH = _mapArea.resolvedStyle.height;

        // Pan so the player node sits at the viewport centre.
        _panOffset = new Vector2(vpW / 2f - nodeCenter.x, vpH / 2f - nodeCenter.y);
        ClampPan();
        ApplyPan();
    }

    // ── Position helpers ──────────────────────────────────────────────────────

    /// Canvas-local pixel centre of a node.
    /// The start node lands exactly at the canvas centre; all others are offset
    /// by their grid distance from start (scaled by <see cref="NodeSpread"/>)
    /// plus a deterministic per-node jitter seeded by the grid position.
    Vector2 CanvasPos(WorldMapNode node)
    {
        float cx = _canvasWidth / 2f;
        float cy = _canvasHeight / 2f;

        return new Vector2(cx, cy) + RelativeScatterOffset(node);
    }

    Vector2 RelativeScatterOffset(WorldMapNode node)
    {
        if (node.Type == WorldMapNodeType.Start)
            return Vector2.zero;

        int dx = node.Position.X - _mapData.StartNode.Position.X;
        int dy = node.Position.Y - _mapData.StartNode.Position.Y;

        float x = dx * NodeSpread;
        float y = dy * NodeSpread;

        var rng = new System.Random(node.Position.GetHashCode());
        x += (float)(rng.NextDouble() * 2 - 1) * NodeJitter;
        y += (float)(rng.NextDouble() * 2 - 1) * NodeJitter;

        return new Vector2(x, y);
    }

    Vector2 CanvasPosCenter(WorldMapNode node) => CanvasPos(node);

    int ComputeTravelHops(WorldMapNode target)
    {
        if (_mapData?.PlayerNode == null || target == null) return -1;
        if (target.Position.Equals(_mapData.PlayerNode.Position)) return 0;

        var queue = new Queue<(MapPoint Pos, int Dist)>();
        var visited = new HashSet<MapPoint>();
        queue.Enqueue((_mapData.PlayerNode.Position, 0));
        visited.Add(_mapData.PlayerNode.Position);

        while (queue.Count > 0)
        {
            var (pos, dist) = queue.Dequeue();
            if (!_mapData.Nodes.TryGetValue(pos, out var node)) continue;

            foreach (var next in node.Connections)
            {
                if (visited.Contains(next)) continue;
                if (next.Equals(target.Position)) return dist + 1;

                visited.Add(next);
                queue.Enqueue((next, dist + 1));
            }
        }

        return -1;
    }

    static string GetNodeDescription(WorldMapNode node)
    {
        int enemyCount = node.Encounter?.EnemyGroup?.Members?.Count ?? 0;
        return node.Type switch
        {
            WorldMapNodeType.Start => "Home waters. Regroup and prepare before venturing deeper.",
            WorldMapNodeType.Boss => "An apex threat controls this territory. Expect a decisive battle.",
            WorldMapNodeType.Elite => $"A dangerous rival force patrols this zone. Enemies spotted: {enemyCount}.",
            _ => $"Open-water skirmish area with roaming predators. Enemies spotted: {enemyCount}.",
        };
    }

    static string GetNodeDifficultyLabel(WorldMapNode node)
    {
        int score = node.Type switch
        {
            WorldMapNodeType.Start => 0,
            WorldMapNodeType.Combat => 2,
            WorldMapNodeType.Elite => 4,
            WorldMapNodeType.Boss => 6,
            _ => 2,
        };

        if (score <= 1) return "Very Low";
        if (score <= 3) return "Low";
        if (score <= 5) return "Medium";
        if (score <= 7) return "High";
        return "Very High";
    }

    static int NodeHalfForType(WorldMapNodeType t) => t switch
    {
        WorldMapNodeType.Boss => 40,
        WorldMapNodeType.Elite => 36,
        _ => NodeHalf,
    };

    // ── USS class helpers ─────────────────────────────────────────────────────

    static string NodeTypeClass(WorldMapNodeType t) => t switch
    {
        WorldMapNodeType.Start => "wmp-node--start",
        WorldMapNodeType.Combat => "wmp-node--combat",
        WorldMapNodeType.Elite => "wmp-node--elite",
        WorldMapNodeType.Boss => "wmp-node--boss",
        _ => "wmp-node--combat",
    };

    static string TypeBadgeClass(WorldMapNodeType t) => t switch
    {
        WorldMapNodeType.Boss => "wmp-info-badge--boss",
        WorldMapNodeType.Elite => "wmp-info-badge--elite",
        WorldMapNodeType.Start => "wmp-info-badge--start",
        _ => string.Empty,
    };

    static string NodeGlyph(WorldMapNodeType t) => t switch
    {
        WorldMapNodeType.Start => "⚓",
        WorldMapNodeType.Boss => "☠",
        WorldMapNodeType.Elite => "★",
        _ => "⚔",
    };
}
