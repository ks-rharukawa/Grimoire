using System;
using System.Collections.Generic;

namespace Grimoire;

// docs/card-operations.md の操作アーキタイプを CombatView に配線する戦闘モデル。
// 第1段階: 基本対処「Probe Request」系のみ操作実装。filter/lookup/echo は後続 increment。
// View からは「カード i クリック」「スロット j クリック」「End Turn」を受け取り、
// 描画は CombatView 側が本モデルの状態を読んで行う（model と view の分離）。

public enum CombatPhase
{
    Idle,       // プレイヤーの手番。カード選択 / End Turn 可
    Probing,    // probe 使用中。スロットの応答を観測して診断クリック
    Resolving,  // 効果適用の演出中（短時間）
    EnemyTurn,  // 敵の反撃演出
    Won,
    Defeated
}

public enum CardKind { Probe, Filter, Lookup, Echo }

public sealed class Card
{
    public string Name { get; }
    public int Cost { get; }
    public string Effect { get; }
    public CardKind Kind { get; }

    public Card(string name, int cost, string effect, CardKind kind)
    {
        Name = name;
        Cost = cost;
        Effect = effect;
        Kind = kind;
    }

    // この increment で操作が実装済みなのは probe のみ。他 job は non-playable（dim 表示）。
    public bool OperationImplemented => Kind == CardKind.Probe;
}

public sealed class Combat
{
    public const int PlayerHpMax = 30;
    public const int EnemyHpMax = 30;
    public const int EnergyMax = 3;
    public const int BaseIntent = 5;

    public int PlayerHp { get; private set; } = PlayerHpMax;
    public int EnemyHp { get; private set; } = EnemyHpMax;
    public int Energy { get; private set; } = EnergyMax;

    // 敵の障害状態「過負荷 (Overload)」段数。probe 成功で 1 段ずつ弱化。
    // classes.md 障害状態語彙: 過負荷 = 被ダメージ増 → 敵の攻撃力に加算する。
    public int Overload { get; private set; } = 3;
    public int IntentDamage => BaseIntent + Overload;

    public CombatPhase Phase { get; private set; } = CombatPhase.Idle;
    public int ActiveCard { get; private set; } = -1;

    private readonly Card[] _hand =
    {
        new("Probe Request", 1, "過負荷源を\n診断し弱化", CardKind.Probe),
        new("Packet Filter", 1, "悪性流量を\n選別遮断", CardKind.Filter),
        new("Lookup",        1, "名前解決で\n+1 ドロー", CardKind.Lookup),
        new("Echo Reply",    2, "複製で\n複数対処",     CardKind.Echo),
        new("Probe Request", 1, "過負荷源を\n診断し弱化", CardKind.Probe),
    };
    public IReadOnlyList<Card> Hand => _hand;

    // ===== probe operation 状態 =====
    // card-operations.md アーキタイプ1: 複数スロットへ probe を送り、応答が返らない
    // スロット（過負荷源）を読み取る。色で正解を露出しない（ガード条件B）。
    public const int SlotCount = 4;
    public int OverloadedSlot { get; private set; }
    // 各スロットの probe 往復進捗 0..1（0→0.5 往路, 0.5→1 復路）。
    // 健全スロットは 1 まで到達（帰還）、過負荷スロットは 0.5 で停滞（応答が返らない）。
    private readonly double[] _slotProbe = new double[SlotCount];
    public IReadOnlyList<double> SlotProbe => _slotProbe;

    // 全健全スロットが帰還し、過負荷スロットが停滞した = 応答が出揃い「読める」状態。
    // これ以前のクリックは診断不可（観測前の運当てを防ぐ。docs/card-operations.md ガード条件B）。
    public bool ProbeObservable
    {
        get
        {
            if (Phase != CombatPhase.Probing) return false;
            for (int i = 0; i < SlotCount; i++)
            {
                var ready = i == OverloadedSlot ? _slotProbe[i] >= 0.5 : _slotProbe[i] >= 1.0;
                if (!ready) return false;
            }
            return true;
        }
    }

    // ===== 演出タイマ =====
    public bool LastSuccess { get; private set; }
    public int LastDamage { get; private set; }
    private double _resolveT;
    private double _enemyT;
    private bool _enemyApplied;

