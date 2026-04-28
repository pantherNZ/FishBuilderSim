using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Coordinates top-level UI screens and game flow transitions.
/// Panels request transitions through this manager instead of directly
/// referencing each other.
/// </summary>
public class ScreenManager : MonoBehaviour
{
    public static ScreenManager Instance { get; private set; }

    [Header("Panels")]
    public SpeciesEditorPanel SpeciesEditorPanel;
    public WorldMapPanel WorldMapPanel;
    public BattlePanel BattlePanel;
    public CardPickerPanel CardPickerPanel;

    [Header("State")]
    [Tooltip("Optional shared state. If null, this is sourced from SpeciesEditorPanel or created at runtime.")]
    public GameState GameState;

    // ── Active battle state ──────────────────────────────────────────────────
    WorldMapNode _activeBattleNode;
    SpeciesGroup _playerBattleGroup;
    SpeciesGroup _enemyBattleGroup;
    int _battleRound;
    bool _battleAwaitingPlayerStep;
    bool _battleRunning;
    Species _playerDefending;
    Species _enemyDefending;

    const int DefendBonus = 2;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        ResolvePanelReferences();
        InitializeSharedState();
        BindPanelEvents();

        ShowSpeciesEditor();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (WorldMapPanel != null)
            WorldMapPanel.OnTravelRequested -= HandleTravelRequested;
        if (BattlePanel != null)
            BattlePanel.OnBeginClicked -= HandleBattleStepRequested;
        if (CardPickerPanel != null)
            CardPickerPanel.OnPicked -= HandleRewardPicked;
    }

    public void ShowSpeciesEditor()
    {
        _battleRunning = false;
        HideAllPanels();

        if (SpeciesEditorPanel == null)
        {
            Debug.LogWarning("[ScreenManager] SpeciesEditorPanel is not assigned.");
            return;
        }

        SpeciesEditorPanel.GameState = GameState;
        SpeciesEditorPanel.Show();
    }

    public void ShowWorldMap()
    {
        if (GameState == null)
        {
            Debug.LogWarning("[ScreenManager] GameState is null; cannot show world map.");
            return;
        }

        // Keep species stats in sync with currently equipped parts.
        GameState.Inventory.ApplyToSpecies(GameState.PlayerSpecies);

        HideAllPanels();
        WorldMapPanel?.Show(GameState.WorldMap);
    }

    public void ShowBattle(WorldMapNode destination)
    {
        if (destination == null)
        {
            Debug.LogWarning("[ScreenManager] Cannot show battle for a null destination node.");
            return;
        }

        if (GameState == null)
        {
            Debug.LogWarning("[ScreenManager] GameState is null; cannot show battle.");
            return;
        }

        if (destination.Encounter == null)
        {
            Debug.LogWarning($"[ScreenManager] Node '{destination.DisplayName}' has no encounter.");
            return;
        }

        GameState.WorldMap.MovePlayerTo(destination);
        GameState.Inventory.ApplyToSpecies(GameState.PlayerSpecies);

        var data = BuildBattleData(destination);
        StartBattle(data, destination);

        HideAllPanels();
        BattlePanel?.Show(data);
        BattlePanel?.SetStepControlsEnabled(true);
        BattlePanel?.AppendCombatLog($"Encounter started: {destination.DisplayName}");
        BattlePanel?.AppendCombatLog($"Round {_battleRound} - choose one species and an action.");
    }

    void ResolvePanelReferences()
    {
        SpeciesEditorPanel ??= FindAnyObjectByType<SpeciesEditorPanel>();
        WorldMapPanel ??= FindAnyObjectByType<WorldMapPanel>();
        BattlePanel ??= FindAnyObjectByType<BattlePanel>();
        CardPickerPanel ??= FindAnyObjectByType<CardPickerPanel>();
    }

    void InitializeSharedState()
    {
        if (GameState == null && SpeciesEditorPanel != null)
            GameState = SpeciesEditorPanel.GameState;

        GameState ??= new GameState();

        if (SpeciesEditorPanel != null)
            SpeciesEditorPanel.GameState = GameState;
    }

    void BindPanelEvents()
    {
        if (WorldMapPanel != null)
        {
            // Avoid duplicate subscription if Start is called after a domain reload edge-case.
            WorldMapPanel.OnTravelRequested -= HandleTravelRequested;
            WorldMapPanel.OnTravelRequested += HandleTravelRequested;
        }

        if (BattlePanel != null)
        {
            BattlePanel.OnBeginClicked -= HandleBattleStepRequested;
            BattlePanel.OnBeginClicked += HandleBattleStepRequested;
        }

        if (CardPickerPanel != null)
            CardPickerPanel.OnPicked -= HandleRewardPicked;
    }

    void HandleTravelRequested(WorldMapNode node)
    {
        ShowBattle(node);
    }

    void HideAllPanels()
    {
        SpeciesEditorPanel?.Hide();
        WorldMapPanel?.Hide();
        BattlePanel?.Hide();
        CardPickerPanel?.Hide();
    }

    void StartBattle(BattleData data, WorldMapNode destination)
    {
        _activeBattleNode = destination;
        _playerBattleGroup = new SpeciesGroup("Player", data.PlayerGroup ?? new List<Species>());
        _enemyBattleGroup = new SpeciesGroup("Enemy", data.EnemyGroup ?? new List<Species>());
        _playerBattleGroup.Initialize();
        _enemyBattleGroup.Initialize();

        _battleRound = 1;
        _battleAwaitingPlayerStep = true;
        _battleRunning = true;
        _playerDefending = null;
        _enemyDefending = null;
    }

    void HandleBattleStepRequested(BattleStepRequest request)
    {
        if (!_battleRunning || !_battleAwaitingPlayerStep) return;
        if (request?.Actor == null || !request.Actor.IsAlive)
        {
            BattlePanel?.AppendCombatLog("Select a living player species.");
            BattlePanel?.SetSelectableSpecies(_playerBattleGroup?.Alive.ToList());
            return;
        }

        RunPlayerStep(request);
        BattlePanel?.RefreshSpeciesVisuals();
        BattlePanel?.RefreshHealthBars();

        if (ResolveBattleEndIfAny()) return;

        _battleAwaitingPlayerStep = false;
        BattlePanel?.SetRoundAndTurn(_battleRound, false);
        BattlePanel?.SetStepControlsEnabled(false);

        RunEnemyStep();
        BattlePanel?.RefreshSpeciesVisuals();
        BattlePanel?.RefreshHealthBars();

        if (ResolveBattleEndIfAny()) return;

        _battleRound++;
        _battleAwaitingPlayerStep = true;
        BattlePanel?.SetRoundAndTurn(_battleRound, true);
        BattlePanel?.SetSelectableSpecies(_playerBattleGroup.Alive.ToList());
        BattlePanel?.SetStepControlsEnabled(true);
        BattlePanel?.AppendCombatLog($"Round {_battleRound} - choose one species and an action.");
    }

    void RunPlayerStep(BattleStepRequest request)
    {
        var actor = request.Actor;
        BattlePanel?.AppendCombatLog($"Round {_battleRound} | Player: {actor.Name} uses {request.Action}.");

        switch (request.Action)
        {
            case BattleStepAction.Attack:
                {
                    var target = actor.PickTarget(_enemyBattleGroup.Alive);
                    if (target == null)
                    {
                        BattlePanel?.AppendCombatLog("No enemy target available.");
                        break;
                    }

                    int defendBonus = target == _enemyDefending ? DefendBonus : 0;
                    int damage = ApplyAttack(actor, target, defendBonus);
                    if (damage <= 0)
                        BattlePanel?.AppendCombatLog($"{actor.Name} could not damage {target.Name}.");
                    else
                        BattlePanel?.AppendCombatLog($"{actor.Name} hit {target.Name} for {damage} damage.");

                    _enemyDefending = null;
                    break;
                }
            case BattleStepAction.Forage:
                {
                    int before = actor.CurrentSize;
                    actor.ApplyForage();
                    BattlePanel?.AppendCombatLog($"{actor.Name} foraged and gained {Mathf.Max(0, actor.CurrentSize - before)} size.");
                    _enemyDefending = null;
                    break;
                }
            case BattleStepAction.Defend:
                {
                    _playerDefending = actor;
                    _enemyDefending = null;
                    BattlePanel?.AppendCombatLog($"{actor.Name} braces for impact (+{DefendBonus} defense this enemy action).");
                    break;
                }
        }
    }

    void RunEnemyStep()
    {
        var enemy = _enemyBattleGroup.Alive.FirstOrDefault();
        if (enemy == null) return;

        BattleStepAction action;
        if (enemy.Attack > 0 && _playerBattleGroup.HasAlive)
            action = BattleStepAction.Attack;
        else if (enemy.Forage > 0)
            action = BattleStepAction.Forage;
        else
            action = BattleStepAction.Defend;

        BattlePanel?.AppendCombatLog($"Round {_battleRound} | Enemy: {enemy.Name} uses {action}.");

        switch (action)
        {
            case BattleStepAction.Attack:
                {
                    var target = enemy.PickTarget(_playerBattleGroup.Alive);
                    if (target == null)
                    {
                        BattlePanel?.AppendCombatLog("Enemy found no valid target.");
                        break;
                    }

                    int defendBonus = target == _playerDefending ? DefendBonus : 0;
                    int damage = ApplyAttack(enemy, target, defendBonus);
                    if (damage <= 0)
                        BattlePanel?.AppendCombatLog($"{enemy.Name} could not damage {target.Name}.");
                    else
                        BattlePanel?.AppendCombatLog($"{enemy.Name} hit {target.Name} for {damage} damage.");

                    _playerDefending = null;
                    break;
                }
            case BattleStepAction.Forage:
                {
                    int before = enemy.CurrentSize;
                    enemy.ApplyForage();
                    BattlePanel?.AppendCombatLog($"{enemy.Name} foraged and gained {Mathf.Max(0, enemy.CurrentSize - before)} size.");
                    _playerDefending = null;
                    break;
                }
            case BattleStepAction.Defend:
                {
                    _enemyDefending = enemy;
                    _playerDefending = null;
                    BattlePanel?.AppendCombatLog($"{enemy.Name} takes a defensive stance.");
                    break;
                }
        }
    }

    int ApplyAttack(Species attacker, Species target, int defenseBonus)
    {
        if (attacker == null || target == null) return 0;
        if (attacker.Attack <= 0 || !attacker.CanAttack) return 0;

        if (attacker.Size < target.Size)
        {
            BattlePanel?.AppendCombatLog($"{attacker.Name} is too small to attack {target.Name}.");
            return 0;
        }

        int before = target.CurrentHealth;
        if (defenseBonus > 0)
            target.BaseDefense += defenseBonus;

        attacker.AttackTarget(target);

        if (defenseBonus > 0)
            target.BaseDefense -= defenseBonus;

        return Mathf.Max(0, before - target.CurrentHealth);
    }

    bool ResolveBattleEndIfAny()
    {
        if (_enemyBattleGroup != null && !_enemyBattleGroup.HasAlive)
        {
            _battleRunning = false;
            BattlePanel?.AppendCombatLog($"Victory on round {_battleRound}!");
            GameState.HandleEncounterResult(true);
            ShowRewardPicker();
            return true;
        }

        if (_playerBattleGroup != null && !_playerBattleGroup.HasAlive)
        {
            _battleRunning = false;
            BattlePanel?.AppendCombatLog("Your species were defeated.");
            GameState.HandleEncounterResult(false);
            ShowSpeciesEditor();
            return true;
        }

        return false;
    }

    void ShowRewardPicker()
    {
        if (CardPickerPanel == null || GameState.PendingRewardChoices == null || GameState.PendingRewardChoices.Count == 0)
        {
            ShowSpeciesEditor();
            return;
        }

        HideAllPanels();

        CardPickerPanel.OnPicked -= HandleRewardPicked;
        CardPickerPanel.OnPicked += HandleRewardPicked;
        CardPickerPanel.Show(new CardPickerData
        {
            Title = "CHOOSE A REWARD",
            Subtitle = $"Pick 1 of {GameState.PendingRewardChoices.Count}",
            Choices = GameState.PendingRewardChoices,
            AllowSkip = true,
        });
    }

    void HandleRewardPicked(Part chosen)
    {
        if (CardPickerPanel != null)
            CardPickerPanel.OnPicked -= HandleRewardPicked;

        GameState.SelectReward(chosen);
        ShowSpeciesEditor();
    }

    BattleData BuildBattleData(WorldMapNode destination)
    {
        var data = new BattleData();

        if (GameState.PlayerSpecies != null)
            data.PlayerGroup.Add(GameState.PlayerSpecies);

        if (destination.Encounter?.EnemyGroup?.Members != null)
            data.EnemyGroup.AddRange(destination.Encounter.EnemyGroup.Members);

        data.ActionCards.AddRange(GameState.Inventory.ActiveParts.ToList());
        return data;
    }
}
