using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Grimoire;

// 画面共通の小物 (中央テキスト・ボタン)。style-guide のパレットに合わせる。
internal static class ScreenUi
{
    public static void Background(DrawingContext ctx, Rect bounds)
    {
        ctx.FillRectangle(Palette.MidnightDeepBrush, bounds);
        var pages = new Rect(80, 0, bounds.Width - 160, bounds.Height);
        ctx.FillRectangle(Palette.MidnightBrush, pages);
    }

    public static void Centered(DrawingContext ctx, string text, double cx, double y, double size, IBrush brush)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, size, brush);
        ctx.DrawText(ft, new Point(cx - ft.Width / 2, y));
    }

    public static void Button(DrawingContext ctx, Rect r, string label)
    {
        ctx.FillRectangle(Palette.MidnightDeepBrush, new Rect(r.X + 3, r.Y + 3, r.Width, r.Height));
        ctx.FillRectangle(Palette.ParchmentAgedBrush, r);
        ctx.FillRectangle(Palette.ParchmentBrush, new Rect(r.X + 2, r.Y + 2, r.Width - 4, 3));
        ctx.DrawRectangle(null, new Pen(Palette.ArcaneGoldBrush, 2), r);
        var ft = new FormattedText(label, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 17, Palette.MidnightBrush);
        ctx.DrawText(ft, new Point(r.X + (r.Width - ft.Width) / 2, r.Y + (r.Height - ft.Height) / 2));
    }
}

// ラン進行画面 (#7 は仮。#8 で StS 風の分岐マップに置き換える)。
public class RunMapView : Control
{
    private readonly RunState _run;
    public Action? Proceed;

    public RunMapView(RunState run) { _run = run; }

    private Rect ProceedRect => new((Bounds.Width - 240) / 2, Bounds.Height * 0.60, 240, 50);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (ProceedRect.Contains(e.GetPosition(this))) Proceed?.Invoke();
        base.OnPointerPressed(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        ScreenUi.Background(context, new Rect(Bounds.Size));
        var cx = Bounds.Width / 2;

        ScreenUi.Centered(context, "ラン進行", cx, 130, 34, Palette.ArcaneGoldBrush);
        ScreenUi.Centered(context,
            $"踏破 {_run.BattlesWon} 戦    HP {_run.PlayerHp}/{_run.PlayerHpMax}    デッキ {_run.Deck.Count} 枚",
            cx, 200, 18, Palette.ParchmentBrush);
        ScreenUi.Centered(context, "※ #8 で分岐マップ (戦闘/精鋭/休憩/ボス) に置き換え予定",
            cx, 240, 13, Palette.ArcaneGoldDimBrush);

        ScreenUi.Button(context, ProceedRect, "次の戦闘へ進む");
    }
}

// ラン結果画面 (勝利 / 敗北 → 新しいラン)。
public class RunOverView : Control
{
    private readonly RunState _run;
    private readonly bool _victory;
    public Action? Restart;

    public RunOverView(RunState run, bool victory) { _run = run; _victory = victory; }

    private Rect RestartRect => new((Bounds.Width - 240) / 2, Bounds.Height * 0.62, 240, 50);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (RestartRect.Contains(e.GetPosition(this))) Restart?.Invoke();
        base.OnPointerPressed(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        ScreenUi.Background(context, new Rect(Bounds.Size));
        var cx = Bounds.Width / 2;

        var title = _victory ? "ラン制覇 — ネットワーク安定を回復!" : "ラン失敗 — 障害に呑まれた…";
        var brush = _victory ? Palette.ArcaneGoldBrush : Palette.CrimsonBrush;
        ScreenUi.Centered(context, title, cx, 150, 30, brush);
        ScreenUi.Centered(context, $"踏破 {_run.BattlesWon} 戦", cx, 220, 18, Palette.ParchmentBrush);

        ScreenUi.Button(context, RestartRect, "新しいランを始める");
    }
}
