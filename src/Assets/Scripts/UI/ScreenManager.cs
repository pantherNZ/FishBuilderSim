using System;
using System.Collections;
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
    public GameEndPanel GameEndPanel;
    public ShopPanel ShopPanel;

    [Header("State")]
    [Tooltip("Optional shared state. If null, this is sourced from SpeciesEditorPanel or created at runtime.")]
    public GameState GameState;
    [Header("Battle Timing")]
    [SerializeField, Min(0f)] float _turnDelaySeconds = 0.35f;
    [SerializeField, Min(0f)] float _gameEndDelaySeconds = 0.75f;

    // ── Active battle state ──────────────────────────────────────────────────
    WorldMapNode _activeBattleNode;
    SpeciesGroup _playerBattleGroup;
    SpeciesGroup _enemyBattleGroup;
    int _battleRound;
    bool _battleAwaitingPlayerStep;
    bool _battleRunning;
    bool _battleStepInProgress;
    bool _battleResultPending;
    bool _battleResultPlayerWon;
    Coroutine _battleStepRoutine;
    bool _battleRewardSelectionActive;
    Species _playerDefending;
    Species _enemyDefending;
    ActionManager _enemyActionManager;

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

        ShowRewardPicker(true);
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
        if (GameEndPanel != null)
        {
            GameEndPanel.OnRewardSelected -= HandleRewardPicked;
            GameEndPanel.OnExitToWorldRequested -= HandleGameEndExitRequested;
        }
        if (ShopPanel != null)
        {
            ShopPanel.OnPartPurchaseRequested -= HandleShopPartPurchaseRequested;
            ShopPanel.OnSpeciesPurchaseRequested -= HandleShopSpeciesPurchaseRequested;
            ShopPanel.OnLeaveRequested -= HandleShopLeaveRequested;
        }
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

        GameState.CurrentEncounter = destination.Encounter;
        GameState.WorldMap.MovePlayerTo(destination);
        GameState.Inventory.ApplyToSpecies(GameState.PlayerSpecies);

        var data = BuildBattleData(destination);
        StartBattle(data, destination);

        HideAllPanels();
        BattlePanel?.Show(data);
        UpdateEnemyIntentVisual();
        BattlePanel?.SetStepControlsEnabled(true);
        BattlePanel?.AppendCombatLog($"Encounter started: {destination.DisplayName}");
        BattlePanel?.AppendSelectionPrompt();
    }

    void ResolvePanelReferences()
    {
        SpeciesEditorPanel ??= FindAnyObjectByType<SpeciesEditorPanel>();
        WorldMapPanel ??= FindAnyObjectByType<WorldMapPanel>();
        BattlePanel ??= FindAnyObjectByType<BattlePanel>();
        CardPickerPanel ??= FindAnyObjectByType<CardPickerPanel>();
        GameEndPanel ??= FindAnyObjectByType<GameEndPanel>();
        ShopPanel ??= FindAnyObjectByType<ShopPanel>();
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

        if (GameEndPanel != null)
        {
            GameEndPanel.OnRewardSelected -= HandleRewardPicked;
            GameEndPanel.OnRewardSelected += HandleRewardPicked;
            GameEndPanel.OnExitToWorldRequested -= HandleGameEndExitRequested;
            GameEndPanel.OnExitToWorldRequested += HandleGameEndExitRequested;
        }

        if (ShopPanel != null)
        {
            ShopPanel.OnPartPurchaseRequested -= HandleShopPartPurchaseRequested;
            ShopPanel.OnPartPurchaseRequested += HandleShopPartPurchaseRequested;
            ShopPanel.OnSpeciesPurchaseRequested -= HandleShopSpeciesPurchaseRequested;
            ShopPanel.OnSpeciesPurchaseRequested += HandleShopSpeciesPurchaseRequested;
            ShopPanel.OnLeaveRequested -= HandleShopLeaveRequested;
            ShopPanel.OnLeaveRequested += HandleShopLeaveRequested;
        }
    }

    void HandleTravelRequested(WorldMapNode node)
    {
        if (node?.Type == WorldMapNodeType.Shop)
            ShowShop(node);
        else
            ShowBattle(node);
    }

    void ShowShop(WorldMapNode destination)
    {
        if (GameState == null || !GameState.EnterShop(destination))
        {
            Debug.LogWarning("[ScreenManager] Unable to open the requested shop.");
            return;
        }

        HideAllPanels();
        ShopPanel?.Show(GameState, GameState.CurrentEncounter as ShopEncounter);
    }

    void HandleShopPartPurchaseRequested(int offerIndex)
    {
        if (GameState.TryPurchaseShopPart(offerIndex))
            ShopPanel?.Refresh();
    }

    void HandleShopSpeciesPurchaseRequested()
    {
        if (GameState.TryPurchaseShopSpecies())
            ShopPanel?.Refresh();
    }

    void HandleShopLeaveRequested()
    {
        GameState.LeaveShop();
        ShowWorldMap();
    }

    void HideAllPanels()
    {
        SpeciesEditorPanel?.Hide();
        WorldMapPanel?.Hide();
        BattlePanel?.Hide();
        CardPickerPanel?.Hide();
        GameEndPanel?.Hide();
        ShopPanel?.Hide();
    }

    void StartBattle(BattleData data, WorldMapNode destination)
    {
        _activeBattleNode = destination;
        _playerBattleGroup = new SpeciesGroup("Player", data.PlayerGroup ?? new List<Species>());
        _enemyBattleGroup = new SpeciesGroup("Enemy", data.EnemyGroup ?? new List<Species>());
        _playerBattleGroup.Initialize();
        _enemyBattleGroup.Initialize();
        _playerBattleGroup.OnEncounterStart(_enemyBattleGroup);
        _enemyBattleGroup.OnEncounterStart(_playerBattleGroup);

        _battleRound = 1;
        _battleAwaitingPlayerStep = true;
        _battleRunning = true;
        _battleStepInProgress = false;
        _battleResultPending = false;
        _playerDefending = null;
        _enemyDefending = null;
        PrepareEnemyAction();
    }

    void PrepareEnemyAction()
    {
        _enemyBattleGroup?.OnTurnStart();
        _enemyActionManager = RollEnemyAction();
        UpdateEnemyIntentVisual();
    }

    ActionManager RollEnemyAction()
    {
        var enemy = _enemyBattleGroup?.Alive.FirstOrDefault();
        if (enemy == null)
            return new ActionManager(1);

        return BuildSingleActionManager(enemy, ChooseEnemyAction(enemy), _playerBattleGroup);
    }

    BattleStepAction ChooseEnemyAction(Species enemy)
    {
        if (enemy.Attack > 0 && _playerBattleGroup.HasAlive)
            return BattleStepAction.Attack;
        if (enemy.Forage > 0)
            return BattleStepAction.Forage;
        if (enemy.ProvidesSpecialAction(SpeciesActionType.Blind) && _playerBattleGroup.HasAlive)
            return BattleStepAction.Blind;
        return BattleStepAction.Defend;
    }

    void UpdateEnemyIntentVisual()
    {
        if (_enemyActionManager == null || _enemyActionManager.Actions.Count == 0)
        {
            BattlePanel?.SetEnemyIntent(null, null);
            return;
        }

        var intent = _enemyActionManager.Actions[0];
        if (intent.Actor == null || !intent.Actor.IsAlive)
        {
            BattlePanel?.SetEnemyIntent(null, null);
            return;
        }

        var target = intent.Targets?.FirstOrDefault(candidate => candidate != null && candidate.IsAlive);
        BattlePanel?.SetEnemyIntent(intent.Actor, intent.Type, target, intent.SourcePart);
    }

    void EnsureEnemyAction()
    {
        if (_enemyActionManager != null && _enemyActionManager.Actions.Count > 0
            && _enemyActionManager.Actions[0].Actor != null
            && _enemyActionManager.Actions[0].Actor.IsAlive)
            return;

        _enemyActionManager = RollEnemyAction();
        UpdateEnemyIntentVisual();
    }

    void HandleBattleStepRequested(BattleStepRequest request)
    {
        if (!_battleRunning || !_battleAwaitingPlayerStep || _battleStepInProgress)
            return;

        if (!TryResolvePlayerStep(request, out var actor, out var action, out var actionManager))
        {
            BattlePanel?.AppendCombatLog("Select a living player species and action.");
            BattlePanel?.SetSelectableSpecies(_playerBattleGroup?.Alive.ToList());
            return;
        }

        _battleStepRoutine = StartCoroutine(ResolveBattleStep(actor, action, actionManager));
    }

    IEnumerator ResolveBattleStep(Species actor, BattleStepAction action, ActionManager actionManager)
    {
        _battleStepInProgress = true;
        _battleAwaitingPlayerStep = false;
        BattlePanel?.SetStepControlsEnabled(false);

        _playerBattleGroup.OnTurnStart();
        BattlePanel?.RefreshHealthBars();
        yield return RunPlayerStep(actor, action, actionManager);
        EnsureEnemyAction();
        _playerBattleGroup.ClearTemporaryStatModifiers();
        BattlePanel?.RefreshSpeciesVisuals();
        BattlePanel?.RefreshHealthBars();

        if (ResolveBattleEndIfAny())
        {
            yield return FinishBattleAfterDelay();
            yield break;
        }

        BattlePanel?.SetRoundAndTurn(_battleRound, false);
        yield return new WaitForSeconds(_turnDelaySeconds);

        yield return RunEnemyStep();
        BattlePanel?.RefreshSpeciesVisuals();
        BattlePanel?.RefreshHealthBars();

        if (ResolveBattleEndIfAny())
        {
            yield return FinishBattleAfterDelay();
            yield break;
        }

        yield return new WaitForSeconds(_turnDelaySeconds);

        _battleRound++;
        PrepareEnemyAction();
        _battleAwaitingPlayerStep = true;
        BattlePanel?.SetRoundAndTurn(_battleRound, true);
        BattlePanel?.SetSelectableSpecies(_playerBattleGroup.Alive.ToList());
        BattlePanel?.SetStepControlsEnabled(true);
        BattlePanel?.AppendSelectionPrompt();
        _battleStepInProgress = false;
        _battleStepRoutine = null;
    }

    IEnumerator FinishBattleAfterDelay()
    {
        yield return new WaitForSeconds(_gameEndDelaySeconds);
        ShowPendingBattleResult();
        _battleStepInProgress = false;
        _battleStepRoutine = null;
    }

    bool TryResolvePlayerStep(BattleStepRequest request, out Species actor, out BattleStepAction action, out ActionManager actionManager)
    {
        actor = null;
        action = BattleStepAction.Attack;
        actionManager = null;
        Part queuedSourcePart = null;
        SpeciesActionType queuedActionType = SpeciesActionType.None;

        if (request?.ActionManager != null && request.ActionManager.Actions.Count > 0)
        {
            var queuedAction = request.ActionManager.Actions[0];
            if (queuedAction.Type == SpeciesActionType.None)
                return false;

            actor = queuedAction.Actor;
            action = ToBattleStepAction(queuedAction.Type);
            actionManager = request.ActionManager;
            queuedSourcePart = queuedAction.SourcePart;
            queuedActionType = queuedAction.Type;
        }
        else if (request?.Actor != null)
        {
            actor = request.Actor;
            action = request.Action;
            actionManager = BuildSingleActionManager(actor, action, _enemyBattleGroup);
        }

        if (actor == null || !actor.IsAlive)
            return false;
        if (_playerBattleGroup == null || !_playerBattleGroup.Members.Contains(actor))
            return false;
        if (queuedSourcePart != null && !actor.CanUseAction(queuedSourcePart, queuedActionType))
            return false;

        actionManager ??= BuildSingleActionManager(actor, action, _enemyBattleGroup);
        return true;
    }

    IEnumerator RunPlayerStep(Species actor, BattleStepAction action, ActionManager actionManager)
    {
        BattlePanel?.AppendCombatLog($"Round {_battleRound} | Player: {actor.Name} uses {action}.");

        switch (action)
        {
            case BattleStepAction.Attack:
                {
                    var target = actionManager?.Actions.FirstOrDefault().Targets?.FirstOrDefault(t => t != null && t.IsAlive)
                        ?? actor.PickTarget(_enemyBattleGroup.Alive);
                    if (target == null)
                    {
                        BattlePanel?.AppendCombatLog("No enemy target available.");
                        yield break;
                    }

                    if (BattlePanel != null)
                        yield return BattlePanel.PlayAttackAnimation(actor, target);

                    int before = target.CurrentHealth;
                    int defendBonus = target == _enemyDefending ? DefendBonus : 0;
                    if (defendBonus > 0)
                        target.BaseDefense += defendBonus;

                    CombatSimulator.ExecuteActions(_playerBattleGroup, _enemyBattleGroup, actionManager);

                    if (defendBonus > 0)
                        target.BaseDefense -= defendBonus;

                    int damage = Mathf.Max(0, before - target.CurrentHealth);
                    BattlePanel?.ShowDamageNumber(target, damage);
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
                    CombatSimulator.ExecuteActions(_playerBattleGroup, _enemyBattleGroup, actionManager);
                    BattlePanel?.AppendCombatLog($"{actor.Name} foraged and gained {Mathf.Max(0, actor.CurrentSize - before)} size.");
                    _enemyDefending = null;
                    break;
                }
            case BattleStepAction.Defend:
                {
                    CombatSimulator.ExecuteActions(_playerBattleGroup, _enemyBattleGroup, actionManager);
                    _playerDefending = actor;
                    _enemyDefending = null;
                    BattlePanel?.AppendCombatLog($"{actor.Name} braces for impact (+{DefendBonus} defense this enemy action).");
                    break;
                }
            case BattleStepAction.Blind:
                {
                    var target = actionManager?.Actions.FirstOrDefault().Targets?.FirstOrDefault(t => t != null && t.IsAlive)
                        ?? actor.PickTarget(_enemyBattleGroup.Alive);
                    if (target == null)
                    {
                        BattlePanel?.AppendCombatLog("No enemy target available.");
                        yield break;
                    }

                    actor.SpecialAction(SpeciesActionType.Blind, target);
                    BattlePanel?.AppendCombatLog($"{actor.Name} blinds {target.Name} for this enemy action.");
                    _enemyDefending = null;
                    break;
                }
        }

        yield break;
    }

    IEnumerator RunEnemyStep()
    {
        EnsureEnemyAction();
        if (_enemyActionManager == null || _enemyActionManager.Actions.Count == 0)
            yield break;

        var queuedAction = _enemyActionManager.Actions[0];
        var enemy = queuedAction.Actor;
        if (enemy == null) yield break;

        BattleStepAction action = ToBattleStepAction(queuedAction.Type);

        BattlePanel?.AppendCombatLog($"Round {_battleRound} | Enemy: {enemy.Name} uses {action}.");

        var actionManager = _enemyActionManager;

        switch (action)
        {
            case BattleStepAction.Attack:
                {
                    var target = actionManager.Actions.FirstOrDefault().Targets?.FirstOrDefault(t => t != null && t.IsAlive)
                        ?? enemy.PickTarget(_playerBattleGroup.Alive);
                    if (target == null)
                    {
                        BattlePanel?.AppendCombatLog("Enemy found no valid target.");
                        yield break;
                    }

                    if (BattlePanel != null)
                        yield return BattlePanel.PlayAttackAnimation(enemy, target);

                    int before = target.CurrentHealth;
                    int defendBonus = target == _playerDefending ? DefendBonus : 0;
                    if (defendBonus > 0)
                        target.BaseDefense += defendBonus;

                    CombatSimulator.ExecuteActions(_enemyBattleGroup, _playerBattleGroup, actionManager);

                    if (defendBonus > 0)
                        target.BaseDefense -= defendBonus;

                    int damage = Mathf.Max(0, before - target.CurrentHealth);
                    BattlePanel?.ShowDamageNumber(target, damage);
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
                    CombatSimulator.ExecuteActions(_enemyBattleGroup, _playerBattleGroup, actionManager);
                    BattlePanel?.AppendCombatLog($"{enemy.Name} foraged and gained {Mathf.Max(0, enemy.CurrentSize - before)} size.");
                    _playerDefending = null;
                    break;
                }
            case BattleStepAction.Defend:
                {
                    CombatSimulator.ExecuteActions(_enemyBattleGroup, _playerBattleGroup, actionManager);
                    _enemyDefending = enemy;
                    _playerDefending = null;
                    BattlePanel?.AppendCombatLog($"{enemy.Name} takes a defensive stance.");
                    break;
                }
            case BattleStepAction.Blind:
                {
                    var target = actionManager.Actions.FirstOrDefault().Targets?.FirstOrDefault(t => t != null && t.IsAlive)
                        ?? enemy.PickTarget(_playerBattleGroup.Alive);
                    if (target == null)
                    {
                        BattlePanel?.AppendCombatLog("Enemy found no valid target to blind.");
                        break;
                    }

                    enemy.SpecialAction(SpeciesActionType.Blind, target);
                    BattlePanel?.AppendCombatLog($"{enemy.Name} blinds {target.Name} for this player action.");
                    _playerDefending = null;
                    break;
                }
        }

        _enemyBattleGroup.ClearTemporaryStatModifiers();
        yield break;
    }

    ActionManager BuildSingleActionManager(Species actor, BattleStepAction action, SpeciesGroup opposingGroup)
    {
        var manager = new ActionManager(1);
        if (actor == null)
            return manager;

        List<Species> targets = null;
        if (action == BattleStepAction.Attack || action == BattleStepAction.Blind)
        {
            var target = actor.PickTarget(opposingGroup?.Alive ?? Enumerable.Empty<Species>());
            if (target != null)
                targets = new List<Species> { target };
        }

        manager.SetAction(new SpeciesAction
        {
            Actor = actor,
            Type = ToSpeciesActionType(action),
            Targets = targets,
        });

        return manager;
    }

    static SpeciesActionType ToSpeciesActionType(BattleStepAction action)
    {
        return action switch
        {
            BattleStepAction.Forage => SpeciesActionType.Forage,
            BattleStepAction.Defend => SpeciesActionType.Defend,
            BattleStepAction.Blind => SpeciesActionType.Blind,
            _ => SpeciesActionType.Attack,
        };
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

        attacker.AttackAction(target);

        if (defenseBonus > 0)
            target.BaseDefense -= defenseBonus;

        return Mathf.Max(0, before - target.CurrentHealth);
    }

    bool ResolveBattleEndIfAny()
    {
        if (_enemyBattleGroup != null && !_enemyBattleGroup.HasAlive)
        {
            BattlePanel?.AppendCombatLog($"Victory on round {_battleRound}!");
            SetPendingBattleResult(true);
            return true;
        }

        if (_playerBattleGroup != null && !_playerBattleGroup.HasAlive)
        {
            BattlePanel?.AppendCombatLog("Your species were defeated.");
            SetPendingBattleResult(false);
            return true;
        }

        return false;
    }

    void SetPendingBattleResult(bool playerWon)
    {
        _battleRunning = false;
        _battleAwaitingPlayerStep = false;
        _battleResultPending = true;
        _battleResultPlayerWon = playerWon;
        GameState.HandleEncounterResult(playerWon);
    }

    void ShowPendingBattleResult()
    {
        if (!_battleResultPending)
            return;

        _battleResultPending = false;
        if (_battleResultPlayerWon)
            ShowGameEndVictory();
        else
            ShowGameEndDefeat();
    }

    static int GetLivingTotalSize(IEnumerable<Species> species)
    {
        return species?.Where(s => s != null && s.IsAlive).Sum(s => s.Size) ?? 0;
    }

    void ShowRewardPicker(bool isFirstReward = false)
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
            Title = isFirstReward ? "PICK A STARTING PART" : "CHOOSE A REWARD",
            Subtitle = $"Pick 1 of {GameState.PendingRewardChoices.Count}",
            Choices = GameState.PendingRewardChoices,
            AllowSkip = true,
        });
    }

    void ShowGameEndVictory()
    {
        _battleRewardSelectionActive = GameState.PendingRewardChoices != null
            && GameState.PendingRewardChoices.Count > 0;

        if (GameEndPanel == null)
        {
            ShowRewardPicker();
            return;
        }

        HideAllPanels();
        GameEndPanel.ShowVictory(GameState.PendingRewardChoices);
    }

    void ShowGameEndDefeat()
    {
        _battleRewardSelectionActive = false;

        if (GameEndPanel == null)
        {
            ShowSpeciesEditor();
            return;
        }

        HideAllPanels();
        GameEndPanel.ShowDefeat();
    }

    void HandleGameEndExitRequested()
    {
        ShowWorldMap();
    }

    void HandleRewardPicked(Part chosen)
    {
        if (CardPickerPanel != null)
            CardPickerPanel.OnPicked -= HandleRewardPicked;

        bool returnToBattleResult = _battleRewardSelectionActive;
        _battleRewardSelectionActive = false;
        GameState.SelectReward(chosen);

        if (returnToBattleResult && GameEndPanel != null)
        {
            if (chosen != null)
            {
                ShowWorldMap();
                return;
            }

            HideAllPanels();
            GameEndPanel.ShowVictoryWithLoot(null);
            return;
        }

        ShowSpeciesEditor();
    }

    BattleData BuildBattleData(WorldMapNode destination)
    {
        var data = new BattleData();

        if (GameState.OwnedSpecies != null)
            data.PlayerGroup.AddRange(GameState.OwnedSpecies);

        if (destination.Encounter?.EnemyGroup?.Members != null)
            data.EnemyGroup.AddRange(destination.Encounter.EnemyGroup.Members);

        return data;
    }
}
