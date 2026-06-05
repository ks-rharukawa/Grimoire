using System;
using Avalonia.Controls;

namespace Grimoire;

// アプリ階層の画面マネージャ。RunState を保持し、Map / Combat / Result 画面を切り替える。
// 1 子画面を持つ Decorator として実装し、Child を差し替えて遷移する。
public class GameView : Decorator
{
    private RunState _run = new();
    private readonly Random _rng = new();

    private const int TargetBattles = 3;   // #7 仮の終端。#11 でボス戦を終端にする。

    public GameView()
    {
        if (CaptureWantsCombat()) StartBattle();   // capture は戦闘画面を直接表示
        else ShowMap();
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
        map.Proceed = StartBattle;
        Child = map;
    }

    private void StartBattle()
    {
        var enemy = (EnemyKind)_rng.Next(4);
        var combat = new Combat(_run.Deck, _run.PlayerHp, enemy);
        var cv = new CombatView(combat);
        cv.Finished = _ => OnBattleFinished(combat);
        Child = cv;
    }

    private void OnBattleFinished(Combat combat)
    {
        _run.PlayerHp = combat.PlayerHp;   // HP はラン中持続
        if (combat.Victory)
        {
            _run.BattlesWon++;
            // #9 でここに戦闘後3択 (RewardView) を挟む。
            if (_run.BattlesWon >= TargetBattles) ShowResult(victory: true);
            else ShowMap();
        }
        else
        {
            ShowResult(victory: false);
        }
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
