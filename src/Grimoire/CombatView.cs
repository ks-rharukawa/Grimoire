using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Grimoire;

// docs/style-guide.md §5 の戦闘画面実装。
// 第4イテレーション (designer 提案反映): Stage 背景の魔法陣・左右フィリグリー・敵 radial halo・粒のトレイル・ピクセル規律修正。
public class CombatView : Control
{
    private readonly DispatcherTimer _timer;

    private const double BookFrameMargin = 80;
    private const double EnemyAreaTop = 40;
    private const double EnemyAreaHeight = 160;
    private const double StageTop = 220;
    private const double StageHeight = 220;
    private const double PlayerStripTop = 460;
    private const double PlayerStripHeight = 50;
    private const double HandTop = 530;
    private const double HandHeight = 170;
    private const double CaptionStripHeight = 32;
    private const double CardWidth = 160;
    private const double CardHeight = 220;

    private readonly Combat _combat;

    // 戦闘終了時に GameView へ結果を返す (Won/Defeated)。
    public Action<CombatPhase>? Finished;

    private enum CardIcon { ProbeRadar, ShieldFilter, EchoLoop, LookupGlass, RetryArrow }

    private static CardIcon IconFor(CardKind kind) => kind switch
    {
        CardKind.Probe => CardIcon.ProbeRadar,
        CardKind.Filter => CardIcon.ShieldFilter,
        CardKind.Lookup => CardIcon.LookupGlass,
        CardKind.Echo => CardIcon.EchoLoop,
        _ => CardIcon.ProbeRadar
    };

    private readonly List<(double X, double Y, byte Alpha, int Size)> _stars = new();

    private double _packetProgress;
    private double _pulse;

    public CombatView(Combat combat)
    {
        _combat = combat;

        // ピクセル規律: 形状は anti-alias 抑止 (style-guide §4)。
        // テキスト (特に日本語) は AA を有効にしないと読めないので default のまま。
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

        var rng = new Random(42);
        for (int i = 0; i < 80; i++)
        {
            _stars.Add((
                Math.Floor(rng.NextDouble() * 1280),
                Math.Floor(rng.NextDouble() * 720),
                (byte)(40 + rng.Next(120)),
                rng.Next(3) == 0 ? 2 : 1
            ));
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();

        // capture 用: probe operation 中の状態を撮るためのフック (GRIMOIRE_CAPTURE_PROBE=1)
        if (Environment.GetEnvironmentVariable("GRIMOIRE_CAPTURE_PROBE") == "1")
        {
            for (int i = 0; i < _combat.Hand.Count; i++)
                if (_combat.Hand[i].Kind == CardKind.Probe && _combat.TryPlayCard(i)) break;
            for (int i = 0; i < 60; i++) _combat.Tick();   // 健全スロット帰還 + 過負荷停滞まで進める
        }

        // capture 用: filter operation の途中状態 (GRIMOIRE_CAPTURE_FILTER=1)
        if (Environment.GetEnvironmentVariable("GRIMOIRE_CAPTURE_FILTER") == "1")
        {
            for (int i = 0; i < _combat.Hand.Count; i++)
                if (_combat.Hand[i].Kind == CardKind.Filter && _combat.TryPlayCard(i)) break;
            for (int i = 0; i < 120; i++) _combat.Tick();                 // パケットを populate
            for (int i = 0; i < Combat.LaneCount; i++)
                if (_combat.IsLaneMalicious(i)) _combat.ToggleLaneFilter(i);  // 悪性を遮断した状態
        }

        // capture 用: lookup operation の状態 (GRIMOIRE_CAPTURE_LOOKUP=1)
        if (Environment.GetEnvironmentVariable("GRIMOIRE_CAPTURE_LOOKUP") == "1")
        {
            for (int i = 0; i < _combat.Hand.Count; i++)
                if (_combat.Hand[i].Kind == CardKind.Lookup && _combat.TryPlayCard(i)) break;
            for (int i = 0; i < 30; i++) _combat.Tick();
        }

        // capture 用: echo operation の状態 (GRIMOIRE_CAPTURE_ECHO=1)
        if (Environment.GetEnvironmentVariable("GRIMOIRE_CAPTURE_ECHO") == "1")
        {
            for (int i = 0; i < _combat.Hand.Count; i++)
                if (_combat.Hand[i].Kind == CardKind.Echo && _combat.TryPlayCard(i)) break;
            for (int i = 0; i < 20; i++) _combat.Tick();
            for (int i = 0; i < Combat.EchoTargetCount; i++)
                if (_combat.IsEchoProblem(i)) _combat.ToggleEchoMark(i);   // 障害ノードを振り分けた状態
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _packetProgress += 0.006;
        if (_packetProgress > 1.05) _packetProgress = -0.05;
        _pulse += 0.06;
        _combat.Tick();
        InvalidateVisual();
    }

    // ===== 入力 (docs/card-operations.md: カード使用 → probe 診断 → End Turn) =====

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);

        switch (_combat.Phase)
        {
            case CombatPhase.Idle:
                for (int i = 0; i < _combat.Hand.Count; i++)
                {
                    if (CardRectAt(i).Contains(pos)) { _combat.TryPlayCard(i); break; }
                }
                if (EndTurnRect.Contains(pos)) _combat.EndTurn();
                break;

            case CombatPhase.Probing:
                for (int i = 0; i < Combat.SlotCount; i++)
                {
                    if (SlotRect(i).Contains(pos)) { _combat.DiagnoseSlot(i); break; }
                }
                break;

            case CombatPhase.Filtering:
                if (FilterApplyRect.Contains(pos)) { _combat.CommitFilter(); break; }
                for (int i = 0; i < Combat.LaneCount; i++)
                {
                    if (LaneRect(i).Contains(pos)) { _combat.ToggleLaneFilter(i); break; }
                }
                break;

            case CombatPhase.Lookup:
                for (int i = 0; i < 3; i++)
                {
                    if (LookupActionRect(i).Contains(pos)) { _combat.ResolveLookup((LookupAction)i); break; }
                }
                break;

            case CombatPhase.Echoing:
                if (EchoFireRect.Contains(pos)) { _combat.CommitEcho(); break; }
                for (int i = 0; i < Combat.EchoTargetCount; i++)
                {
                    if (EchoNodeRect(i).Contains(pos)) { _combat.ToggleEchoMark(i); break; }
                }
                break;

            case CombatPhase.Won:
            case CombatPhase.Defeated:
                Finished?.Invoke(_combat.Phase);   // ラン遷移は GameView が担当
                break;
        }

        InvalidateVisual();
        base.OnPointerPressed(e);
    }

    // ===== ヒットテスト用ジオメトリ (Render と座標を共有) =====

    private const double HandAreaLeft = BookFrameMargin + 20;
    private double HandAreaWidth => Bounds.Width - (BookFrameMargin + 20) * 2;
    private const double CardGap = 12;

    private double HandStartX
    {
        get
        {
            var totalWidth = _combat.Hand.Count * CardWidth + (_combat.Hand.Count - 1) * CardGap;
            return HandAreaLeft + (HandAreaWidth - totalWidth) / 2;
        }
    }

    private Rect CardRectAt(int i)
    {
        var x = HandStartX + i * (CardWidth + CardGap);
        var cardY = HandTop + (HandHeight - CardHeight) / 2;
        return new Rect(x, cardY, CardWidth, CardHeight);
    }

    private Rect EndTurnRect
    {
        get
        {
            var stripRight = HandAreaLeft + HandAreaWidth;
            return new Rect(stripRight - 140, PlayerStripTop + 8, 124, 34);
        }
    }

    // probe operation: Stage 内に縦並びのサーバスロット 4 枚。
    private static double StageInnerLeft => BookFrameMargin + 20;
    private double ServerSlotX => Bounds.Width - (BookFrameMargin + 20) - 200;
    private const double SlotWidth = 180;
    private const double SlotHeight = 26;
    private const double SlotPitch = 36;
    private const double SlotTop = StageTop + 24;

    private Rect SlotRect(int i) => new(ServerSlotX, SlotTop + i * SlotPitch, SlotWidth, SlotHeight);

