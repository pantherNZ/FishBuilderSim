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
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Tooltip("Leave null to load from Resources/UI/WorldMapPanel.")]
    public VisualTreeAsset OverrideUxml;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the player clicks "Travel Here" on an accessible node.</summary>
    public event Action<WorldMapNode> OnTravelRequested;

    // ── Layout constants ──────────────────────────────────────────────────────

    /// Half the node diameter, used to centre nodes on their grid position.
    const int NodeHalf = 32;

    /// Extra cells of blank space at the canvas borders so edge nodes aren't clipped.
    const int OriginPad = 2;

    // ── Private state ─────────────────────────────────────────────────────────

    UIDocument _doc;
    VisualElement _root;

    // Map area
    VisualElement _mapArea;
    VisualElement _canvas;
    VisualElement _edgesLayer;
    VisualElement _playerMarker;

    // Info sidebar
    VisualElement _infoEmpty;
    VisualElement _infoDetail;
    Label _infoName;
    Label _infoType;
    VisualElement _infoEnemyList;
    Label _infoStatus;
    Button _travelBtn;

    // Pan state
    bool _isDragging;
    Vector2 _dragPointerStart;
    Vector2 _panOffset;
    int _canvasWidth;
    int _canvasHeight;

    // Map data
    WorldMapData _mapData;
    WorldMapNode _selectedNode;

    // Lookup: grid position → visual element
    readonly Dictionary<MapPoint, VisualElement> _nodeElements = new();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _doc = GetComponent<UIDocument>();

        VisualTreeAsset asset = OverrideUxml
            ? OverrideUxml
            : Resources.Load<VisualTreeAsset>("UI/WorldMapPanel");

        if (asset == null)
        {
            Debug.LogError("[WorldMapPanel] Could not load WorldMapPanel.uxml from Resources/UI/");
            return;
        }

        _root = asset.CloneTree();
        _doc.rootVisualElement.Add(_root);

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

        _travelBtn.clicked += OnTravelClicked;

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

        // Work out how big the canvas needs to be.
        int maxCol = 0, maxRow = 0;
        foreach (var node in _mapData.Nodes.Values)
        {
            if (node.Position.X > maxCol) maxCol = node.Position.X;
            if (node.Position.Y > maxRow) maxRow = node.Position.Y;
        }

        _canvasWidth = (maxCol + OriginPad * 2 + 1) * WorldMapData.CellSize;
        _canvasHeight = (maxRow + OriginPad * 2 + 1) * WorldMapData.CellSize;

        _canvas = new VisualElement { name = "wmp-canvas" };
        _canvas.AddToClassList("wmp-canvas");
        _canvas.style.width = _canvasWidth;
        _canvas.style.height = _canvasHeight;
        _mapArea.Add(_canvas);

        // Edges layer behind nodes (drawn via Painter2D).
        _edgesLayer = new VisualElement { name = "wmp-edges-layer" };
        _edgesLayer.AddToClassList("wmp-edges-layer");
        _edgesLayer.style.width = _canvasWidth;
        _edgesLayer.style.height = _canvasHeight;
        _edgesLayer.generateVisualContent += DrawEdges;
        _canvas.Add(_edgesLayer);

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

        // Drag events on the map area (not the canvas itself, so we always
        // receive events even when the canvas doesn't fill the area).
        _mapArea.RegisterCallback<PointerDownEvent>(OnPointerDown);
        _mapArea.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        _mapArea.RegisterCallback<PointerUpEvent>(OnPointerUp);
        _mapArea.RegisterCallback<PointerCancelEvent>(_ => _isDragging = false);
    }

    // ── Edge drawing ──────────────────────────────────────────────────────────

    void DrawEdges(MeshGenerationContext ctx)
    {
        var painter = ctx.painter2D;

        foreach (var node in _mapData.Nodes.Values)
        {
            foreach (var connPos in node.Connections)
            {
                // Only draw each edge once (from the node with the smaller X).
                if (connPos.X <= node.Position.X) continue;

                if (!_mapData.Nodes.TryGetValue(connPos, out var connNode)) continue;

                Vector2 from = CanvasPosCenter(node);
                Vector2 to = CanvasPosCenter(connNode);

                bool dim = node.IsVisited && connNode.IsVisited;
                painter.strokeColor = dim
                    ? new Color(0.15f, 0.35f, 0.25f, 0.55f)
                    : new Color(0.20f, 0.50f, 0.70f, 0.50f);

                painter.lineWidth = 2f;
                painter.BeginPath();
                painter.MoveTo(from);
                painter.LineTo(to);
                painter.Stroke();
            }
        }
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

        el.RegisterCallback<ClickEvent>(_ => SelectNode(node));
        return el;
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

        if (_nodeElements.TryGetValue(node.Position, out var el))
            el.AddToClassList("wmp-node--selected");

        PopulateInfoPanel(node);
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

    /// Canvas-local pixel position of a node's top-left for absolute placement.
    Vector2 CanvasPos(WorldMapNode node)
    {
        return new Vector2(
            (node.Position.X + OriginPad) * WorldMapData.CellSize,
            (node.Position.Y + OriginPad) * WorldMapData.CellSize);
    }

    /// Canvas-local pixel position of a node's visual centre.
    Vector2 CanvasPosCenter(WorldMapNode node)
    {
        int half = NodeHalfForType(node.Type);
        Vector2 pos = CanvasPos(node);
        return new Vector2(pos.x + half, pos.y + half);
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