    private const double ResolveSeconds = 0.9;
    private const double EnemyTurnSeconds = 1.2;
    private const double EnemyHitAt = 0.5;

    private readonly Random _rng = new();

    // 16ms ティックごとに View から呼ばれる。phase 固有のアニメ・自動遷移を進める。
    public void Tick()
    {
        switch (Phase)
        {
            case CombatPhase.Probing:
                AdvanceProbe();
                break;

            case CombatPhase.Resolving:
                _resolveT += 1.0 / 60.0;
                if (_resolveT >= ResolveSeconds) FinishResolve();
                break;

            case CombatPhase.EnemyTurn:
                _enemyT += 1.0 / 60.0;
                if (_enemyT >= EnemyTurnSeconds * EnemyHitAt && !_enemyApplied) ApplyEnemyDamage();
                if (_enemyT >= EnemyTurnSeconds) FinishEnemyTurn();
                break;
        }
    }

    private void AdvanceProbe()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (i == OverloadedSlot)
            {
                // 過負荷: 往路途中（スロット到達）まで進んで停滞。応答が返らない。
                if (_slotProbe[i] < 0.5) _slotProbe[i] = Math.Min(0.5, _slotProbe[i] + 0.013);
            }
            else
            {
                // 健全: 速やかに往復して帰還。
                if (_slotProbe[i] < 1.0) _slotProbe[i] = Math.Min(1.0, _slotProbe[i] + 0.028);
            }
        }
    }

    // ===== Idle phase: カード使用 / End Turn =====

    public bool TryPlayCard(int index)
    {
        if (Phase != CombatPhase.Idle) return false;
        if (index < 0 || index >= _hand.Length) return false;
        var card = _hand[index];
        if (!card.OperationImplemented) return false;   // filter/lookup/echo は後続
        if (Energy < card.Cost) return false;

        Energy -= card.Cost;
        ActiveCard = index;
        StartProbe();
        return true;
    }

    private void StartProbe()
    {
        Phase = CombatPhase.Probing;
        OverloadedSlot = _rng.Next(SlotCount);
        Array.Clear(_slotProbe, 0, SlotCount);
    }

    public void EndTurn()
    {
        if (Phase != CombatPhase.Idle) return;
        Phase = CombatPhase.EnemyTurn;
        _enemyT = 0;
        _enemyApplied = false;
    }

    // ===== Probing phase: スロット診断 =====

    public void DiagnoseSlot(int slot)
    {
        if (Phase != CombatPhase.Probing) return;
        if (slot < 0 || slot >= SlotCount) return;
        if (!ProbeObservable) return;   // 応答が出揃う前は診断不可（観測前の運当て防止）

        LastSuccess = slot == OverloadedSlot;
        Phase = CombatPhase.Resolving;
        _resolveT = 0;
        if (LastSuccess)
        {
            LastDamage = 6;
            EnemyHp = Math.Max(0, EnemyHp - LastDamage);
            if (Overload > 0) Overload--;   // 過負荷を 1 段弱化（敵 IntentDamage も下がる）
        }
        else
        {
            LastDamage = 0;                 // 診断ミス = シンプル不発（quiz-cadence 決定3）
        }
    }

    private void FinishResolve()
    {
        ActiveCard = -1;
        Phase = EnemyHp <= 0 ? CombatPhase.Won : CombatPhase.Idle;
    }

    // ===== EnemyTurn phase =====

    private void ApplyEnemyDamage()
    {
        LastDamage = IntentDamage;
        PlayerHp = Math.Max(0, PlayerHp - LastDamage);
        _enemyApplied = true;
    }

    private void FinishEnemyTurn()
    {
        if (PlayerHp <= 0)
        {
            Phase = CombatPhase.Defeated;
            return;
        }
        Energy = EnergyMax;
        Phase = CombatPhase.Idle;
    }

    // ===== Won/Defeated → 再戦 =====

    public void Restart()
    {
        PlayerHp = PlayerHpMax;
        EnemyHp = EnemyHpMax;
        Energy = EnergyMax;
        Overload = 3;
        ActiveCard = -1;
        LastSuccess = false;
        LastDamage = 0;
        _resolveT = 0;
        _enemyT = 0;
        _enemyApplied = false;
        Array.Clear(_slotProbe, 0, SlotCount);
        Phase = CombatPhase.Idle;
    }
}