    // filter operation: Stage 内に横並びのトラフィックレーン 3 本。
    private double FilterLaneLeft => StageInnerLeft + 96;                       // 自App 側 (左)
    private double FilterLaneRight => Bounds.Width - (BookFrameMargin + 20) - 96; // 攻撃元 側 (右)
    private const double LaneTop = StageTop + 36;
    private const double LanePitch = 44;
    private double LaneY(int i) => LaneTop + i * LanePitch;
    private Rect LaneRect(int i) => new(FilterLaneLeft, LaneY(i) - 16, FilterLaneRight - FilterLaneLeft, 32);

    private Rect FilterApplyRect
    {
        get
        {
            var stageRight = Bounds.Width - (BookFrameMargin + 20);
            return new Rect(stageRight - 130, StageTop + StageHeight - CaptionStripHeight - 40, 116, 30);
        }
    }

    // lookup operation: 3 つの解決アクションボタン (0=キャッシュ参照 1=権威に問い合わせ 2=解決不能)
    private Rect LookupActionRect(int i)
    {
        var stageRight = Bounds.Width - (BookFrameMargin + 20);
        const double w = 168, gap = 16, h = 32;
        double total = 3 * w + 2 * gap;
        double startX = StageInnerLeft + (stageRight - StageInnerLeft - total) / 2;
        double y = StageTop + StageHeight - CaptionStripHeight - 42;
        return new Rect(startX + i * (w + gap), y, w, h);
    }

    // echo operation: 5 つの候補ノード + 発火ボタン
    private Rect EchoNodeRect(int i)
    {
        const double w = 120, h = 64, gap = 18;
        double total = Combat.EchoTargetCount * w + (Combat.EchoTargetCount - 1) * gap;
        var stageRight = Bounds.Width - (BookFrameMargin + 20);
        double startX = StageInnerLeft + (stageRight - StageInnerLeft - total) / 2;
        double y = StageTop + 52;
        return new Rect(startX + i * (w + gap), y, w, h);
    }

    private Rect EchoFireRect
    {
        get
        {
            var stageRight = Bounds.Width - (BookFrameMargin + 20);
            return new Rect(stageRight - 130, StageTop + StageHeight - CaptionStripHeight - 42, 116, 32);
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        DrawPagesBackground(context);
        DrawStarfield(context);
        DrawBookFrame(context);
        DrawPageFiligree(context);
        DrawEnemyArea(context);
        DrawStage(context);
        DrawPlayerStrip(context);
        DrawHand(context);
        DrawEndOverlay(context);

        if (Environment.GetEnvironmentVariable("GRIMOIRE_FONT_PROBE") == "1")
            DrawFontProbe(context);
    }

    private void DrawEndOverlay(DrawingContext context)
    {
        if (_combat.Phase != CombatPhase.Won && _combat.Phase != CombatPhase.Defeated) return;

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(190, 5, 7, 21)), new Rect(Bounds.Size));

        var win = _combat.Phase == CombatPhase.Won;
        var msg = win ? "ネットワーク安定を回復!" : "障害に呑まれた…";
        var brush = win ? Palette.ArcaneGoldBrush : Palette.CrimsonBrush;
        var ft = new FormattedText(msg, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 40, brush);
        context.DrawText(ft, new Point((Bounds.Width - ft.Width) / 2, Bounds.Height / 2 - 50));

