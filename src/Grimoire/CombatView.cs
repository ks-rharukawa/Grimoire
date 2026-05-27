using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Grimoire;

// docs/style-guide.md §5「レイアウト（戦闘画面ベース）」を実装する CombatView。
// 第1イテレーション: 背景・ブックフレーム・各エリアのプレースホルダ枠を配置。
// 以降のイテレーションで各エリアの中身を埋めていく。
public class CombatView : Control
{
    private readonly DispatcherTimer _timer;

    // 参照寸法 (style-guide §5, 1280x720 基準)
    private const double BookFrameMargin = 80;
    private const double EnemyAreaTop = 40;
    private const double EnemyAreaHeight = 160;
    private const double StageTop = 220;
    private const double StageHeight = 220;
    private const double PlayerStripTop = 460;
    private const double PlayerStripHeight = 50;
    private const double HandTop = 530;
    private const double HandHeight = 170;

    public CombatView()
    {
        // 60fps tick for future animations (現状は静止描画のみだが基盤として始動)
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => InvalidateVisual();
        _timer.Start();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        DrawPagesBackground(context);
        DrawBookFrame(context);
        DrawEnemyAreaPlaceholder(context);
        DrawStagePlaceholder(context);
        DrawPlayerStripPlaceholder(context);
        DrawHandPlaceholder(context);
    }

    private void DrawPagesBackground(DrawingContext context)
    {
        // 全体: MidnightDeep (book の外側)
        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(Bounds.Size));
        // ページ領域: Midnight
        var pages = new Rect(BookFrameMargin, 0, Bounds.Width - BookFrameMargin * 2, Bounds.Height);
        context.FillRectangle(Palette.MidnightBrush, pages);
    }

    private void DrawBookFrame(DrawingContext context)
    {
        // 左右のページ端 (gold dim 縦線)
        context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(BookFrameMargin - 2, 0, 2, Bounds.Height));
        context.FillRectangle(Palette.ArcaneGoldDimBrush, new Rect(Bounds.Width - BookFrameMargin, 0, 2, Bounds.Height));

        // 4 つのフィリグリーコーナー (プレースホルダ: L字)
        const double cornerLen = 30;
        const double cornerThick = 4;
        var gold = Palette.ArcaneGoldBrush;

        DrawCorner(context, gold, 20, 20, cornerLen, cornerThick, topLeft: true);
        DrawCorner(context, gold, Bounds.Width - 20 - cornerLen, 20, cornerLen, cornerThick, topLeft: false, topRight: true);
        DrawCorner(context, gold, 20, Bounds.Height - 20 - cornerLen, cornerLen, cornerThick, topLeft: false, bottomLeft: true);
        DrawCorner(context, gold, Bounds.Width - 20 - cornerLen, Bounds.Height - 20 - cornerLen, cornerLen, cornerThick, topLeft: false, bottomRight: true);
    }

    private static void DrawCorner(DrawingContext context, IBrush brush, double x, double y,
        double len, double thick, bool topLeft = false, bool topRight = false, bool bottomLeft = false, bool bottomRight = false)
    {
        // L字パターン: コーナー位置に応じて 2 本の rect を描く
        if (topLeft)
        {
            context.FillRectangle(brush, new Rect(x, y, len, thick));
            context.FillRectangle(brush, new Rect(x, y, thick, len));
        }
        else if (topRight)
        {
            context.FillRectangle(brush, new Rect(x, y, len, thick));
            context.FillRectangle(brush, new Rect(x + len - thick, y, thick, len));
        }
        else if (bottomLeft)
        {
            context.FillRectangle(brush, new Rect(x, y + len - thick, len, thick));
            context.FillRectangle(brush, new Rect(x, y, thick, len));
        }
        else if (bottomRight)
        {
            context.FillRectangle(brush, new Rect(x, y + len - thick, len, thick));
            context.FillRectangle(brush, new Rect(x + len - thick, y, thick, len));
        }
    }

    private void DrawEnemyAreaPlaceholder(DrawingContext context)
    {
        var rect = new Rect(BookFrameMargin + 20, EnemyAreaTop, Bounds.Width - (BookFrameMargin + 20) * 2, EnemyAreaHeight);
        DrawPlaceholder(context, rect, "ENEMY AREA (Section A)");
    }

    private void DrawStagePlaceholder(DrawingContext context)
    {
        var rect = new Rect(BookFrameMargin + 20, StageTop, Bounds.Width - (BookFrameMargin + 20) * 2, StageHeight);
        DrawPlaceholder(context, rect, "STAGE / §5 動く図 (Section B)");
    }

    private void DrawPlayerStripPlaceholder(DrawingContext context)
    {
        var rect = new Rect(BookFrameMargin + 20, PlayerStripTop, Bounds.Width - (BookFrameMargin + 20) * 2, PlayerStripHeight);
        DrawPlaceholder(context, rect, "PLAYER STRIP (Section C)");
    }

    private void DrawHandPlaceholder(DrawingContext context)
    {
        var rect = new Rect(BookFrameMargin + 20, HandTop, Bounds.Width - (BookFrameMargin + 20) * 2, HandHeight);
        DrawPlaceholder(context, rect, "HAND / 5 cards fan (Section D)");
    }

    private static void DrawPlaceholder(DrawingContext context, Rect rect, string label)
    {
        context.DrawRectangle(null, new Pen(Palette.ArcaneGoldDimBrush, 1), rect);
        var ft = new FormattedText(label, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 13, Palette.ArcaneGoldDimBrush);
        context.DrawText(ft, new Point(rect.X + 8, rect.Y + 6));
    }
}
