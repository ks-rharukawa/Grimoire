using System;
using System.Collections.Generic;

namespace Grimoire;

// docs/card-operations.md の操作アーキタイプを CombatView に配線する戦闘モデル。
// 実装済み operation: probe (基本対処) / filter (基本遮断)。lookup/echo は後続 increment。
// View からは「カード i クリック」「スロット j クリック」「End Turn」を受け取り、
// 描画は CombatView 側が本モデルの状態を読んで行う（model と view の分離）。

public enum CombatPhase
{
    Idle,       // プレイヤーの手番。カード選択 / End Turn 可
    Probing,    // probe 使用中。スロットの応答を観測して診断クリック
    Filtering,  // filter 使用中。バースト(悪性)レーンを選別して遮断
    Lookup,     // lookup 使用中。キャッシュTTL/権威の状態を読み解決経路を選ぶ
    Echoing,    // echo 使用中。障害ノードだけに複製を振り分ける(シグネチャ)
    Resolving,  // 効果適用の演出中（短時間）
    EnemyTurn,  // 敵の反撃演出
    Won,
    Defeated
}

// lookup operation: 名前解決の動的状態と、プレイヤーが選ぶ解決経路。
public enum LookupScenario { CacheValid, CacheExpired, NxDomain }
public enum LookupAction { Cache, Authoritative, GiveUp }

public enum CardKind { Probe, Filter, Lookup, Echo }