        var sub = new FormattedText("クリックで進む", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 18, Palette.ParchmentBrush);
        context.DrawText(sub, new Point((Bounds.Width - sub.Width) / 2, Bounds.Height / 2 + 16));
    }

    private static void DrawFontProbe(DrawingContext context)
    {
        // 切り分けテスト: 同じ文字列を違う FontFamily 指定で描画。
        // 全部同じに見えればフォント指定が効いていない証拠。
        var samples = new (string Label, string FamilyName)[]
        {
            ("Typeface.Default          ", ""),
            ("FF: 'Hiragino Sans'       ", "Hiragino Sans"),
            ("FF: 'Hiragino Sans, Yu...' ", "Hiragino Sans, Yu Gothic, Noto Sans CJK JP, sans-serif"),
            ("FF: 'Yu Gothic'           ", "Yu Gothic"),
            ("FF: 'PingFang SC'         ", "PingFang SC"),
            ("FF: 'Comic Sans MS'       ", "Comic Sans MS"),
            ("FF: 'NoSuchFont12345'     ", "NoSuchFont12345"),
        };
        const string body = "障害源を5弱化 ABC123";
        double y = 8;
        var bgBrush = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0));
        context.FillRectangle(bgBrush, new Rect(8, 4, 760, samples.Length * 22 + 8));

        foreach (var s in samples)
        {
            Typeface tf = s.FamilyName == ""
                ? Typeface.Default
                : new Typeface(new FontFamily(s.FamilyName), FontStyle.Normal, FontWeight.Bold);

            var ft = new FormattedText(s.Label + body, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, tf, 14,
                new SolidColorBrush(Color.FromRgb(255, 255, 255)));
            context.DrawText(ft, new Point(12, y));
            y += 22;
        }
    }

    // ===== 背景 =====

    private void DrawPagesBackground(DrawingContext context)
    {
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(Bounds.Size));
        var pages = new Rect(BookFrameMargin, 0, Bounds.Width - BookFrameMargin * 2, Bounds.Height);
        context.FillRectangle(Palette.MidnightBrush, pages);

        // 製本溝 (gutter) - 中央の縦薄帯
        var gutterX = Bounds.Width / 2;
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(180, 5, 7, 21)),
            new Rect(gutterX - 4, 0, 8, Bounds.Height));
    }

    private void DrawStarfield(DrawingContext context)
    {
        var pages = new Rect(BookFrameMargin, 0, Bounds.Width - BookFrameMargin * 2, Bounds.Height);
        foreach (var s in _stars)
        {
            if (s.X < pages.Left || s.X > pages.Right) continue;
            var brush = new SolidColorBrush(Color.FromArgb(s.Alpha, 212, 175, 55));
            context.FillRectangle(brush, new Rect(s.X, s.Y, s.Size, s.Size));
        }
    }

    // ===== ブックフレーム (Designer #2: 左右フィリグリー) =====

    private void DrawBookFrame(DrawingContext context)
    {
        // ページ端の縦線
        context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(BookFrameMargin - 2, 0, 2, Bounds.Height));
        context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(Bounds.Width - BookFrameMargin, 0, 2, Bounds.Height));

        // 4 つのコーナー
        const double cornerLen = 40;
        const double cornerThick = 4;
        DrawRichCorner(context, 18, 18, cornerLen, cornerThick, Corner.TopLeft);
        DrawRichCorner(context, Bounds.Width - 18 - cornerLen, 18, cornerLen, cornerThick, Corner.TopRight);
        DrawRichCorner(context, 18, Bounds.Height - 18 - cornerLen, cornerLen, cornerThick, Corner.BottomLeft);
        DrawRichCorner(context, Bounds.Width - 18 - cornerLen, Bounds.Height - 18 - cornerLen, cornerLen, cornerThick, Corner.BottomRight);
    }

    private enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    private static void DrawRichCorner(DrawingContext context, double x, double y, double len, double thick, Corner c)
    {
        var gold = Palette.ArcaneGoldBrush;
        var dim = Palette.ArcaneGoldDimBrush;
        switch (c)
        {
            case Corner.TopLeft:
                context.FillRectangle(gold, new Rect(x, y, len, thick));
                context.FillRectangle(gold, new Rect(x, y, thick, len));
                context.FillRectangle(dim, new Rect(x + thick + 4, y + thick + 4, len - thick - 8, 2));
                context.FillRectangle(dim, new Rect(x + thick + 4, y + thick + 4, 2, len - thick - 8));
                context.FillRectangle(gold, new Rect(x + len - 2, y + len - 2, 4, 4));
                break;
            case Corner.TopRight:
                context.FillRectangle(gold, new Rect(x, y, len, thick));
                context.FillRectangle(gold, new Rect(x + len - thick, y, thick, len));
                context.FillRectangle(dim, new Rect(x + 4, y + thick + 4, len - thick - 8, 2));
                context.FillRectangle(dim, new Rect(x + len - thick - 6, y + thick + 4, 2, len - thick - 8));
                context.FillRectangle(gold, new Rect(x - 2, y + len - 2, 4, 4));
                break;
            case Corner.BottomLeft:
                context.FillRectangle(gold, new Rect(x, y + len - thick, len, thick));
                context.FillRectangle(gold, new Rect(x, y, thick, len));
                context.FillRectangle(dim, new Rect(x + thick + 4, y + len - thick - 6, len - thick - 8, 2));
                context.FillRectangle(dim, new Rect(x + thick + 4, y + 4, 2, len - thick - 8));
                context.FillRectangle(gold, new Rect(x + len - 2, y - 2, 4, 4));
                break;
            case Corner.BottomRight:
                context.FillRectangle(gold, new Rect(x, y + len - thick, len, thick));
                context.FillRectangle(gold, new Rect(x + len - thick, y, thick, len));
                context.FillRectangle(dim, new Rect(x + 4, y + len - thick - 6, len - thick - 8, 2));
                context.FillRectangle(dim, new Rect(x + len - thick - 6, y + 4, 2, len - thick - 8));
                context.FillRectangle(gold, new Rect(x - 2, y - 2, 4, 4));
                break;
        }
    }

    private void DrawPageFiligree(DrawingContext context)
    {
        // 左右の各サイドストリップ: コンパスローズ (上) + ぶら下がる玉珠連 (中) + 円形紋章 (下)
        const double sideCenter = 40; // BookFrameMargin / 2
        DrawSideFiligree(context, sideCenter);
        DrawSideFiligree(context, Bounds.Width - sideCenter);
    }

    private void DrawSideFiligree(DrawingContext context, double cx)
    {
        // (1) 上: 8 角コンパスローズ (y=100)
        DrawCompassRose(context, cx, 100);

        // (2) 中: ぶら下がり玉珠連 (y=180 〜 540)
        for (int i = 0; i < 8; i++)
        {
            var by = 180 + i * 45;
            // 大玉 (4x4)
            context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(cx - 2, by, 4, 4));
            // 中玉 (2x2、上下)
            context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(cx - 1, by + 8, 2, 2));
            context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(cx - 1, by + 14, 2, 2));
        }

        // (3) 下: 円形紋章 (y=600)
        DrawEmblem(context, cx, 600);
    }

    private void DrawCompassRose(DrawingContext context, double cx, double cy)
    {
        var gold = Palette.ArcaneGoldBrush;
        var dim = Palette.ArcaneGoldDimBrush;
        // 8 方向のスパイク
        // 上下 (太)
        context.FillRectangle(gold, new Rect(cx - 1, cy - 18, 2, 16));
        context.FillRectangle(gold, new Rect(cx - 1, cy + 2, 2, 16));
        // 左右
        context.FillRectangle(gold, new Rect(cx - 18, cy - 1, 16, 2));
        context.FillRectangle(gold, new Rect(cx + 2, cy - 1, 16, 2));
        // 斜め (細、dim)
        for (int i = 0; i < 10; i++)
        {
            context.FillRectangle(dim, new Rect(cx + i, cy - i - 1, 2, 2));
            context.FillRectangle(dim, new Rect(cx - i - 1, cy - i - 1, 2, 2));
            context.FillRectangle(dim, new Rect(cx + i, cy + i, 2, 2));
            context.FillRectangle(dim, new Rect(cx - i - 1, cy + i, 2, 2));
        }
        // 中心円 (4x4 dim + 2x2 gold)
        context.FillRectangle(dim, new Rect(cx - 4, cy - 4, 8, 8));
        context.FillRectangle(gold, new Rect(cx - 2, cy - 2, 4, 4));
    }

    private void DrawEmblem(DrawingContext context, double cx, double cy)
    {
        var gold = Palette.ArcaneGoldBrush;
        var dim = Palette.ArcaneGoldDimBrush;
        // 外輪 (16x16 のリング、4 隅は欠ける)
        context.FillRectangle(gold, new Rect(cx - 6, cy - 16, 12, 2));
        context.FillRectangle(gold, new Rect(cx - 6, cy + 14, 12, 2));
        context.FillRectangle(gold, new Rect(cx - 16, cy - 6, 2, 12));
        context.FillRectangle(gold, new Rect(cx + 14, cy - 6, 2, 12));
        // コーナー dim
        context.FillRectangle(dim, new Rect(cx - 12, cy - 12, 4, 2));
        context.FillRectangle(dim, new Rect(cx - 12, cy - 12, 2, 4));
        context.FillRectangle(dim, new Rect(cx + 8, cy - 12, 4, 2));
        context.FillRectangle(dim, new Rect(cx + 10, cy - 12, 2, 4));
        context.FillRectangle(dim, new Rect(cx - 12, cy + 10, 4, 2));
        context.FillRectangle(dim, new Rect(cx - 12, cy + 8, 2, 4));
        context.FillRectangle(dim, new Rect(cx + 8, cy + 10, 4, 2));
        context.FillRectangle(dim, new Rect(cx + 10, cy + 8, 2, 4));
        // 内側マーク (十字)
        context.FillRectangle(gold, new Rect(cx - 1, cy - 6, 2, 12));
        context.FillRectangle(gold, new Rect(cx - 6, cy - 1, 12, 2));
        // 中心
        context.FillRectangle(Palette.CrimsonBrush, new Rect(cx - 1, cy - 1, 2, 2));
    }

    // ===== Section A: Enemy Area (Designer #3: radial halo + cracks) =====

    private void DrawEnemyArea(DrawingContext context)
    {
        const double areaLeft = BookFrameMargin + 20;
        var centerX = Bounds.Width / 2;

        // (Designer #3) Crimson radial halo 2 段、パルス
        var haloPulse = (byte)(40 + Math.Floor(Math.Sin(_pulse) * 30));
        var innerPulse = (byte)(80 + Math.Floor(Math.Sin(_pulse + 0.5) * 40));
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(haloPulse, 192, 57, 43)),
            new Rect(centerX - 60, EnemyAreaTop, 120, 130));
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(innerPulse, 192, 57, 43)),
            new Rect(centerX - 30, EnemyAreaTop + 10, 60, 110));

        DrawServerRack(context, centerX - 40, EnemyAreaTop + 5, 80, 110);

        var nameFt = new FormattedText(_combat.EnemyName, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 14, Palette.CrimsonBrush);
        context.DrawText(nameFt, new Point(areaLeft + 20, EnemyAreaTop + 8));

        DrawBar(context, areaLeft + 20, EnemyAreaTop + 34, 240, 14, _combat.EnemyHp, _combat.EnemyHpMax,
            Palette.CrimsonBrush, $"HP {_combat.EnemyHp}/{_combat.EnemyHpMax}");

        if (_combat.EnemyBlock > 0)
            DrawBlockChip(context, areaLeft + 350, EnemyAreaTop + 30, _combat.EnemyBlock);

        // 敵の意図 (telegraph): 種別ごとに多彩な行動
        DrawIntentDisplay(context, areaLeft + 20, EnemyAreaTop + 58, _combat.CurrentIntent);

        // 障害状態チップ (classes.md 障害状態語彙)
        double chipX = areaLeft + 20;
        foreach (var (kind, stacks) in _combat.ActiveEnemyStatuses)
        {
            var text = $"{StatusLabel(kind)} x{stacks}";
            var ft = new FormattedText(text, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 12, Palette.MidnightBrush);
            var chipW = ft.Width + 14;
            var chipRect = new Rect(chipX, EnemyAreaTop + 80, chipW, 18);
            context.FillRectangle(Palette.ArcaneGoldBrush, chipRect);
            context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1), chipRect);
            context.DrawText(ft, new Point(chipX + 7, EnemyAreaTop + 81));
            chipX += chipW + 8;
        }
    }

    private static void DrawIntentDisplay(DrawingContext context, double x, double y, EnemyIntent it)
    {
        string text;
        IBrush brush;
        switch (it.Kind)
        {
            case IntentKind.Attack:      text = $"攻撃 -{it.Value}";              brush = Palette.CrimsonBrush; break;
            case IntentKind.MultiAttack: text = $"連撃 -{it.Value}×{it.Hits}";    brush = Palette.CrimsonBrush; break;
            case IntentKind.Buff:        text = $"自己強化 +{it.Value}";          brush = Palette.ArcaneGoldBrush; break;
            case IntentKind.Debuff:      text = $"遅延付与 +{it.Value}";          brush = Palette.EtherealCyanBrush; break;
            case IntentKind.Defend:      text = $"防御 +{it.Value}";              brush = Palette.EtherealCyanBrush; break;
            default:                     text = "?";                              brush = Palette.ParchmentBrush; break;
        }
        var ft = new FormattedText("意図: " + text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 14, brush);
        context.DrawText(ft, new Point(x, y));
    }

    private static string StatusLabel(StatusKind k) => k switch
    {
        StatusKind.Overload => "過負荷",
        StatusKind.Congestion => "輻輳",
        StatusKind.PacketLoss => "欠落",
        StatusKind.Latency => "遅延",
        StatusKind.AttackTraffic => "攻撃流量",
        StatusKind.DnsFailure => "名前解決失敗",
        _ => k.ToString()
    };

    private void DrawServerRack(DrawingContext context, double x, double y, double w, double h)
    {
        // 影
        context.FillRectangle(Palette.CrimsonDimBrush, new Rect(x + 2, y + 2, w, h));
        // 本体
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(x, y, w, h));

        // 外枠
        const double frame = 2;
        context.FillRectangle(Palette.CrimsonBrush, new Rect(x, y, w, frame));
        context.FillRectangle(Palette.CrimsonBrush, new Rect(x, y + h - frame, w, frame));
        context.FillRectangle(Palette.CrimsonBrush, new Rect(x, y, frame, h));
        context.FillRectangle(Palette.CrimsonBrush, new Rect(x + w - frame, y, frame, h));

        // スロット 5段
        for (int i = 0; i < 5; i++)
        {
            var rowY = y + 8 + i * 18;
            context.FillRectangle(Palette.CrimsonDimBrush, new Rect(x + 6, rowY, w - 12, 12));
            context.FillRectangle(Palette.CrimsonBrush, new Rect(x + 8, rowY + 2, w - 16, 1));
            context.FillRectangle(Palette.CrimsonBrush, new Rect(x + 8, rowY + 8, w - 16, 1));
            var pulse = (byte)(180 + Math.Floor(Math.Sin(_pulse + i) * 60));
            var ledBrush = new SolidColorBrush(Color.FromArgb(pulse, 192, 57, 43));
            context.FillRectangle(ledBrush, new Rect(x + w - 12, rowY + 4, 4, 4));
        }

        // (Designer #3) 亀裂: 折れ線 2 本 (1px Crimson)
        DrawCrack(context, x + 18, y + 20, new[] { (6, 20), (-2, 18), (4, 22) });
        DrawCrack(context, x + 50, y + 15, new[] { (4, 24), (-3, 20), (5, 28) });

        // 火花 (パルス)
        var sparkAlpha = (byte)(120 + Math.Floor(Math.Sin(_pulse * 2) * 80));
        var sparkBrush = new SolidColorBrush(Color.FromArgb(sparkAlpha, 212, 175, 55));
        context.FillRectangle(sparkBrush, new Rect(x + w / 2 - 1, y - 8, 2, 4));
        context.FillRectangle(sparkBrush, new Rect(x + w / 2 - 5, y - 6, 2, 2));
        context.FillRectangle(sparkBrush, new Rect(x + w / 2 + 3, y - 6, 2, 2));
        context.FillRectangle(sparkBrush, new Rect(x + w / 2 - 7, y - 3, 1, 1));
        context.FillRectangle(sparkBrush, new Rect(x + w / 2 + 6, y - 3, 1, 1));
    }

    private static void DrawCrack(DrawingContext context, double startX, double startY, (int dx, int dy)[] segments)
    {
        var brush = new SolidColorBrush(Color.FromArgb(220, 192, 57, 43));
        double cx = startX, cy = startY;
        foreach (var (dx, dy) in segments)
        {
            // 直線セグメントを 1px の点列で描く (Bresenham 簡易版)
            var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            for (int i = 0; i <= steps; i++)
            {
                var t = (double)i / steps;
                var px = Math.Floor(cx + dx * t);
                var py = Math.Floor(cy + dy * t);
                context.FillRectangle(brush, new Rect(px, py, 1, 1));
            }
            cx += dx;
            cy += dy;
        }
    }

    // ===== Section B: Stage (Designer #1: 魔法陣背景、#4: trail) =====

    private void DrawStage(DrawingContext context)
    {
        var stageRect = new Rect(StageInnerLeft, StageTop, Bounds.Width - StageInnerLeft * 2, StageHeight);

        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1), stageRect);

        // (Designer #1) 魔法陣・同心円・四隅の十字 (常時 ambient)
        DrawMagicCircleBackground(context, stageRect);

        string caption;
        switch (_combat.Phase)
        {
            case CombatPhase.Probing:
                caption = "> probe 送信中 | 応答が返らないスロットを選べ";
                DrawProbeOperation(context, stageRect);
                break;

            case CombatPhase.Filtering:
                caption = "> トラフィック選別中 | バースト(悪性)を遮断し散発(正常)は通す → 適用";
                DrawFilterOperation(context, stageRect);
                break;

            case CombatPhase.Lookup:
                caption = "> 名前解決 | TTL と権威の状態を読み、正しい経路を選べ";
                DrawLookupOperation(context, stageRect);
                break;

            case CombatPhase.Echoing:
                caption = "> シグネチャ | 障害ノード(ジャグ波形)だけに echo を振り分け → 発火";
                DrawEchoOperation(context, stageRect);
                break;

            case CombatPhase.Resolving:
                bool ok = _combat.LastSuccess;
                string banner;
                switch (_combat.LastOp)
                {
                    case CardKind.Filter:
                        caption = ok ? $"> 選別成功 | Block +{_combat.LastDamage}" : "> 選別ミス | 不発";
                        banner = ok ? $"選別成功! Block +{_combat.LastDamage}" : "選別ミス… 不発";
                        break;
                    case CardKind.Lookup:
                        caption = ok ? "> 名前解決成功 | カード +1" : "> 解決ミス | 不発";
                        banner = ok ? "解決成功! カード +1" : "解決ミス… 不発";
                        break;
                    case CardKind.Echo:
                        caption = ok ? "> シグネチャ発動 | 複合効果" : "> 振り分けミス | 不発";
                        banner = ok ? "シグネチャ発動! 障害源-8 / Block+6 / 過負荷弱化" : "振り分けミス… 不発";
                        break;
                    default:
                        caption = ok ? $"> 過負荷源を特定 | 弱化 -{_combat.LastDamage}" : "> 診断ミス | 不発";
                        banner = ok ? $"成功! 過負荷を弱化  -{_combat.LastDamage}" : "診断ミス… 不発";
                        break;
                }
                DrawIdleTopology(context, stageRect);
                DrawCenterBanner(context, stageRect, banner, ok ? Palette.LimeGreenBrush : Palette.CrimsonBrush);
                break;

            case CombatPhase.EnemyTurn:
                caption = "> 敵のターン";
                DrawIdleTopology(context, stageRect);
                DrawCenterBanner(context, stageRect, EnemyTurnMessage(), Palette.CrimsonBrush);
                break;

            default: // Idle / Won / Defeated
                caption = _combat.Energy > 0
                    ? "> カードを使う | Energy 0 なら End Turn"
                    : "> Energy 0 | End Turn で手番終了";
                DrawIdleTopology(context, stageRect);
                break;
        }

        DrawCaptionStrip(context, stageRect, caption);
    }

    // 静的トポロジ (ambient / Idle)。旧 DrawStage の図。
    private string EnemyTurnMessage()
    {
        if (!_combat.EnemyHitApplied) return "敵のターン…";
        switch (_combat.CurrentIntent.Kind)
        {
            case IntentKind.Buff:   return "敵が自己強化した";
            case IntentKind.Debuff: return "遅延を受けた (次の手番のエネルギー減)";
            case IntentKind.Defend: return "敵が防御を固めた";
            default:
                return _combat.LastBlocked > 0
                    ? $"敵の攻撃  -{_combat.LastDamage} HP  ({_combat.LastBlocked} ブロックで吸収)"
                    : $"敵の攻撃  -{_combat.LastDamage} HP";
        }
    }

    private void DrawIdleTopology(DrawingContext context, Rect stageRect)
    {
        var lineY = StageTop + (StageHeight - CaptionStripHeight) / 2;
        var clientX = stageRect.X + 80;
        var serverX = stageRect.Right - 80;

        DrawPixelGlowLine(context, clientX + 20, serverX - 20, lineY);
        DrawNetworkNode(context, clientX, lineY, "Client", Palette.LimeGreenBrush, healthy: true);
        DrawNetworkNode(context, serverX, lineY, "Server", Palette.CrimsonBrush, healthy: false);
        DrawPacketWithTrail(context, clientX + 20, serverX - 20, lineY);
    }

    // ===== probe operation (docs/card-operations.md アーキタイプ1) =====
    // 複数スロットへ probe を送り、応答が返らない (停滞する) スロット = 過負荷源 を読み取らせる。
    // 全スロットは同一見た目で、色で正解を露出しない (ガード条件B)。tell は probe 応答の「動き」のみ。
    private void DrawProbeOperation(DrawingContext context, Rect stageRect)
    {
        var playerX = stageRect.X + 64;
        var slotsMidY = SlotTop + (Combat.SlotCount - 1) * SlotPitch / 2 + SlotHeight / 2;

        DrawNetworkNode(context, playerX, slotsMidY, "自App", Palette.EtherealCyanBrush, healthy: true);

        var instrFt = new FormattedText("probe の応答を観測 — 返らないスロットが過負荷源",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 13, Palette.ParchmentBrush);
        context.DrawText(instrFt, new Point(stageRect.X + (stageRect.Width - instrFt.Width) / 2, StageTop + 6));

        for (int i = 0; i < Combat.SlotCount; i++)
        {
            var slot = SlotRect(i);
            var sy = slot.Y + slot.Height / 2;
            var laneStartX = playerX + 18;

            // 接続レーン
            context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(laneStartX, sy, slot.X - laneStartX, 1));

            // スロット筐体 (全スロット同一見た目)
            context.FillRectangle(Palette.MidnightDeepBrush, slot);
            context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1), slot);
            var lblFt = new FormattedText($"slot {i + 1}", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 12, Palette.ParchmentBrush);
            context.DrawText(lblFt, new Point(slot.X + 10, sy - lblFt.Height / 2));

            var t = _combat.SlotProbe[i];
            if (t >= 1.0)
            {
                // 健全: 応答が帰還済み
                var okFt = new FormattedText("応答 ✓", CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 12, Palette.EtherealCyanBrush);
                context.DrawText(okFt, new Point(slot.Right + 10, sy - okFt.Height / 2));
            }
            else
            {
                // probe 粒の現在位置 (0→0.5 往路, 0.5→1 復路)
                double px = t <= 0.5
                    ? laneStartX + (slot.X - laneStartX) * (t / 0.5)
                    : slot.X - (slot.X - laneStartX) * ((t - 0.5) / 0.5);
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(80, 212, 175, 55)),
                    new Rect(px - 5, sy - 5, 10, 10));
                context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(px - 3, sy - 3, 6, 6));

                // スロットに到達して停滞 (≒0.5) = 応答が返らない。pulse で「待たされている」を示す (色では教えない)。
                // 健全スロットは復路で 0.5 を一瞬で通過するため、停滞中の過負荷スロットにだけ持続表示される。
                if (Math.Abs(t - 0.5) < 0.03)
                {
                    var a = (byte)(120 + Math.Floor(Math.Sin(_pulse * 1.5) * 80));
                    var waitFt = new FormattedText("応答待ち…", CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, Typeface.Default, 12,
                        new SolidColorBrush(Color.FromArgb(a, 244, 232, 193)));
                    context.DrawText(waitFt, new Point(slot.Right + 10, sy - waitFt.Height / 2));
                }
            }
        }
    }

    // ===== filter operation (docs/card-operations.md アーキタイプ2) =====
    // 複数レーンの流量パターンを読み、バースト(悪性)だけを遮断。色で正解を出さない(条件B)。
    // tell はパケット密度 (バースト=連続 / 散発=正常) のみ。全レーンは同一見た目。
    private void DrawFilterOperation(DrawingContext context, Rect stageRect)
    {
        var laneMidY = LaneY(0) + (Combat.LaneCount - 1) * LanePitch / 2;
        var playerX = stageRect.X + 56;
        var enemyX = stageRect.Right - 56;

        DrawNetworkNode(context, playerX, laneMidY, "自App", Palette.EtherealCyanBrush, healthy: true);
        DrawNetworkNode(context, enemyX, laneMidY, "攻撃元", Palette.CrimsonBrush, healthy: false);

        var instr = new FormattedText("流れの密度を読む — バースト(連続)が悪性、散発が正常通信",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 13, Palette.ParchmentBrush);
        context.DrawText(instr, new Point(stageRect.X + (stageRect.Width - instr.Width) / 2, StageTop + 8));

        double laneL = FilterLaneLeft, laneR = FilterLaneRight;
        double gateX = (laneL + laneR) / 2;
        double phase = _combat.FilterElapsed * 0.16;

        for (int i = 0; i < Combat.LaneCount; i++)
        {
            double y = LaneY(i);
            bool mal = _combat.IsLaneMalicious(i);
            bool filt = _combat.IsLaneFiltered(i);

            context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(laneL, y, laneR - laneL, 1));

            // パケット (敵→自App、右→左)。密度のみが tell。色は一律 gold (条件B)。
            int density = mal ? 11 : 3;
            double spacing = mal ? 0.09 : 0.4;
            for (int k = 0; k < density; k++)
            {
                double f = (phase + k * spacing) % 1.0;
                double px = laneR - f * (laneR - laneL);
                if (filt && px <= gateX) continue;          // フィルタ通過後は遮断
                context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(px - 2, y - 2, 4, 4));
            }

            // フィルタゲート (遮断中レーンにシアンのバー + ×)
            if (filt)
            {
                context.FillRectangle(Palette.EtherealCyanBrush, new Rect(gateX - 2, y - 12, 4, 24));
                context.FillRectangle(Palette.EtherealCyanBrush, new Rect(gateX - 8, y - 8, 2, 2));
                context.FillRectangle(Palette.EtherealCyanBrush, new Rect(gateX + 6, y - 8, 2, 2));
                context.FillRectangle(Palette.EtherealCyanBrush, new Rect(gateX - 8, y + 6, 2, 2));
                context.FillRectangle(Palette.EtherealCyanBrush, new Rect(gateX + 6, y + 6, 2, 2));
            }

            // レーン枠 (filtered = シアン枠でトグル状態を示す)
            context.DrawRectangle(null,
                new Pen(filt ? Palette.EtherealCyanBrush : Palette.ArcaneGoldDimBrush, 1), LaneRect(i));
            var lbl = new FormattedText($"lane {i + 1}", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 11, Palette.ParchmentBrush);
            context.DrawText(lbl, new Point(laneL + 4, y - 18));
        }

        // 適用ボタン
        var apply = FilterApplyRect;
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(apply.X + 2, apply.Y + 2, apply.Width, apply.Height));
        context.FillRectangle(Palette.ParchmentAgedBrush, apply);
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 2), apply);
        var applyFt = new FormattedText("適用", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 15, Palette.MidnightBrush);
        context.DrawText(applyFt, new Point(apply.X + (apply.Width - applyFt.Width) / 2, apply.Y + (apply.Height - applyFt.Height) / 2));
    }

    // ===== lookup operation (docs/card-operations.md アーキタイプ3) =====
    // キャッシュ TTL / 権威の動的状態を読み、正しい解決経路を選ぶ。固定の名前→IP対応にしない(条件B)。
    private void DrawLookupOperation(DrawingContext context, Rect stageRect)
    {
        double midY = StageTop + 92;
        double nameX = stageRect.X + 96;
        double cacheX = stageRect.X + stageRect.Width * 0.42;
        double authX = stageRect.X + stageRect.Width * 0.70;

        var instr = new FormattedText("キャッシュ TTL と権威の状態を読み、正しい解決経路を選べ",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 13, Palette.ParchmentBrush);
        context.DrawText(instr, new Point(stageRect.X + (stageRect.Width - instr.Width) / 2, StageTop + 8));

        // 接続線
        context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(nameX + 18, midY, cacheX - nameX - 36, 1));
        context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(cacheX + 18, midY, authX - cacheX - 36, 1));

        // 問い合わせ名ノード
        DrawNetworkNode(context, nameX, midY, "? 名前", Palette.EtherealCyanBrush, healthy: true);

        // キャッシュノード + TTL バー (動的に減る)
        bool ttlValid = _combat.LookupTtl > 0;
        DrawNetworkNode(context, cacheX, midY, "キャッシュ", ttlValid ? Palette.EtherealCyanBrush : Palette.ArcaneGoldDimBrush, healthy: ttlValid);
        var barRect = new Rect(cacheX - 44, midY + 34, 88, 10);
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 1), barRect);
        if (ttlValid)
            context.FillRectangle(Palette.EtherealCyanBrush, new Rect(barRect.X + 1, barRect.Y + 1, (barRect.Width - 2) * _combat.LookupTtl, barRect.Height - 2));
        var ttlFt = new FormattedText(ttlValid ? "TTL 有効" : "TTL 切れ", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 11, ttlValid ? Palette.EtherealCyanBrush : Palette.CrimsonBrush);
        context.DrawText(ttlFt, new Point(cacheX - ttlFt.Width / 2, midY + 48));

        // 権威サーバノード (NXDOMAIN かどうか)
        bool nx = _combat.Scenario == LookupScenario.NxDomain;
        DrawNetworkNode(context, authX, midY, "権威", nx ? Palette.CrimsonBrush : Palette.ArcaneGoldBrush, healthy: !nx);
        var authFt = new FormattedText(nx ? "NXDOMAIN" : "レコード有り", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 11, nx ? Palette.CrimsonBrush : Palette.ParchmentBrush);
        context.DrawText(authFt, new Point(authX - authFt.Width / 2, midY + 34));

        // 3 アクションボタン
        var labels = new[] { "キャッシュ参照", "権威に問い合わせ", "解決不能" };
        for (int i = 0; i < 3; i++)
        {
            var r = LookupActionRect(i);
            context.FillRectangle(Palette.MidnightDeepBrush, new Rect(r.X + 2, r.Y + 2, r.Width, r.Height));
            context.FillRectangle(Palette.ParchmentAgedBrush, r);
            context.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 2), r);
            var bft = new FormattedText(labels[i], CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 14, Palette.MidnightBrush);
            context.DrawText(bft, new Point(r.X + (r.Width - bft.Width) / 2, r.Y + (r.Height - bft.Height) / 2));
        }
    }

    // ===== echo operation (docs/card-operations.md アーキタイプ4 / シグネチャ) =====
    // 自己完結図: 5 つの候補ノードのうち障害(ジャグ波形)だけに echo を振り分ける。色で正解を出さない(条件B)。
    private void DrawEchoOperation(DrawingContext context, Rect stageRect)
    {
        var instr = new FormattedText("障害ノード(乱れた波形)を見分け、複製 echo を振り分けよ — 全正解で発火",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 13, Palette.ParchmentBrush);
        context.DrawText(instr, new Point(stageRect.X + (stageRect.Width - instr.Width) / 2, StageTop + 8));

        int shift = (int)(_combat.EchoElapsed * 6);
        for (int i = 0; i < Combat.EchoTargetCount; i++)
        {
            var r = EchoNodeRect(i);
            bool prob = _combat.IsEchoProblem(i);
            bool marked = _combat.IsEchoMarked(i);

            // ノード筐体 (全ノード同一見た目)
            context.FillRectangle(Palette.MidnightDeepBrush, r);
            context.DrawRectangle(null,
                new Pen(marked ? Palette.EtherealCyanBrush : Palette.ArcaneGoldDimBrush, marked ? 2 : 1), r);

            // signal trace — 障害=ジャグ波形 / 正常=フラット。tell はこれだけ (条件B)。
            double cy = r.Y + 24;
            const int pts = 9;
            double stepX = (r.Width - 20) / (pts - 1);
            for (int k = 0; k < pts; k++)
            {
                double x = r.X + 10 + k * stepX;
                double v = prob ? (((k + shift) % 2 == 0) ? -9 : 9) : 0;
                context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(x - 1, cy + v - 1, 3, 3));
            }

            var lbl = new FormattedText($"node {i + 1}", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 11, Palette.ParchmentBrush);
            context.DrawText(lbl, new Point(r.X + (r.Width - lbl.Width) / 2, r.Bottom - 16));

            // marked: echo リング
            if (marked)
            {
                var em = new FormattedText("((echo))", CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 11, Palette.EtherealCyanBrush);
                context.DrawText(em, new Point(r.X + (r.Width - em.Width) / 2, r.Y + 3));
            }
        }

        // 発火ボタン
        var fire = EchoFireRect;
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(fire.X + 2, fire.Y + 2, fire.Width, fire.Height));
        context.FillRectangle(Palette.ParchmentAgedBrush, fire);
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 2), fire);
        var fireFt = new FormattedText("発火", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 15, Palette.MidnightBrush);
        context.DrawText(fireFt, new Point(fire.X + (fire.Width - fireFt.Width) / 2, fire.Y + (fire.Height - fireFt.Height) / 2));
    }

    private static void DrawCenterBanner(DrawingContext context, Rect stageRect, string text, IBrush brush)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 24, brush);
        var cx = stageRect.X + (stageRect.Width - ft.Width) / 2;
        var cy = stageRect.Y + (stageRect.Height - CaptionStripHeight - ft.Height) / 2;
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(210, 5, 7, 21)),
            new Rect(cx - 18, cy - 10, ft.Width + 36, ft.Height + 20));
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1),
            new Rect(cx - 18, cy - 10, ft.Width + 36, ft.Height + 20));
        context.DrawText(ft, new Point(cx, cy));
    }

    private void DrawMagicCircleBackground(DrawingContext context, Rect stage)
    {
        var cx = stage.X + stage.Width / 2;
        var cy = stage.Y + (stage.Height - CaptionStripHeight) / 2;
        var dim = new SolidColorBrush(Color.FromArgb(50, 138, 111, 31)); // ArcaneGoldDim α

        // 同心円 3 重
        DrawPixelCircle(context, cx, cy, 60, dim);
        DrawPixelCircle(context, cx, cy, 90, dim);
        DrawPixelCircle(context, cx, cy, 120, dim);

        // 四隅に十字 (8x8)
        DrawSmallCross(context, stage.X + 30, stage.Y + 30, dim);
        DrawSmallCross(context, stage.Right - 38, stage.Y + 30, dim);
        DrawSmallCross(context, stage.X + 30, stage.Bottom - CaptionStripHeight - 38, dim);
        DrawSmallCross(context, stage.Right - 38, stage.Bottom - CaptionStripHeight - 38, dim);
    }

    private static void DrawPixelCircle(DrawingContext context, double cx, double cy, double r, IBrush brush)
    {
        // 16 分割の弧で表現 (整数化)
        for (int a = 0; a < 360; a += 6)
        {
            var rad = a * Math.PI / 180;
            var px = Math.Floor(cx + Math.Cos(rad) * r);
            var py = Math.Floor(cy + Math.Sin(rad) * r);
            context.FillRectangle(brush, new Rect(px, py, 2, 2));
        }
    }

    private static void DrawSmallCross(DrawingContext context, double x, double y, IBrush brush)
    {
        context.FillRectangle(brush, new Rect(x + 3, y, 2, 8));
        context.FillRectangle(brush, new Rect(x, y + 3, 8, 2));
    }

    private static void DrawPixelGlowLine(DrawingContext context, double x1, double x2, double y)
    {
        // 3 段の手動 halo (ピクセル規律遵守: Pen は 1-2px のみ)
        // y±3 弱
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(60, 212, 175, 55)),
            new Rect(x1, y - 3, x2 - x1, 1));
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(60, 212, 175, 55)),
            new Rect(x1, y + 3, x2 - x1, 1));
        // y±2 中
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(120, 212, 175, 55)),
            new Rect(x1, y - 2, x2 - x1, 1));
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(120, 212, 175, 55)),
            new Rect(x1, y + 2, x2 - x1, 1));
        // y±1 強
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(200, 212, 175, 55)),
            new Rect(x1, y - 1, x2 - x1, 1));
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(200, 212, 175, 55)),
            new Rect(x1, y + 1, x2 - x1, 1));
        // 中心 (2px Gold)
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(x1, y, x2 - x1, 1));
    }

    private void DrawPacketWithTrail(DrawingContext context, double x1, double x2, double y)
    {
        // 6 段の trail (本体 + 5 個の減衰)
        for (int i = 5; i >= 0; i--)
        {
            var progress = _packetProgress - i * 0.025;
            if (progress < 0 || progress > 1) continue;
            var px = Math.Floor(x1 + progress * (x2 - x1));
            var size = 6 - i; // 6,5,4,3,2,1
            var alpha = (byte)(255 - i * 40);

            if (i == 0)
            {
                // 本体 + halo
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(80, 212, 175, 55)),
                    new Rect(px - 6, y - 6, 12, 12));
                context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(px - 3, y - 3, 6, 6));
                context.FillRectangle(Palette.ParchmentBrush, new Rect(px - 1, y - 1, 2, 2));
            }
            else
            {
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(alpha, 212, 175, 55)),
                    new Rect(px - size / 2.0, y - size / 2.0, size, size));
            }
        }
    }

    private void DrawNetworkNode(DrawingContext context, double cx, double cy, string label, IBrush color, bool healthy)
    {
        const double size = 36;
        var rect = new Rect(cx - size / 2, cy - size / 2, size, size);

        // halo (健常=シアン / 障害=赤)
        var haloColor = healthy ? Color.FromArgb(80, 0, 212, 255) : Color.FromArgb(80, 192, 57, 43);
        context.FillRectangle(new SolidColorBrush(haloColor),
            new Rect(rect.X - 4, rect.Y - 4, size + 8, size + 8));

        context.FillRectangle(color, rect);
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(rect.X + 4, rect.Y + 4, size - 8, size - 8));
        context.FillRectangle(color, new Rect(rect.X + 8, rect.Y + 8, size - 16, size - 16));

        context.FillRectangle(Palette.ParchmentBrush, new Rect(rect.X + 10, rect.Y + 10, 2, 2));

        var ft = new FormattedText(label, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 10, Palette.ParchmentBrush);
        context.DrawText(ft, new Point(cx - ft.Width / 2, cy + size / 2 + 6));
    }

    private void DrawCaptionStrip(DrawingContext context, Rect stage, string text)
    {
        var stripRect = new Rect(stage.X + 2, stage.Bottom - CaptionStripHeight - 2,
            stage.Width - 4, CaptionStripHeight);
        context.FillRectangle(Palette.ParchmentBrush, stripRect);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(180, 10, 14, 46)), stripRect);
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(stripRect.X, stripRect.Y, stripRect.Width, 1));
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(stripRect.X, stripRect.Bottom - 1, stripRect.Width, 1));

        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 12, Palette.ParchmentBrush);
        context.DrawText(ft, new Point(stripRect.X + 12, stripRect.Y + 8));
    }

    // ===== Section C: Player Strip =====

    private void DrawPlayerStrip(DrawingContext context)
    {
        const double areaLeft = BookFrameMargin + 20;
        var areaWidth = Bounds.Width - (BookFrameMargin + 20) * 2;

        var stripRect = new Rect(areaLeft, PlayerStripTop, areaWidth, PlayerStripHeight);
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1), stripRect);

        DrawBar(context, areaLeft + 20, PlayerStripTop + 18, 220, 14, _combat.PlayerHp, Combat.PlayerHpMax,
            Palette.LimeGreenBrush, $"HP {_combat.PlayerHp}/{Combat.PlayerHpMax}");

        // ブロック表示 (>0 のときシアンの盾チップ。filter #3 が生成)
        if (_combat.PlayerBlock > 0)
            DrawBlockChip(context, areaLeft + 300, PlayerStripTop + 14, _combat.PlayerBlock);

        DrawEnergyGems(context, areaLeft + 360, PlayerStripTop + 14, _combat.Energy, Combat.EnergyMax);

        var pileFt = new FormattedText($"山札 {_combat.DrawPileCount} ・ 捨て {_combat.DiscardPileCount}",
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 12, Palette.ParchmentBrush);
        context.DrawText(pileFt, new Point(areaLeft + 600, PlayerStripTop + 18));

        // 遅延デバフ (敵が付与、次の手番のエネルギーを絞る)
        if (_combat.PlayerLatency > 0)
        {
            var latFt = new FormattedText($"遅延 x{_combat.PlayerLatency}", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 12, Palette.CrimsonBrush);
            context.DrawText(latFt, new Point(areaLeft + 600, PlayerStripTop + 2));
        }

        DrawEndTurnButton(context, EndTurnRect, _combat.Phase == CombatPhase.Idle);
    }

    private static void DrawBlockChip(DrawingContext context, double x, double y, int block)
    {
        var cyan = Palette.EtherealCyanBrush;
        // 小さな盾 (pixel)
        context.FillRectangle(cyan, new Rect(x, y, 14, 4));
        context.FillRectangle(cyan, new Rect(x + 1, y + 4, 12, 6));
        context.FillRectangle(cyan, new Rect(x + 3, y + 10, 8, 4));
        context.FillRectangle(cyan, new Rect(x + 5, y + 14, 4, 3));
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(x + 4, y + 5, 6, 4)); // 中抜き
        var ft = new FormattedText(block.ToString(), CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 13, cyan);
        context.DrawText(ft, new Point(x + 20, y + 2));
    }

    private void DrawEnergyGems(DrawingContext context, double x, double y, int current, int max)
    {
        var labelFt = new FormattedText("Energy", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 11, Palette.ParchmentBrush);
        context.DrawText(labelFt, new Point(x, y));

        const double gemSize = 14;
        const double gap = 6;
        var gemsX = x + 56;
        for (int i = 0; i < max; i++)
        {
            var gx = gemsX + i * (gemSize + gap);
            var active = i < current;
            var brush = active ? Palette.ArcaneGoldBrush : Palette.ArcaneGoldDimBrush;
            if (active)
            {
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(60, 212, 175, 55)),
                    new Rect(gx - 2, y - 2, gemSize + 4, gemSize + 4));
            }
            context.FillRectangle(brush, new Rect(gx + 4, y, 6, 4));
            context.FillRectangle(brush, new Rect(gx + 2, y + 4, 10, 6));
            context.FillRectangle(brush, new Rect(gx + 4, y + 10, 6, 4));
            if (active)
                context.FillRectangle(Palette.ParchmentBrush, new Rect(gx + 5, y + 5, 2, 2));
        }

        var countFt = new FormattedText($"{current}/{max}", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 11, Palette.ParchmentBrush);
        context.DrawText(countFt, new Point(gemsX + max * (gemSize + gap) + 8, y));
    }

    private static void DrawEndTurnButton(DrawingContext context, Rect rect, bool enabled)
    {
        var faceBrush = enabled ? Palette.ParchmentAgedBrush : Palette.MidnightBrush;
        var frameBrush = enabled ? Palette.ArcaneGoldBrush : Palette.ArcaneGoldDimBrush;
        var textBrush = enabled ? Palette.MidnightBrush : Palette.ArcaneGoldDimBrush;

        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(rect.X + 2, rect.Y + 2, rect.Width, rect.Height));
        context.FillRectangle(faceBrush, rect);
        if (enabled)
            context.FillRectangle(Palette.ParchmentBrush, new Rect(rect.X + 2, rect.Y + 2, rect.Width - 4, 3));
        context.DrawRectangle(null, new Pen(frameBrush, 2), rect);
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1),
            new Rect(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8));

        var ft = new FormattedText("End Turn", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 15, textBrush);
        context.DrawText(ft, new Point(rect.X + (rect.Width - ft.Width) / 2, rect.Y + (rect.Height - ft.Height) / 2));
    }

    // ===== Section D: Hand =====

    private void DrawHand(DrawingContext context)
    {
        for (int i = 0; i < _combat.Hand.Count; i++)
        {
            var card = _combat.Hand[i];
            var rect = CardRectAt(i);
            // active = この手番で実際に使える (Idle / 操作実装済み / Energy 足りる) — style-guide §6 枠色
            var active = _combat.Phase == CombatPhase.Idle
                         && card.OperationImplemented
                         && _combat.Energy >= card.Cost;
            DrawCard(context, rect.X, rect.Y, card.Name, card.Cost, card.Effect, IconFor(card.Kind), active);
        }
    }

    private void DrawCard(DrawingContext context, double x, double y, string name, int cost, string effect, CardIcon icon, bool active)
    {
        var cardRect = new Rect(x, y, CardWidth, CardHeight);
        var frameBrush = active ? Palette.ArcaneGoldBrush : Palette.ArcaneGoldDimBrush;

        // 影
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(x + 3, y + 3, CardWidth, CardHeight));

        context.FillRectangle(Palette.MidnightBrush, new Rect(x, y, CardWidth, CardHeight / 2));
        context.FillRectangle(Palette.ParchmentBrush, new Rect(x, y + CardHeight / 2, CardWidth, CardHeight / 2));
        context.FillRectangle(frameBrush, new Rect(x, y + CardHeight / 2, CardWidth, 2));
        context.DrawRectangle(null, new Pen(frameBrush, 3), cardRect);
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1),
            new Rect(x + 4, y + 4, CardWidth - 8, CardHeight - 8));

        var jpFamily = new FontFamily("Hiragino Sans, Yu Gothic, Noto Sans CJK JP, sans-serif");
        var nameTypeface = new Typeface(jpFamily, FontStyle.Normal, FontWeight.Normal);
        var nameFt = new FormattedText(name, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, nameTypeface, 15, Palette.ParchmentBrush);
        context.DrawText(nameFt, new Point(x + (CardWidth - nameFt.Width) / 2, y + 12));

        DrawCostGem(context, x + CardWidth - 30, y + 10, cost);

        var iconRect = new Rect(x + (CardWidth - 80) / 2, y + 38, 80, 64);
        context.FillRectangle(Palette.MidnightDeepBrush, iconRect);
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1), iconRect);
        DrawCardIcon(context, iconRect, icon);

        var lines = effect.Split('\n');
        var effectTypeface = new Typeface(jpFamily, FontStyle.Normal, FontWeight.Normal);
        var inkBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        for (int i = 0; i < lines.Length; i++)
        {
            var ft = new FormattedText(lines[i], CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, effectTypeface, 16, inkBrush);
            context.DrawText(ft, new Point(x + (CardWidth - ft.Width) / 2, y + CardHeight / 2 + 16 + i * 22));
        }
    }

    private static void DrawCostGem(DrawingContext context, double x, double y, int cost)
    {
        const double w = 22, h = 26;
        // cost=0 は dim 表現
        var gemBrush = cost == 0 ? Palette.ArcaneGoldDimBrush : Palette.ArcaneGoldBrush;

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(80, 212, 175, 55)),
            new Rect(x - 2, y - 2, w + 4, h + 4));
        context.FillRectangle(gemBrush, new Rect(x + 5, y, w - 10, 5));
        context.FillRectangle(gemBrush, new Rect(x + 2, y + 5, w - 4, 16));
        context.FillRectangle(gemBrush, new Rect(x + 5, y + 21, w - 10, 5));
        context.FillRectangle(Palette.ParchmentBrush, new Rect(x + 7, y + 6, 2, 2));
        var ft = new FormattedText(cost.ToString(), CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 14, Palette.MidnightBrush);
        context.DrawText(ft, new Point(x + (w - ft.Width) / 2, y + (h - ft.Height) / 2 - 1));
    }

    // ===== カード固有アイコン =====

    private void DrawCardIcon(DrawingContext context, Rect rect, CardIcon icon)
    {
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        switch (icon)
        {
            case CardIcon.ProbeRadar:   DrawProbeRadar(context, cx, cy); break;
            case CardIcon.ShieldFilter: DrawShieldFilter(context, cx, cy); break;
            case CardIcon.EchoLoop:     DrawEchoLoop(context, cx, cy); break;
            case CardIcon.LookupGlass:  DrawLookupGlass(context, cx, cy); break;
            case CardIcon.RetryArrow:   DrawRetryArrow(context, cx, cy); break;
        }
    }

    private void DrawProbeRadar(DrawingContext context, double cx, double cy)
    {
        var gold = Palette.ArcaneGoldBrush;
        var dim = Palette.ArcaneGoldDimBrush;
        context.FillRectangle(gold, new Rect(cx - 2, cy - 2, 4, 4));
        var pulseR = Math.Floor(Math.Sin(_pulse) * 2);
        DrawPixelArc(context, cx, cy, 8 + pulseR, gold);
        DrawPixelArc(context, cx, cy, 14, dim);
        DrawPixelArc(context, cx, cy, 20, dim);
    }

    private static void DrawPixelArc(DrawingContext context, double cx, double cy, double r, IBrush brush)
    {
        for (int a = -40; a <= 40; a += 8)
        {
            var rad = a * Math.PI / 180;
            var px = Math.Floor(cx + Math.Sin(rad) * r);
            var py = Math.Floor(cy - Math.Cos(rad) * r);
            context.FillRectangle(brush, new Rect(px - 1, py - 1, 2, 2));
        }
    }

    private static void DrawShieldFilter(DrawingContext context, double cx, double cy)
    {
        var cyan = Palette.EtherealCyanBrush;
        var cyanDim = Palette.EtherealCyanDimBrush;
        context.FillRectangle(cyan, new Rect(cx - 10, cy - 14, 20, 4));
        context.FillRectangle(cyan, new Rect(cx - 12, cy - 10, 24, 12));
        context.FillRectangle(cyan, new Rect(cx - 8, cy + 2, 16, 6));
        context.FillRectangle(cyan, new Rect(cx - 4, cy + 8, 8, 6));
        context.FillRectangle(cyanDim, new Rect(cx + 4, cy - 8, 6, 8));
        context.FillRectangle(Palette.ParchmentBrush, new Rect(cx - 1, cy - 6, 2, 8));
        context.FillRectangle(Palette.ParchmentBrush, new Rect(cx - 4, cy - 3, 8, 2));
    }

    private static void DrawEchoLoop(DrawingContext context, double cx, double cy)
    {
        var gold = Palette.ArcaneGoldBrush;
        var dim = Palette.ArcaneGoldDimBrush;
        context.FillRectangle(gold, new Rect(cx - 12, cy - 2, 18, 3));
        context.FillRectangle(gold, new Rect(cx - 12, cy + 4, 18, 3));
        context.FillRectangle(gold, new Rect(cx + 6, cy - 2, 3, 9));
        context.FillRectangle(gold, new Rect(cx - 14, cy - 4, 3, 3));
        context.FillRectangle(gold, new Rect(cx - 16, cy - 2, 3, 3));
        context.FillRectangle(gold, new Rect(cx - 14, cy, 3, 3));
        context.FillRectangle(dim, new Rect(cx + 10, cy + 6, 2, 2));
    }

    private static void DrawLookupGlass(DrawingContext context, double cx, double cy)
    {
        var gold = Palette.ArcaneGoldBrush;
        var parchment = Palette.ParchmentBrush;
        var lensX = cx - 12;
        var lensY = cy - 14;
        const double lensSize = 18;
        context.FillRectangle(gold, new Rect(lensX, lensY, lensSize, 3));
        context.FillRectangle(gold, new Rect(lensX, lensY + lensSize - 3, lensSize, 3));
        context.FillRectangle(gold, new Rect(lensX, lensY, 3, lensSize));
        context.FillRectangle(gold, new Rect(lensX + lensSize - 3, lensY, 3, lensSize));
        context.FillRectangle(parchment, new Rect(lensX + 4, lensY + 4, 4, 4));
        context.FillRectangle(gold, new Rect(cx + 4, cy + 2, 4, 4));
        context.FillRectangle(gold, new Rect(cx + 7, cy + 5, 4, 4));
        context.FillRectangle(gold, new Rect(cx + 10, cy + 8, 4, 4));
    }

    private static void DrawRetryArrow(DrawingContext context, double cx, double cy)
    {
        var gold = Palette.ArcaneGoldBrush;
        context.FillRectangle(gold, new Rect(cx - 8, cy - 12, 14, 3));
        context.FillRectangle(gold, new Rect(cx + 4, cy - 9, 3, 12));
        context.FillRectangle(gold, new Rect(cx - 6, cy, 12, 3));
        context.FillRectangle(gold, new Rect(cx - 8, cy - 12, 3, 12));
        context.FillRectangle(gold, new Rect(cx + 7, cy + 1, 3, 3));
        context.FillRectangle(gold, new Rect(cx + 9, cy + 3, 3, 3));
        context.FillRectangle(gold, new Rect(cx + 7, cy + 5, 3, 3));
    }

    // ===== 共通ヘルパー =====

    private static void DrawBar(DrawingContext context, double x, double y, double w, double h,
        int value, int max, IBrush fill, string label)
    {
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(x + 1, y + 1, w, h));
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 1), new Rect(x, y, w, h));
        var ratio = (double)value / max;
        context.FillRectangle(fill, new Rect(x + 1, y + 1, (w - 2) * ratio, h - 2));
        var ft = new FormattedText(label, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 11, Palette.ParchmentBrush);
        context.DrawText(ft, new Point(x + w + 8, y - 1));
    }

}
