using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Grimoire;

// docs/style-guide.md §5「レイアウト（戦闘画面ベース）」を実装する CombatView。
// 第3イテレーション (richness pass): 星空背景・ライン発光・粒アニメ・カード固有アイコン・フィリグリー詳細。
// カードクリック・ターン構造は後続イテレーションで追加。
public class CombatView : Control
{
    private readonly DispatcherTimer _timer;

    // 寸法 (style-guide §5, 1280x720 基準)
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
    private const double CardWidth = 144;
    private const double CardHeight = 200;

    // ゲーム状態 (静的サンプル、後続で GameState に切り出す)
    private readonly int _playerHp = 28;
    private readonly int _playerHpMax = 30;
    private readonly int _energy = 3;
    private readonly int _energyMax = 3;
    private readonly int _enemyHp = 24;
    private readonly int _enemyHpMax = 30;
    private readonly int _enemyIntent = 5;

    // 手札データ (docs/classes.md の決定通り)
    private enum CardIcon { ProbeRadar, ShieldFilter, EchoLoop, LookupGlass, RetryArrow }
    private readonly (string Name, int Cost, string Effect, CardIcon Icon)[] _hand =
    {
        ("Probe Request", 1, "障害源を\n5 弱化",      CardIcon.ProbeRadar),
        ("Packet Filter", 1, "ブロック\n+5 生成",      CardIcon.ShieldFilter),
        ("Echo Reply",    0, "前回効果\nもう一度",     CardIcon.EchoLoop),
        ("Lookup",        1, "カード\n+1 ドロー",      CardIcon.LookupGlass),
        ("Retry",         1, "失敗カード\n再使用可",   CardIcon.RetryArrow),
    };

    // 星空 (Midnight 背景の constellation 効果)
    private readonly List<(double X, double Y, byte Alpha, int Size)> _stars = new();

    // アニメーション
    private double _packetProgress; // 0..1 (client から server へ)
    private double _pulse;            // sin pulse for breathing effects

    public CombatView()
    {
        // 星空生成 (固定 seed で再現性)
        var rng = new Random(42);
        for (int i = 0; i < 80; i++)
        {
            _stars.Add((
                rng.NextDouble() * 1280,
                rng.NextDouble() * 720,
                (byte)(40 + rng.Next(120)),
                rng.Next(3) == 0 ? 2 : 1
            ));
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _packetProgress += 0.006;
        if (_packetProgress > 1.05) _packetProgress = -0.05;
        _pulse += 0.06;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        DrawPagesBackground(context);
        DrawStarfield(context);
        DrawBookFrame(context);
        DrawEnemyArea(context);
        DrawStage(context);
        DrawPlayerStrip(context);
        DrawHand(context);
    }

    // ===== 背景 =====

    private void DrawPagesBackground(DrawingContext context)
    {
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(Bounds.Size));
        var pages = new Rect(BookFrameMargin, 0, Bounds.Width - BookFrameMargin * 2, Bounds.Height);
        context.FillRectangle(Palette.MidnightBrush, pages);
    }

    private void DrawStarfield(DrawingContext context)
    {
        var pages = new Rect(BookFrameMargin, 0, Bounds.Width - BookFrameMargin * 2, Bounds.Height);
        foreach (var s in _stars)
        {
            if (s.X < pages.Left || s.X > pages.Right) continue;
            var brush = new SolidColorBrush(Color.FromArgb(s.Alpha, 212, 175, 55)); // gold tint
            context.FillRectangle(brush, new Rect(s.X, s.Y, s.Size, s.Size));
        }
    }

    // ===== ブックフレーム (richness: 内側アクセント追加) =====

