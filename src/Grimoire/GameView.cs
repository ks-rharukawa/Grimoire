using System;
using Avalonia.Controls;

namespace Grimoire;

// アプリ階層の画面マネージャ。RunState を保持し、Map / Combat / Result 画面を切り替える。
// 1 子画面を持つ Decorator として実装し、Child を差し替えて遷移する。
public class GameView : Decorator
{
    private RunState _run = new();
    private readonly Random _rng = new();

    public GameView()
    {
        if (Environment.GetEnvironmentVariable("GRIMOIRE_CAPTURE_REWARD") == "1") ShowReward();
        else if (Environment.GetEnvironmentVariable("GRIMOIRE_CAPTURE_REST") == "1") Child = new RestView(_run);
        else if (CaptureWantsCombat()) StartCaptureBattle();   // capture は戦闘画面を直接表示
        else ShowMap();
    }

    private void StartCaptureBattle()
    {
        var combat = new Combat(_run.Deck, _run.PlayerHp, EnemyKind.OverloadServer);
        Child = new CombatView(combat);
    }

    // GRIMOIRE_CAPTURE_*/START_COMBAT が立っていれば戦闘から開始 (capture 用)
    private static bool CaptureWantsCombat()
    {
        string[] envs =
        {
            "GRIMOIRE_CAPTURE_PROBE", "GRIMOIRE_CAPTURE_FILTER",
            "GRIMOIRE_CAPTURE_LOOKUP", "GRIMOIRE_CAPTURE_ECHO", "GRIMOIRE_START_COMBAT"
        };
        foreach (var e in envs)
            if (Environment.GetEnvironmentVariable(e) == "1") return true;
        return false;
    }

    private void ShowMap()
    {
        var map = new RunMapView(_run);
        map.NodeSelected = OnNodeSelected;
        Child = map;
    }

    private void OnNodeSelected(MapNode node)
    {
        switch (node.Type)
        {
            case MapNodeType.Rest:
                ShowRest(node);
                break;
            default:   // Battle / Elite / Boss → 戦闘
                StartBattle(node);
                break;
        }
    }

    private void StartBattle(MapNode node)
    {
        var combat = new Combat(_run.Deck, _run.PlayerHp, EnemyForNode(node));
        var cv = new CombatView(combat);
        cv.Finished = _ => OnBattleFinished(combat, node);
        Child = cv;
    }

    private EnemyKind EnemyForNode(MapNode node) => node.Type switch
    {
        MapNodeType.Elite => EnemyKind.RequestFlood,     // 精鋭は逓増型 (#11 で専用調整)
        MapNodeType.Boss  => EnemyKind.OverloadServer,   // #11 で専用ボスに置き換え
        _ => (EnemyKind)_rng.Next(4),
    };

    private void OnBattleFinished(Combat combat, MapNode node)
    {
        _run.PlayerHp = combat.PlayerHp;   // HP はラン中持続
        if (combat.Victory)
        {
            _run.BattlesWon++;
            _run.Map.Advance(node);
            if (node.Type == MapNodeType.Boss) ShowResult(victory: true);
            else ShowReward();             // 戦闘後3択 → その後マップへ
        }
        else
        {
            ShowResult(victory: false);
        }
    }

    private void ShowRest(MapNode node)
    {
        var rest = new RestView(_run);
        rest.Rest = () =>
        {
            _run.PlayerHp = Math.Min(_run.PlayerHpMax, _run.PlayerHp + rest.HealAmount);
            _run.Map.Advance(node);
            ShowMap();
        };
        Child = rest;
    }

    private void ShowReward()
    {
        var reward = new RewardView(CardPool.Offer(_rng, 3));
        reward.Chosen = card =>
        {
            if (card != null) _run.Deck.Add(card);   // スキップ時は追加なし
            ShowMap();
        };
        Child = reward;
    }

    private void ShowResult(bool victory)
    {
        var over = new RunOverView(_run, victory);
        over.Restart = NewRun;
        Child = over;
    }

    private void NewRun()
    {
        _run = new RunState();
        ShowMap();
    }
}
