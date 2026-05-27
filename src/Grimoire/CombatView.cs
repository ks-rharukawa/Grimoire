using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
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

    private readonly int _playerHp = 28;
    private readonly int _playerHpMax = 30;
    private readonly int _energy = 3;
    private readonly int _energyMax = 3;
    private readonly int _enemyHp = 24;
    private readonly int _enemyHpMax = 30;
    private readonly int _enemyIntent = 5;

    private enum CardIcon { ProbeRadar, ShieldFilter, EchoLoop, LookupGlass, RetryArrow }
    private readonly (string Name, int Cost, string Effect, CardIcon Icon)[] _hand =
    {
        ("Probe Request", 1, "障害源を\n5 弱化",      CardIcon.ProbeRadar),
        ("Packet Filter", 1, "ブロック\n+5 生成",      CardIcon.ShieldFilter),
        ("Echo Reply",    0, "前回効果\nもう一度",     CardIcon.EchoLoop),
        ("Lookup",        1, "カード\n+1 ドロー",      CardIcon.LookupGlass),
        ("Retry",         1, "失敗カード\n再使用可",   CardIcon.RetryArrow),
    };

    private readonly List<(double X, double Y, byte Alpha, int Size)> _stars = new();

    private double _packetProgress;
    private double _pulse;

    public CombatView()
    {
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
        DrawPageFiligree(context);
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

        var nameFt = new FormattedText("障害サーバ / Overload Server", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 14, Palette.CrimsonBrush);
        context.DrawText(nameFt, new Point(areaLeft + 20, EnemyAreaTop + 8));

        DrawBar(context, areaLeft + 20, EnemyAreaTop + 36, 240, 14, _enemyHp, _enemyHpMax,
            Palette.CrimsonBrush, $"HP {_enemyHp}/{_enemyHpMax}");

        DrawIntent(context, areaLeft + 20, EnemyAreaTop + 64, _enemyIntent);
    }

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
        const double areaLeft = BookFrameMargin + 20;
        var areaWidth = Bounds.Width - (BookFrameMargin + 20) * 2;
        var stageRect = new Rect(areaLeft, StageTop, areaWidth, StageHeight);

        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1), stageRect);

        // (Designer #1) 魔法陣・同心円・四隅の十字
        DrawMagicCircleBackground(context, stageRect);

        var lineY = StageTop + (StageHeight - CaptionStripHeight) / 2;
        var clientX = stageRect.X + 80;
        var serverX = stageRect.Right - 80;

        // 接続線 (ピクセル規律: Pen 太さは 1-2 のみ。3 段の手動水平スラブで halo)
        DrawPixelGlowLine(context, clientX + 20, serverX - 20, lineY);

        DrawNetworkNode(context, clientX, lineY, "Client", Palette.LimeGreenBrush, healthy: true);
        DrawNetworkNode(context, serverX, lineY, "Server", Palette.CrimsonBrush, healthy: false);

        // (Designer #4) パケット trail
        DrawPacketWithTrail(context, clientX + 20, serverX - 20, lineY);

        DrawCaptionStrip(context, stageRect, "> ブレーカー Closed | 失敗 1/3");
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

    private static void DrawEndTurnButton(DrawingContext context, Rect rect)
    {
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(rect.X + 2, rect.Y + 2, rect.Width, rect.Height));
        context.FillRectangle(Palette.ParchmentAgedBrush, rect);
        context.FillRectangle(Palette.ParchmentBrush, new Rect(rect.X + 2, rect.Y + 2, rect.Width - 4, 3));
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 2), rect);
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

        context.FillRectangle(Palette.MidnightBrush, new Rect(x, y, CardWidth, CardHeight / 2));
        context.FillRectangle(Palette.ParchmentBrush, new Rect(x, y + CardHeight / 2, CardWidth, CardHeight / 2));
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(x, y + CardHeight / 2, CardWidth, 2));
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 3), cardRect);
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1),
            new Rect(x + 4, y + 4, CardWidth - 8, CardHeight - 8));

        var nameTypeface = new Typeface(new FontFamily("Hiragino Sans, Yu Gothic, Noto Sans CJK JP, sans-serif"), FontStyle.Normal, FontWeight.Bold);
        var nameFt = new FormattedText(name, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, nameTypeface, 14, Palette.ParchmentBrush);
        context.DrawText(nameFt, new Point(x + (CardWidth - nameFt.Width) / 2, y + 12));

        DrawCostGem(context, x + CardWidth - 30, y + 10, cost);

        var iconRect = new Rect(x + (CardWidth - 80) / 2, y + 38, 80, 64);
        context.FillRectangle(Palette.MidnightDeepBrush, iconRect);
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1), iconRect);
        DrawCardIcon(context, iconRect, icon);

        var lines = effect.Split('\n');
        var effectTypeface = new Typeface(new FontFamily("Hiragino Sans, Yu Gothic, Noto Sans CJK JP, sans-serif"), FontStyle.Normal, FontWeight.Bold);
        var inkBrush = new SolidColorBrush(Color.FromRgb(26, 26, 26));
        for (int i = 0; i < lines.Length; i++)
        {
            var ft = new FormattedText(lines[i], CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, effectTypeface, 14, inkBrush);
            context.DrawText(ft, new Point(x + (CardWidth - ft.Width) / 2, y + CardHeight / 2 + 16 + i * 20));
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

    private static void DrawIntent(DrawingContext context, double x, double y, int damage)
    {
        var gold = Palette.ArcaneGoldBrush;
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