// classes.md 障害状態語彙。敵に積む状態でゲームメカニクスとネットワーク概念を接続する。
public enum StatusKind { Overload, Congestion, PacketLoss, Latency, AttackTraffic, DnsFailure }

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

    // 操作が実装済みのカード。
    public bool OperationImplemented => Kind is CardKind.Probe or CardKind.Filter or CardKind.Lookup or CardKind.Echo;
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

    // プレイヤーのブロック (StS 流: 被ダメージを吸収し、次の手番開始で 0 にリセット)。filter が生成。
    private const int StatCap = 9999;   // overflow / UI 崩れ防止の飽和上限
    public int PlayerBlock { get; private set; }
    public void AddBlock(int n) { if (n > 0) PlayerBlock = (int)Math.Min(StatCap, (long)PlayerBlock + n); }

    // ===== 敵の障害状態 (classes.md 障害状態語彙) =====
    // 過負荷 = 被ダメージ増 を IntentDamage に反映。他状態は #3-5 の operation が積む枠組み。
    private readonly Dictionary<StatusKind, int> _enemyStatus = new();
    public int EnemyStatusOf(StatusKind k) => _enemyStatus.TryGetValue(k, out var v) ? v : 0;
    public void AddEnemyStatus(StatusKind k, int n)
    {
        var v = (int)Math.Clamp((long)EnemyStatusOf(k) + n, 0, StatCap);
        if (v == 0) _enemyStatus.Remove(k); else _enemyStatus[k] = v;
    }
    public IReadOnlyList<(StatusKind Kind, int Stacks)> ActiveEnemyStatuses
    {
        get
        {
            var list = new List<(StatusKind, int)>();
            foreach (var kind in Enum.GetValues<StatusKind>())
                if (EnemyStatusOf(kind) > 0) list.Add((kind, EnemyStatusOf(kind)));
            return list;
        }
    }
    public int Overload => EnemyStatusOf(StatusKind.Overload);
    public int IntentDamage => BaseIntent + Overload;

    public CombatPhase Phase { get; private set; } = CombatPhase.Idle;

    // ===== デッキ循環 (classes.md 決定5: 初期デッキ10枚 4-3-2-1) =====
    public const int HandSize = 5;
    private readonly List<Card> _drawPile = new();
    private readonly List<Card> _hand = new();
    private readonly List<Card> _discard = new();
    public IReadOnlyList<Card> Hand => _hand;
    public int DrawPileCount => _drawPile.Count;
    public int DiscardPileCount => _discard.Count;

    public Combat()
    {
        SetupEnemy();
        BuildDeck();
        DrawToFull();
    }

    private void SetupEnemy()
    {
        _enemyStatus.Clear();
        AddEnemyStatus(StatusKind.Overload, 3);   // 初期障害シナリオ: 過負荷 3 段
    }

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

    // ===== filter operation 状態 (card-operations.md アーキタイプ2) =====
    // 複数レーンのうちバースト(悪性=攻撃流量)を見分け、それだけを遮断。色で正解を出さない(条件B)。
    // tell はパケットの「密度」(バースト vs 散発) のみ。view が密度アニメに IsLaneMalicious を使う。
    public const int LaneCount = 3;
    private readonly bool[] _laneMalicious = new bool[LaneCount];
    private readonly bool[] _laneFiltered = new bool[LaneCount];
    public bool IsLaneMalicious(int i) => i >= 0 && i < LaneCount && _laneMalicious[i];
    public bool IsLaneFiltered(int i) => i >= 0 && i < LaneCount && _laneFiltered[i];
    public double FilterElapsed { get; private set; }

    // ===== lookup operation 状態 (card-operations.md アーキタイプ3) =====
    // キャッシュTTL / 権威の動的状態を読み、正しい解決経路を選ぶ。静的な名前→IP対応にしない(条件B)。
    // 毎回シナリオをランダム化するので「この名前→この答え」の固定マッピングが効かない (Q&A退化対策)。
    public LookupScenario Scenario { get; private set; }
    public double LookupTtl { get; private set; }    // 0..1 キャッシュ残TTL (CacheValid のみ >0、動的に減る)
    private double _lookupElapsed;

    // ===== echo operation 状態 (card-operations.md アーキタイプ4 / シグネチャ) =====
    // 自己完結図: echo 自身が複数の候補ノードを内包。障害(problem)ノードだけに複製を振り分ける。
    // tell はノードの signal trace (障害=ジャグ波形 / 正常=フラット)。色で正解を出さない(条件B)。
    // 判断対象の数が難度・順不同・精度不問・時間制限なし。全正解で複合効果、1つでも誤れば全不発。
    public const int EchoTargetCount = 5;
    private readonly bool[] _echoProblem = new bool[EchoTargetCount];
    private readonly bool[] _echoMarked = new bool[EchoTargetCount];
    public bool IsEchoProblem(int i) => i >= 0 && i < EchoTargetCount && _echoProblem[i];
    public bool IsEchoMarked(int i) => i >= 0 && i < EchoTargetCount && _echoMarked[i];
    public double EchoElapsed { get; private set; }

    // ===== 演出タイマ =====
    public CardKind LastOp { get; private set; }    // 直近に使った操作の種別 (resolve バナーの出し分け)
    public bool LastSuccess { get; private set; }
    public int LastDamage { get; private set; }
    public int LastBlocked { get; private set; }
    public bool EnemyHitApplied => _enemyApplied;   // 敵の攻撃が実際に着弾したか (バナー表示用)
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

            case CombatPhase.Filtering:
                FilterElapsed += 1.0 / 60.0;   // パケット密度アニメ用の経過時間
                break;

            case CombatPhase.Lookup:
                _lookupElapsed += 1.0 / 60.0;
                if (Scenario == LookupScenario.CacheValid)
                    LookupTtl = Math.Max(0.3, 1.0 - _lookupElapsed * 0.05);  // 動的に減るが有効を保つ
                break;

            case CombatPhase.Echoing:
                EchoElapsed += 1.0 / 60.0;   // signal trace アニメ用
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
        if (index < 0 || index >= _hand.Count) return false;
        var card = _hand[index];
        if (!card.OperationImplemented) return false;   // echo は後続
        if (Energy < card.Cost) return false;

        Energy -= card.Cost;
        _hand.RemoveAt(index);
        _discard.Add(card);             // 使用したカードは捨て山へ (quiz-cadence 決定3: 成否問わず)
        LastOp = card.Kind;
        switch (card.Kind)
        {
            case CardKind.Probe: StartProbe(); break;
            case CardKind.Filter: StartFilter(); break;
            case CardKind.Lookup: StartLookup(); break;
            case CardKind.Echo: StartEcho(); break;
        }
        return true;
    }

    private void StartProbe()
    {
        Phase = CombatPhase.Probing;
        OverloadedSlot = _rng.Next(SlotCount);
        Array.Clear(_slotProbe, 0, SlotCount);
    }

    private void StartFilter()
    {
        Phase = CombatPhase.Filtering;
        FilterElapsed = 0;
        Array.Clear(_laneFiltered, 0, LaneCount);
        Array.Clear(_laneMalicious, 0, LaneCount);
        // 1〜2 レーンを悪性(バースト)に。残りは散発(良性)＝必ず 1 本は通すべき正常通信が残る。
        int k = 1 + _rng.Next(2);
        var idx = new List<int>();
        for (int i = 0; i < LaneCount; i++) idx.Add(i);
        for (int picked = 0; picked < k; picked++)
        {
            int j = _rng.Next(idx.Count);
            _laneMalicious[idx[j]] = true;
            idx.RemoveAt(j);
        }
    }

    public void ToggleLaneFilter(int lane)
    {
        if (Phase != CombatPhase.Filtering) return;
        if (lane < 0 || lane >= LaneCount) return;
        _laneFiltered[lane] = !_laneFiltered[lane];
    }

    // 適用: 遮断したレーンの集合が悪性レーンの集合と「過不足なく一致」したら成功。
    // 全遮断や良性遮断は失敗（card-operations: フィルタの本質は選別であって全遮断ではない）。
    public void CommitFilter()
    {
        if (Phase != CombatPhase.Filtering) return;
        bool correct = true;
        int maliciousCount = 0;
        for (int i = 0; i < LaneCount; i++)
        {
            if (_laneMalicious[i]) maliciousCount++;
            if (_laneFiltered[i] != _laneMalicious[i]) correct = false;
        }
        LastSuccess = correct;
        Phase = CombatPhase.Resolving;
        _resolveT = 0;
        if (correct)
        {
            int block = 4 + maliciousCount * 2;   // 遮断した悪性レーン数に応じた Block
            AddBlock(block);
            LastDamage = block;                   // resolve バナーの見出し数値
        }
        else
        {
            LastDamage = 0;                       // 選別ミス = 不発 (quiz-cadence 決定3)
        }
    }

    // ===== lookup operation =====

    private void StartLookup()
    {
        Phase = CombatPhase.Lookup;
        _lookupElapsed = 0;
        Scenario = (LookupScenario)_rng.Next(3);   // 毎回ランダム → 固定マッピング無効
        LookupTtl = Scenario == LookupScenario.CacheValid ? 1.0 : 0.0;
    }

    private LookupAction CorrectLookupAction => Scenario switch
    {
        LookupScenario.CacheValid => LookupAction.Cache,           // TTL 有効 → キャッシュから即解決
        LookupScenario.CacheExpired => LookupAction.Authoritative, // TTL 切れ → 権威へ問い合わせ
        _ => LookupAction.GiveUp                                   // NXDOMAIN → 解決不能と判断
    };

    public void ResolveLookup(LookupAction action)
    {
        if (Phase != CombatPhase.Lookup) return;
        LastSuccess = action == CorrectLookupAction;
        Phase = CombatPhase.Resolving;
        _resolveT = 0;
        if (LastSuccess)
        {
            DrawOne();          // 名前解決成功 → 情報取得 = カード +1 ドロー
            LastDamage = 1;
        }
        else
        {
            LastDamage = 0;     // 解決ミス = 不発 (ドローなし)
        }
    }

    private void DrawOne()
    {
        if (_drawPile.Count == 0)
        {
            if (_discard.Count == 0) return;
            _drawPile.AddRange(_discard);
            _discard.Clear();
            Shuffle(_drawPile);
        }
        var top = _drawPile[^1];
        _drawPile.RemoveAt(_drawPile.Count - 1);
        _hand.Add(top);
    }

    // ===== echo operation (シグネチャ) =====

    private void StartEcho()
    {
        Phase = CombatPhase.Echoing;
        EchoElapsed = 0;
        Array.Clear(_echoMarked, 0, EchoTargetCount);
        Array.Clear(_echoProblem, 0, EchoTargetCount);
        // 2〜3 ノードを障害(正解の振り分け先)に。判断対象の数が難度。
        int k = 2 + _rng.Next(2);
        var idx = new List<int>();
        for (int i = 0; i < EchoTargetCount; i++) idx.Add(i);
        for (int picked = 0; picked < k; picked++)
        {
            int j = _rng.Next(idx.Count);
            _echoProblem[idx[j]] = true;
            idx.RemoveAt(j);
        }
    }

    public void ToggleEchoMark(int node)
    {
        if (Phase != CombatPhase.Echoing) return;
        if (node < 0 || node >= EchoTargetCount) return;
        _echoMarked[node] = !_echoMarked[node];   // 順不同・精度不問
    }

    // 発火: 振り分けた集合が障害集合と過不足なく一致したら複合効果。1つでも誤れば全不発(部分成功なし)。
    public void CommitEcho()
    {
        if (Phase != CombatPhase.Echoing) return;
        bool correct = true;
        for (int i = 0; i < EchoTargetCount; i++)
            if (_echoMarked[i] != _echoProblem[i]) correct = false;

        LastSuccess = correct;
        Phase = CombatPhase.Resolving;
        _resolveT = 0;
        if (correct)
        {
            LastDamage = 8;                                  // 複合効果: 障害源を削る
            EnemyHp = Math.Max(0, EnemyHp - LastDamage);
            AddBlock(6);                                     // + ブロック生成
            AddEnemyStatus(StatusKind.Overload, -1);         // + 過負荷弱化
        }
        else
        {
            LastDamage = 0;                                  // 振り分けミス = 全不発
        }
    }

    public void EndTurn()
    {
        if (Phase != CombatPhase.Idle) return;
        DiscardHand();                  // 残った手札は捨て山へ (StS 流: ターン終了で手札を流す)
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
            AddEnemyStatus(StatusKind.Overload, -1);   // 過負荷を 1 段弱化（敵 IntentDamage も下がる）
        }
        else
        {
            LastDamage = 0;                 // 診断ミス = シンプル不発（quiz-cadence 決定3）
        }
    }

    private void FinishResolve()
    {
        Phase = EnemyHp <= 0 ? CombatPhase.Won : CombatPhase.Idle;
    }

    // ===== EnemyTurn phase =====

    private void ApplyEnemyDamage()
    {
        int incoming = IntentDamage;
        int absorbed = Math.Min(PlayerBlock, incoming);   // ブロックで吸収
        PlayerBlock -= absorbed;
        LastBlocked = absorbed;
        LastDamage = incoming - absorbed;                 // HP に通った分
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
        PlayerBlock = 0;                // ブロックは次の手番開始でリセット (StS 流)
        Energy = EnergyMax;
        DrawToFull();                   // 新しい手番の手札を引く
        Phase = CombatPhase.Idle;
    }

    // ===== デッキ操作 =====

    private void BuildDeck()
    {
        _drawPile.Clear();
        _hand.Clear();
        _discard.Clear();
        for (int i = 0; i < 4; i++) _drawPile.Add(new Card("Probe Request", 1, "過負荷源を\n診断し弱化", CardKind.Probe));
        for (int i = 0; i < 3; i++) _drawPile.Add(new Card("Packet Filter", 1, "悪性流量を\n選別遮断", CardKind.Filter));
        for (int i = 0; i < 2; i++) _drawPile.Add(new Card("Lookup",        1, "名前解決で\n+1 ドロー", CardKind.Lookup));
        _drawPile.Add(new Card("Echo Reply", 2, "複製で\n複数対処", CardKind.Echo));
        Shuffle(_drawPile);
    }

    private void DrawToFull()
    {
        while (_hand.Count < HandSize)
        {
            if (_drawPile.Count == 0)
            {
                if (_discard.Count == 0) break;     // 引けるカードが尽きた
                _drawPile.AddRange(_discard);
                _discard.Clear();
                Shuffle(_drawPile);
            }
            var top = _drawPile[^1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            _hand.Add(top);
        }
    }

    private void DiscardHand()
    {
        _discard.AddRange(_hand);
        _hand.Clear();
    }

    private void Shuffle(List<Card> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    // ===== Won/Defeated → 再戦 =====

    public void Restart()
    {
        PlayerHp = PlayerHpMax;
        EnemyHp = EnemyHpMax;
        Energy = EnergyMax;
        PlayerBlock = 0;
        SetupEnemy();
        LastSuccess = false;
        LastDamage = 0;
        LastBlocked = 0;
        _resolveT = 0;
        _enemyT = 0;
        _enemyApplied = false;
        Array.Clear(_slotProbe, 0, SlotCount);
        Array.Clear(_laneFiltered, 0, LaneCount);
        Array.Clear(_laneMalicious, 0, LaneCount);
        FilterElapsed = 0;
        _lookupElapsed = 0;
        LookupTtl = 0;
        Array.Clear(_echoProblem, 0, EchoTargetCount);
        Array.Clear(_echoMarked, 0, EchoTargetCount);
        EchoElapsed = 0;
        BuildDeck();
        DrawToFull();
        Phase = CombatPhase.Idle;
    }
}
