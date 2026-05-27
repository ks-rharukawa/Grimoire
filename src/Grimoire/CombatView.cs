using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Grimoire;

// docs/style-guide.md §5「レイアウト（戦闘画面ベース）」を実装する CombatView。
// 第2イテレーション: 4 セクション (Enemy / Stage / Player Strip / Hand) を静的に実装。
// 粒のアニメーション・インタラクション・ターン構造は後続イテレーションで追加。
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

    // ゲーム状態 (静的サンプル値、後で GameState に切り出す)
    private readonly int _playerHp = 28;
    private readonly int _playerHpMax = 30;
    private readonly int _energy = 3;
    private readonly int _energyMax = 3;
    private readonly int _enemyHp = 24;
    private readonly int _enemyHpMax = 30;
    private readonly int _enemyIntent = 5;

    // 手札のサンプルデータ (docs/classes.md の決定通り)
    private readonly (string Name, int Cost, string Effect)[] _hand =
    {
        ("Probe Request", 1, "障害源を\n5 弱化"),
        ("Packet Filter", 1, "ブロック\n+5 生成"),
        ("Echo Reply",    0, "前回効果\nもう一度"),
        ("Lookup",        1, "カード\n+1 ドロー"),
        ("Retry",         1, "失敗したカード\n再使用可"),
    };

    public CombatView()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => InvalidateVisual();
        _timer.Start();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        DrawPagesBackground(context);
        DrawBookFrame(context);
        DrawEnemyArea(context);
        DrawStage(context);
        DrawPlayerStrip(context);
        DrawHand(context);
    }

    // ===== 背景・装飾 =====

    private void DrawPagesBackground(DrawingContext context)
    {
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(Bounds.Size));
        var pages = new Rect(BookFrameMargin, 0, Bounds.Width - BookFrameMargin * 2, Bounds.Height);
        context.FillRectangle(Palette.MidnightBrush, pages);
    }

    private void DrawBookFrame(DrawingContext context)
    {
        context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(BookFrameMargin - 2, 0, 2, Bounds.Height));
        context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(Bounds.Width - BookFrameMargin, 0, 2, Bounds.Height));

        const double cornerLen = 30;
        const double cornerThick = 4;
        DrawCornerL(context, 20, 20, cornerLen, cornerThick, true, false, false, false);
        DrawCornerL(context, Bounds.Width - 20 - cornerLen, 20, cornerLen, cornerThick, false, true, false, false);
        DrawCornerL(context, 20, Bounds.Height - 20 - cornerLen, cornerLen, cornerThick, false, false, true, false);
        DrawCornerL(context, Bounds.Width - 20 - cornerLen, Bounds.Height - 20 - cornerLen, cornerLen, cornerThick, false, false, false, true);
    }

    private static void DrawCornerL(DrawingContext context, double x, double y,
        double len, double thick, bool tl, bool tr, bool bl, bool br)
    {
        var b = Palette.ArcaneGoldBrush;
        if (tl)
        {
            context.FillRectangle(b, new Rect(x, y, len, thick));
            context.FillRectangle(b, new Rect(x, y, thick, len));
        }
        else if (tr)
        {
            context.FillRectangle(b, new Rect(x, y, len, thick));
            context.FillRectangle(b, new Rect(x + len - thick, y, thick, len));
        }
        else if (bl)
        {
            context.FillRectangle(b, new Rect(x, y + len - thick, len, thick));
            context.FillRectangle(b, new Rect(x, y, thick, len));
        }
        else if (br)
        {
            context.FillRectangle(b, new Rect(x, y + len - thick, len, thick));
            context.FillRectangle(b, new Rect(x + len - thick, y, thick, len));
        }
    }

    // ===== Section A: Enemy Area =====

    private void DrawEnemyArea(DrawingContext context)
    {
        const double areaLeft = BookFrameMargin + 20;
        var centerX = Bounds.Width / 2;

        // Pixel art サーバラック (sprite)
        DrawServerRack(context, centerX - 40, EnemyAreaTop + 10, 80, 100);

        // 敵名
        var nameFt = new FormattedText("障害サーバ / Overload Server", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 14, Palette.CrimsonBrush);
        context.DrawText(nameFt, new Point(areaLeft + 20, EnemyAreaTop + 8));

        // HP バー
        DrawBar(context, areaLeft + 20, EnemyAreaTop + 36, 240, 14, _enemyHp, _enemyHpMax,
            Palette.CrimsonBrush, $"HP {_enemyHp}/{_enemyHpMax}");

        // Intent
        DrawIntent(context, areaLeft + 20, EnemyAreaTop + 64, _enemyIntent);
    }

    private static void DrawServerRack(DrawingContext context, double x, double y, double w, double h)
    {
        // 背景
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(x, y, w, h));

        // 赤い外枠
        const double frame = 2;
        context.FillRectangle(Palette.CrimsonBrush, new Rect(x, y, w, frame));
        context.FillRectangle(Palette.CrimsonBrush, new Rect(x, y + h - frame, w, frame));
        context.FillRectangle(Palette.CrimsonBrush, new Rect(x, y, frame, h));
        context.FillRectangle(Palette.CrimsonBrush, new Rect(x + w - frame, y, frame, h));

        // 内部のラックスロット (5段)
        for (int i = 0; i < 5; i++)
        {
            var rowY = y + 8 + i * 18;
            context.FillRectangle(Palette.CrimsonDimBrush, new Rect(x + 6, rowY, w - 12, 12));
            // 各スロットの LED
            context.FillRectangle(Palette.CrimsonBrush, new Rect(x + w - 12, rowY + 4, 4, 4));
        }

        // 上から飛び散る火花 (静的)
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(x + w / 2 - 1, y - 6, 2, 4));
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(x + w / 2 - 5, y - 4, 2, 2));
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(x + w / 2 + 3, y - 4, 2, 2));
    }

    // ===== Section B: Stage (§5 動く図) =====

    private void DrawStage(DrawingContext context)
    {
        const double areaLeft = BookFrameMargin + 20;
        var areaWidth = Bounds.Width - (BookFrameMargin + 20) * 2;
        var stageRect = new Rect(areaLeft, StageTop, areaWidth, StageHeight);

        // ステージ枠
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1), stageRect);

        // ネットワークトポロジー (静的、後で動的に)
        var lineY = StageTop + (StageHeight - CaptionStripHeight) / 2;
        var clientX = stageRect.X + 80;
        var serverX = stageRect.Right - 80;

        // 接続線
        context.DrawLine(new Pen(Palette.ArcaneGoldBrush, 2),
            new Point(clientX + 20, lineY),
            new Point(serverX - 20, lineY));

        // クライアントノード (LimeGreen)
        DrawNetworkNode(context, clientX, lineY, "Client", Palette.LimeGreenBrush);

        // サーバノード (Crimson、障害中)
        DrawNetworkNode(context, serverX, lineY, "Server", Palette.CrimsonBrush);

        // パケット (静的に1個、線の中央に)
        var packetX = (clientX + serverX) / 2;
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(packetX - 3, lineY - 3, 6, 6));

        // 字幕ストリップ (Stage 下部内側)
        DrawCaptionStrip(context, stageRect, "> ブレーカー Closed | 失敗 1/3");
    }

    private static void DrawNetworkNode(DrawingContext context, double cx, double cy, string label, IBrush color)
    {
        const double size = 32;
        var rect = new Rect(cx - size / 2, cy - size / 2, size, size);
        // 外側 (ピクセル円風: 角を落とした矩形)
        context.FillRectangle(color, rect);
        // 内側 (ハイライト)
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(rect.X + 4, rect.Y + 4, size - 8, size - 8));
        context.FillRectangle(color, new Rect(rect.X + 8, rect.Y + 8, size - 16, size - 16));

        // ラベル
        var ft = new FormattedText(label, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 10, Palette.ParchmentBrush);
        context.DrawText(ft, new Point(cx - ft.Width / 2, cy + size / 2 + 4));
    }

    private void DrawCaptionStrip(DrawingContext context, Rect stage, string text)
    {
        var stripRect = new Rect(stage.X + 2, stage.Bottom - CaptionStripHeight - 2,
            stage.Width - 4, CaptionStripHeight);
        // パーチメント風背景
        context.FillRectangle(Palette.ParchmentBrush, stripRect);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(140, 10, 14, 46)), stripRect);
        // 字幕
        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 12, Palette.MidnightBrush);
        context.DrawText(ft, new Point(stripRect.X + 12, stripRect.Y + 8));
    }

    // ===== Section C: Player Strip =====

    private void DrawPlayerStrip(DrawingContext context)
    {
        const double areaLeft = BookFrameMargin + 20;
        var areaWidth = Bounds.Width - (BookFrameMargin + 20) * 2;

        var stripRect = new Rect(areaLeft, PlayerStripTop, areaWidth, PlayerStripHeight);
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1), stripRect);

        // Player HP バー
        DrawBar(context, areaLeft + 20, PlayerStripTop + 18, 220, 14, _playerHp, _playerHpMax,
            Palette.LimeGreenBrush, $"HP {_playerHp}/{_playerHpMax}");

        // Energy ジェム
        DrawEnergyGems(context, areaLeft + 360, PlayerStripTop + 14, _energy, _energyMax);

        // End Turn ボタン
        var btnRect = new Rect(stripRect.Right - 140, PlayerStripTop + 8, 124, 34);
        DrawEndTurnButton(context, btnRect);
    }

    private static void DrawEnergyGems(DrawingContext context, double x, double y, int current, int max)
    {
        // "Energy" ラベル
        var labelFt = new FormattedText("Energy", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 11, Palette.ParchmentBrush);
        context.DrawText(labelFt, new Point(x, y));

        // ジェム (gold diamond)
        const double gemSize = 14;
        const double gap = 6;
        var gemsX = x + 56;
        for (int i = 0; i < max; i++)
        {
            var gx = gemsX + i * (gemSize + gap);
            var brush = i < current ? Palette.ArcaneGoldBrush : Palette.ArcaneGoldDimBrush;
            // ダイヤモンド (傾けた矩形をピクセル風で代用): 中心に4方向ピクセル
            context.FillRectangle(brush, new Rect(gx + 4, y, 6, 4));
            context.FillRectangle(brush, new Rect(gx + 2, y + 4, 10, 6));
            context.FillRectangle(brush, new Rect(gx + 4, y + 10, 6, 4));
        }

        var countFt = new FormattedText($"{current}/{max}", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 11, Palette.ParchmentBrush);
        context.DrawText(countFt, new Point(gemsX + max * (gemSize + gap) + 8, y));
    }

    private static void DrawEndTurnButton(DrawingContext context, Rect rect)
    {
        // パーチメント背景
        context.FillRectangle(Palette.ParchmentAgedBrush, rect);
        // ゴールドの内枠
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 2), rect);
        // テキスト
        var ft = new FormattedText("End Turn", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 15, Palette.MidnightBrush);
        context.DrawText(ft, new Point(rect.X + (rect.Width - ft.Width) / 2, rect.Y + (rect.Height - ft.Height) / 2));
    }

    // ===== Section D: Hand =====

    private void DrawHand(DrawingContext context)
    {
        const double areaLeft = BookFrameMargin + 20;
        var areaWidth = Bounds.Width - (BookFrameMargin + 20) * 2;

        // カードを横並び中央配置 (fan layout は次イテレーション)
        const double cardGap = 12;
        var totalWidth = _hand.Length * CardWidth + (_hand.Length - 1) * cardGap;
        var startX = areaLeft + (areaWidth - totalWidth) / 2;
        var cardY = HandTop + (HandHeight - CardHeight) / 2;

        for (int i = 0; i < _hand.Length; i++)
        {
            var x = startX + i * (CardWidth + cardGap);
            DrawCard(context, x, cardY, _hand[i].Name, _hand[i].Cost, _hand[i].Effect);
        }
    }

    private static void DrawCard(DrawingContext context, double x, double y, string name, int cost, string effect)
    {
        var cardRect = new Rect(x, y, CardWidth, CardHeight);

        // 上半分: Midnight (アート/icon 領域)
        context.FillRectangle(Palette.MidnightBrush, new Rect(x, y, CardWidth, CardHeight / 2));
        // 下半分: Parchment (説明領域)
        context.FillRectangle(Palette.ParchmentBrush, new Rect(x, y + CardHeight / 2, CardWidth, CardHeight / 2));

        // 枠 (ArcaneGold, 3px)
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 3), cardRect);

        // カード名 (上部中央)
        var nameFt = new FormattedText(name, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 12, Palette.ParchmentBrush);
        context.DrawText(nameFt, new Point(x + (CardWidth - nameFt.Width) / 2, y + 10));

        // コスト (右上、ゴールドのダイヤ)
        var costX = x + CardWidth - 22;
        const double costY = 8;
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(costX + 4, y + costY, 8, 4));
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(costX, y + costY + 4, 16, 8));
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(costX + 4, y + costY + 12, 8, 4));
        var costFt = new FormattedText(cost.ToString(), CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 12, Palette.MidnightBrush);
        context.DrawText(costFt, new Point(costX + (16 - costFt.Width) / 2, y + costY + 2));

        // アイコン枠 (中央のアート領域、プレースホルダ)
        var iconRect = new Rect(x + (CardWidth - 64) / 2, y + 36, 64, 50);
        context.FillRectangle(Palette.MidnightDeepBrush, iconRect);
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1), iconRect);
        // 中央に小さな粒 (placeholder icon)
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(iconRect.X + 28, iconRect.Y + 21, 8, 8));

        // 効果説明 (下半分中央)
        var lines = effect.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var ft = new FormattedText(lines[i], CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 11, Palette.MidnightBrush);
            context.DrawText(ft, new Point(x + (CardWidth - ft.Width) / 2, y + CardHeight / 2 + 12 + i * 16));
        }
    }

    // ===== 共通ヘルパー =====

    private static void DrawBar(DrawingContext context, double x, double y, double w, double h,
        int value, int max, IBrush fill, string label)
    {
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 1), new Rect(x, y, w, h));
        var ratio = (double)value / max;
        context.FillRectangle(fill, new Rect(x + 1, y + 1, (w - 2) * ratio, h - 2));
        var ft = new FormattedText(label, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 11, Palette.ParchmentBrush);
        context.DrawText(ft, new Point(x + w + 8, y - 1));
    }

    private static void DrawIntent(DrawingContext context, double x, double y, int damage)
    {
        // 剣アイコン (pixel art)
        // 刃
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(x + 6, y, 4, 14));
        // 柄
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(x + 2, y + 12, 12, 3));
        // 持ち手
        context.FillRectangle(Palette.ArcaneGoldBrush, new Rect(x + 6, y + 15, 4, 5));
        // ダメージ
        var ft = new FormattedText($"-{damage}", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 16, Palette.CrimsonBrush);
        context.DrawText(ft, new Point(x + 22, y + 2));
    }
}