    private void DrawBookFrame(DrawingContext context)
    {
        context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(BookFrameMargin - 2, 0, 2, Bounds.Height));
        context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(Bounds.Width - BookFrameMargin, 0, 2, Bounds.Height));

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

        // 外側 L字 (太め)
        switch (c)
        {
            case Corner.TopLeft:
                context.FillRectangle(gold, new Rect(x, y, len, thick));
                context.FillRectangle(gold, new Rect(x, y, thick, len));
                // 内側 L字 (細め、dim)
                context.FillRectangle(dim, new Rect(x + thick + 4, y + thick + 4, len - thick - 8, 2));
                context.FillRectangle(dim, new Rect(x + thick + 4, y + thick + 4, 2, len - thick - 8));
                // 装飾ドット
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

    // ===== Section A: Enemy Area =====

    private void DrawEnemyArea(DrawingContext context)
    {
        const double areaLeft = BookFrameMargin + 20;
        var centerX = Bounds.Width / 2;

        DrawServerRack(context, centerX - 40, EnemyAreaTop + 5, 80, 110);

        var nameFt = new FormattedText("障害サーバ / Overload Server", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 14, Palette.CrimsonBrush);
        context.DrawText(nameFt, new Point(areaLeft + 20, EnemyAreaTop + 8));

        DrawBar(context, areaLeft + 20, EnemyAreaTop + 36, 240, 14, _enemyHp, _enemyHpMax,
            Palette.CrimsonBrush, $"HP {_enemyHp}/{_enemyHpMax}");

        DrawIntent(context, areaLeft + 20, EnemyAreaTop + 64, _enemyIntent);
    }

    private void DrawServerRack(DrawingContext context, double x, double y, double w, double h)
    {
        // 影 (1px ずれ)
        context.FillRectangle(Palette.CrimsonDimBrush, new Rect(x + 2, y + 2, w, h));
        // 本体
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(x, y, w, h));

        // 赤い外枠
        const double frame = 2;
        context.FillRectangle(Palette.CrimsonBrush, new Rect(x, y, w, frame));
        context.FillRectangle(Palette.CrimsonBrush, new Rect(x, y + h - frame, w, frame));
        context.FillRectangle(Palette.CrimsonBrush, new Rect(x, y, frame, h));
        context.FillRectangle(Palette.CrimsonBrush, new Rect(x + w - frame, y, frame, h));

        // スロット 5段 (グロウあり)
        for (int i = 0; i < 5; i++)
        {
            var rowY = y + 8 + i * 18;
            context.FillRectangle(Palette.CrimsonDimBrush, new Rect(x + 6, rowY, w - 12, 12));
            // 微細なライン
            context.FillRectangle(Palette.CrimsonBrush, new Rect(x + 8, rowY + 2, w - 16, 1));
            context.FillRectangle(Palette.CrimsonBrush, new Rect(x + 8, rowY + 8, w - 16, 1));
            // LED (パルス)
            var pulse = (byte)(180 + Math.Sin(_pulse + i) * 60);
            var ledBrush = new SolidColorBrush(Color.FromArgb(pulse, 192, 57, 43));
            context.FillRectangle(ledBrush, new Rect(x + w - 12, rowY + 4, 4, 4));
        }

        // 火花 (pulse)
        var sparkAlpha = (byte)(120 + Math.Sin(_pulse * 2) * 100);
        var sparkBrush = new SolidColorBrush(Color.FromArgb(sparkAlpha, 212, 175, 55));
        context.FillRectangle(sparkBrush, new Rect(x + w / 2 - 1, y - 8, 2, 4));
        context.FillRectangle(sparkBrush, new Rect(x + w / 2 - 5, y - 6, 2, 2));
        context.FillRectangle(sparkBrush, new Rect(x + w / 2 + 3, y - 6, 2, 2));
        context.FillRectangle(sparkBrush, new Rect(x + w / 2 - 7, y - 3, 1, 1));
        context.FillRectangle(sparkBrush, new Rect(x + w / 2 + 6, y - 3, 1, 1));
    }

    // ===== Section B: Stage (§5 動く図) =====

    private void DrawStage(DrawingContext context)
    {
        const double areaLeft = BookFrameMargin + 20;
        var areaWidth = Bounds.Width - (BookFrameMargin + 20) * 2;
        var stageRect = new Rect(areaLeft, StageTop, areaWidth, StageHeight);

        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1), stageRect);

        var lineY = StageTop + (StageHeight - CaptionStripHeight) / 2;
        var clientX = stageRect.X + 80;
        var serverX = stageRect.Right - 80;

        // 接続線 (glow 効果: 3 層の同心ピクセル線)
        DrawGlowLine(context, clientX + 20, lineY, serverX - 20, lineY);

        DrawNetworkNode(context, clientX, lineY, "Client", Palette.LimeGreenBrush, healthy: true);
        DrawNetworkNode(context, serverX, lineY, "Server", Palette.CrimsonBrush, healthy: false);

        // 移動中のパケット (粒)
        var packetX = clientX + 20 + (_packetProgress) * (serverX - clientX - 40);
        if (_packetProgress >= 0 && _packetProgress <= 1)
        {
            // glow halo
            var haloBrush = new SolidColorBrush(Color.FromArgb(80, 212, 175, 55));
            context.FillRectangle(haloBrush, new Rect(packetX - 6, lineY - 6, 12, 12));
            // 中心
            context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(packetX - 3, lineY - 3, 6, 6));
            // ハイライト
            context.FillRectangle(Palette.ParchmentBrush, new Rect(packetX - 1, lineY - 1, 2, 2));
        }

        DrawCaptionStrip(context, stageRect, "> ブレーカー Closed | 失敗 1/3");
    }

    private static void DrawGlowLine(DrawingContext context, double x1, double y1, double x2, double y2)
    {
        // 外側 (最も薄い、太い)
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(60, 212, 175, 55)), 8),
            new Point(x1, y1), new Point(x2, y2));
        // 中間
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(120, 212, 175, 55)), 4),
            new Point(x1, y1), new Point(x2, y2));
        // 内側 (鮮明)
        context.DrawLine(new Pen(Palette.ArcaneGoldBrush, 2),
            new Point(x1, y1), new Point(x2, y2));
    }

    private void DrawNetworkNode(DrawingContext context, double cx, double cy, string label, IBrush color, bool healthy)
    {
        const double size = 36;
        var rect = new Rect(cx - size / 2, cy - size / 2, size, size);

        // glow halo (健常はシアン、障害は赤)
        var haloColor = healthy ? Color.FromArgb(80, 0, 212, 255) : Color.FromArgb(80, 192, 57, 43);
        context.FillRectangle(new SolidColorBrush(haloColor),
            new Rect(rect.X - 4, rect.Y - 4, size + 8, size + 8));

        context.FillRectangle(color, rect);
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(rect.X + 4, rect.Y + 4, size - 8, size - 8));
        context.FillRectangle(color, new Rect(rect.X + 8, rect.Y + 8, size - 16, size - 16));

        // ハイライトドット (左上)
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
        // パーチメントの陰影
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(180, 10, 14, 46)), stripRect);
        // 上下に金線
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

        DrawBar(context, areaLeft + 20, PlayerStripTop + 18, 220, 14, _playerHp, _playerHpMax,
            Palette.LimeGreenBrush, $"HP {_playerHp}/{_playerHpMax}");

        DrawEnergyGems(context, areaLeft + 360, PlayerStripTop + 14, _energy, _energyMax);

        var btnRect = new Rect(stripRect.Right - 140, PlayerStripTop + 8, 124, 34);
        DrawEndTurnButton(context, btnRect);
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
            // ダイヤ + 軽い glow
            if (active)
            {
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(60, 212, 175, 55)),
                    new Rect(gx - 2, y - 2, gemSize + 4, gemSize + 4));
            }
            context.FillRectangle(brush, new Rect(gx + 4, y, 6, 4));
            context.FillRectangle(brush, new Rect(gx + 2, y + 4, 10, 6));
            context.FillRectangle(brush, new Rect(gx + 4, y + 10, 6, 4));
            // ハイライト
            if (active)
            {
                context.FillRectangle(Palette.ParchmentBrush, new Rect(gx + 5, y + 5, 2, 2));
            }
        }

        var countFt = new FormattedText($"{current}/{max}", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 11, Palette.ParchmentBrush);
        context.DrawText(countFt, new Point(gemsX + max * (gemSize + gap) + 8, y));
    }

    private static void DrawEndTurnButton(DrawingContext context, Rect rect)
    {
        // 影
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(rect.X + 2, rect.Y + 2, rect.Width, rect.Height));
        // パーチメント本体
        context.FillRectangle(Palette.ParchmentAgedBrush, rect);
        // 上ハイライト
        context.FillRectangle(Palette.ParchmentBrush, new Rect(rect.X + 2, rect.Y + 2, rect.Width - 4, 3));
        // ゴールド外枠
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 2), rect);
        // 内側細枠
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1),
            new Rect(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8));

        var ft = new FormattedText("End Turn", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 15, Palette.MidnightBrush);
        context.DrawText(ft, new Point(rect.X + (rect.Width - ft.Width) / 2, rect.Y + (rect.Height - ft.Height) / 2));
    }

    // ===== Section D: Hand =====

    private void DrawHand(DrawingContext context)
    {
        const double areaLeft = BookFrameMargin + 20;
        var areaWidth = Bounds.Width - (BookFrameMargin + 20) * 2;

        const double cardGap = 12;
        var totalWidth = _hand.Length * CardWidth + (_hand.Length - 1) * cardGap;
        var startX = areaLeft + (areaWidth - totalWidth) / 2;
        var cardY = HandTop + (HandHeight - CardHeight) / 2;

        for (int i = 0; i < _hand.Length; i++)
        {
            var x = startX + i * (CardWidth + cardGap);
            DrawCard(context, x, cardY, _hand[i].Name, _hand[i].Cost, _hand[i].Effect, _hand[i].Icon);
        }
    }

    private void DrawCard(DrawingContext context, double x, double y, string name, int cost, string effect, CardIcon icon)
    {
        var cardRect = new Rect(x, y, CardWidth, CardHeight);

        // 影
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(x + 3, y + 3, CardWidth, CardHeight));

        // 上半分: Midnight (アート領域)
        context.FillRectangle(Palette.MidnightBrush, new Rect(x, y, CardWidth, CardHeight / 2));
        // 下半分: Parchment (説明領域)
        context.FillRectangle(Palette.ParchmentBrush, new Rect(x, y + CardHeight / 2, CardWidth, CardHeight / 2));

        // 中央 separator (gold 線)
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(x, y + CardHeight / 2, CardWidth, 2));

        // 外枠
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 3), cardRect);

        // 内枠 (細)
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1),
            new Rect(x + 4, y + 4, CardWidth - 8, CardHeight - 8));

        // カード名 (上部中央)
        var nameFt = new FormattedText(name, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 12, Palette.ParchmentBrush);
        context.DrawText(nameFt, new Point(x + (CardWidth - nameFt.Width) / 2, y + 10));

        // コスト (右上ゴールドジェム)
        DrawCostGem(context, x + CardWidth - 26, y + 8, cost);

        // アイコン枠 + アイコン
        var iconRect = new Rect(x + (CardWidth - 64) / 2, y + 32, 64, 56);
        context.FillRectangle(Palette.MidnightDeepBrush, iconRect);
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1), iconRect);
        DrawCardIcon(context, iconRect, icon);

        // 効果説明
        var lines = effect.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var ft = new FormattedText(lines[i], CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 11, Palette.MidnightBrush);
            context.DrawText(ft, new Point(x + (CardWidth - ft.Width) / 2, y + CardHeight / 2 + 14 + i * 16));
        }
    }

    private static void DrawCostGem(DrawingContext context, double x, double y, int cost)
    {
        const double w = 18, h = 22;
        // halo
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(80, 212, 175, 55)),
            new Rect(x - 2, y - 2, w + 4, h + 4));
        // ジェム本体
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(x + 4, y, w - 8, 4));
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(x + 2, y + 4, w - 4, 12));
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(x + 4, y + 16, w - 8, 4));
        // ハイライト
        context.FillRectangle(Palette.ParchmentBrush, new Rect(x + 6, y + 4, 2, 2));
        // コスト数字
        var ft = new FormattedText(cost.ToString(), CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 12, Palette.MidnightBrush);
        context.DrawText(ft, new Point(x + (w - ft.Width) / 2, y + (h - ft.Height) / 2 - 1));
    }

    // ===== カード固有のピクセルアートアイコン =====

    private void DrawCardIcon(DrawingContext context, Rect rect, CardIcon icon)
    {
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;

        switch (icon)
        {
            case CardIcon.ProbeRadar:
                DrawProbeRadar(context, cx, cy);
                break;
            case CardIcon.ShieldFilter:
                DrawShieldFilter(context, cx, cy);
                break;
            case CardIcon.EchoLoop:
                DrawEchoLoop(context, cx, cy);
                break;
            case CardIcon.LookupGlass:
                DrawLookupGlass(context, cx, cy);
                break;
            case CardIcon.RetryArrow:
                DrawRetryArrow(context, cx, cy);
                break;
        }
    }

    private void DrawProbeRadar(DrawingContext context, double cx, double cy)
    {
        // 同心円状のレーダー波 (pixel art arcs)
        var gold = Palette.ArcaneGoldBrush;
        var dim = Palette.ArcaneGoldDimBrush;

        // 内側コア (送信源)
        context.FillRectangle(gold, new Rect(cx - 2, cy - 2, 4, 4));
        // 弧 (3 段)
        var pulseR = Math.Sin(_pulse) * 2;
        DrawPixelArc(context, cx, cy, 8 + pulseR, gold);
        DrawPixelArc(context, cx, cy, 14, dim);
        DrawPixelArc(context, cx, cy, 20, dim);
    }

    private static void DrawPixelArc(DrawingContext context, double cx, double cy, double r, IBrush brush)
    {
        // 簡易ピクセル弧 (上向き 60°、点線で表現)
        for (int a = -40; a <= 40; a += 8)
        {
            var rad = a * Math.PI / 180;
            var px = cx + Math.Sin(rad) * r;
            var py = cy - Math.Cos(rad) * r;
            context.FillRectangle(brush, new Rect(px - 1, py - 1, 2, 2));
        }
    }

    private static void DrawShieldFilter(DrawingContext context, double cx, double cy)
    {
        // 盾型 (Slay the Spire 風)
        var cyan = Palette.EtherealCyanBrush;
        var cyanDim = Palette.EtherealCyanDimBrush;

        // 盾本体 (上が広く下が細い)
        context.FillRectangle(cyan, new Rect(cx - 10, cy - 14, 20, 4));
        context.FillRectangle(cyan, new Rect(cx - 12, cy - 10, 24, 12));
        context.FillRectangle(cyan, new Rect(cx - 8, cy + 2, 16, 6));
        context.FillRectangle(cyan, new Rect(cx - 4, cy + 8, 8, 6));
        // 影
        context.FillRectangle(cyanDim, new Rect(cx + 4, cy - 8, 6, 8));
        // 中央クロス
        context.FillRectangle(Palette.ParchmentBrush, new Rect(cx - 1, cy - 6, 2, 8));
        context.FillRectangle(Palette.ParchmentBrush, new Rect(cx - 4, cy - 3, 8, 2));
    }

    private static void DrawEchoLoop(DrawingContext context, double cx, double cy)
    {
        // 循環矢印 (左→右→上→戻る)
        var gold = Palette.ArcaneGoldBrush;
        var dim = Palette.ArcaneGoldDimBrush;
        // 横線
        context.FillRectangle(gold, new Rect(cx - 12, cy - 2, 18, 3));
        context.FillRectangle(gold, new Rect(cx - 12, cy + 4, 18, 3));
        // 右側カーブ
        context.FillRectangle(gold, new Rect(cx + 6, cy - 2, 3, 9));
        // 矢じり (左)
        context.FillRectangle(gold, new Rect(cx - 14, cy - 4, 3, 3));
        context.FillRectangle(gold, new Rect(cx - 16, cy - 2, 3, 3));
        context.FillRectangle(gold, new Rect(cx - 14, cy, 3, 3));
        // dim sparkle
        context.FillRectangle(dim, new Rect(cx + 10, cy + 6, 2, 2));
    }

    private static void DrawLookupGlass(DrawingContext context, double cx, double cy)
    {
        // 虫眼鏡
        var gold = Palette.ArcaneGoldBrush;
        var parchment = Palette.ParchmentBrush;

        // レンズ (中空の正方形)
        var lensX = cx - 12;
        var lensY = cy - 14;
        const double lensSize = 18;
        // 外枠
        context.FillRectangle(gold, new Rect(lensX, lensY, lensSize, 3));
        context.FillRectangle(gold, new Rect(lensX, lensY + lensSize - 3, lensSize, 3));
        context.FillRectangle(gold, new Rect(lensX, lensY, 3, lensSize));
        context.FillRectangle(gold, new Rect(lensX + lensSize - 3, lensY, 3, lensSize));
        // レンズ内ハイライト
        context.FillRectangle(parchment, new Rect(lensX + 4, lensY + 4, 4, 4));
        // 柄 (斜め)
        context.FillRectangle(gold, new Rect(cx + 4, cy + 2, 4, 4));
        context.FillRectangle(gold, new Rect(cx + 7, cy + 5, 4, 4));
        context.FillRectangle(gold, new Rect(cx + 10, cy + 8, 4, 4));
    }

    private static void DrawRetryArrow(DrawingContext context, double cx, double cy)
    {
        // 循環矢印 (時計回り)
        var gold = Palette.ArcaneGoldBrush;
        // 上の弧
        context.FillRectangle(gold, new Rect(cx - 8, cy - 12, 14, 3));
        // 右の弧
        context.FillRectangle(gold, new Rect(cx + 4, cy - 9, 3, 12));
        // 下の弧
        context.FillRectangle(gold, new Rect(cx - 6, cy, 12, 3));
        // 左の弧
        context.FillRectangle(gold, new Rect(cx - 8, cy - 12, 3, 12));
        // 矢じり (右下)
        context.FillRectangle(gold, new Rect(cx + 7, cy + 1, 3, 3));
        context.FillRectangle(gold, new Rect(cx + 9, cy + 3, 3, 3));
        context.FillRectangle(gold, new Rect(cx + 7, cy + 5, 3, 3));
    }

    // ===== 共通ヘルパー =====

    private static void DrawBar(DrawingContext context, double x, double y, double w, double h,
        int value, int max, IBrush fill, string label)
    {
        // 影
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(x + 1, y + 1, w, h));
        // 枠
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 1), new Rect(x, y, w, h));
        // フィル
        var ratio = (double)value / max;
        context.FillRectangle(fill, new Rect(x + 1, y + 1, (w - 2) * ratio, h - 2));
        // ラベル
        var ft = new FormattedText(label, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 11, Palette.ParchmentBrush);
        context.DrawText(ft, new Point(x + w + 8, y - 1));
    }

    private static void DrawIntent(DrawingContext context, double x, double y, int damage)
    {
        var gold = Palette.ArcaneGoldBrush;
        // 剣 (より丁寧なドット絵)
        context.FillRectangle(gold, new Rect(x + 7, y, 2, 12));
        context.FillRectangle(gold, new Rect(x + 6, y + 1, 4, 1));
        context.FillRectangle(gold, new Rect(x + 5, y + 11, 6, 1));
        context.FillRectangle(gold, new Rect(x + 3, y + 12, 10, 3));
        context.FillRectangle(gold, new Rect(x + 7, y + 15, 2, 4));
        context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(x + 7, y + 19, 2, 1));

        var ft = new FormattedText($"-{damage}", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 16, Palette.CrimsonBrush);
        context.DrawText(ft, new Point(x + 22, y + 2));
    }
}
