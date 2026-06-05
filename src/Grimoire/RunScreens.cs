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

// StS 風の分岐マップ画面。層状ノードグラフを描き、現在地から到達可能なノードを選んで上へ進む。
public class RunMapView : Control
{
    private readonly RunState _run;
    private RunMap Map => _run.Map;
    public Action<MapNode>? NodeSelected;

    public RunMapView(RunState run) { _run = run; }

    private const double TopY = 130;
    private const double LayerGap = 82;
    private const double ColGap = 150;
    private double CenterX => Bounds.Width / 2;

    private Point NodeCenter(MapNode n) =>
        new(CenterX + (n.Col - 1) * ColGap, TopY + (Map.BossLayer - n.Layer) * LayerGap);

    private Rect NodeRect(MapNode n)
    {
        var c = NodeCenter(n);
        double w = n.Type == MapNodeType.Boss ? 100 : 64;
        return new Rect(c.X - w / 2, c.Y - 22, w, 44);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);
        foreach (var node in Map.ReachableNext())
            if (NodeRect(node).Contains(pos)) { NodeSelected?.Invoke(node); break; }
        base.OnPointerPressed(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        ScreenUi.Background(context, new Rect(Bounds.Size));

        ScreenUi.Centered(context,
            $"HP {_run.PlayerHp}/{_run.PlayerHpMax}    デッキ {_run.Deck.Count} 枚    踏破 {_run.BattlesWon} 戦",
            CenterX, 64, 18, Palette.ParchmentBrush);
        ScreenUi.Centered(context, "光るノードを選んで進む（戦=戦闘 / 精鋭 / 休=休憩 / BOSS）",
            CenterX, 92, 13, Palette.ArcaneGoldDimBrush);

        // エッジ (ノードの裏に描く)
        for (int i = 0; i < Map.Layers.Count - 1; i++)
            foreach (var a in Map.Layers[i])
                foreach (var j in a.NextIdx)
                    context.DrawLine(new Pen(Palette.ArcaneGoldDimBrush, 1),
                        NodeCenter(a), NodeCenter(Map.Layers[i + 1][j]));

        var reachable = Map.ReachableNext();
        foreach (var layer in Map.Layers)
            foreach (var node in layer)
                DrawNode(context, node, reachable.Contains(node));
    }

    private void DrawNode(DrawingContext context, MapNode node, bool reachable)
    {
        var rect = NodeRect(node);
        (IBrush fill, string label) = node.Type switch
        {
            MapNodeType.Battle => (Palette.CrimsonDimBrush, "戦"),
            MapNodeType.Elite  => (Palette.ArcaneGoldDimBrush, "精鋭"),
            MapNodeType.Rest   => (Palette.ForestBrush, "休"),
            MapNodeType.Boss   => (Palette.CrimsonBrush, "BOSS"),
            _ => (Palette.MidnightBrush, "?"),
        };

        context.FillRectangle(Palette.MidnightDeepBrush, new Rect(rect.X + 2, rect.Y + 2, rect.Width, rect.Height));
        context.FillRectangle(node.Visited ? Palette.MidnightBrush : fill, rect);
        context.DrawRectangle(null,
            new Pen(reachable ? Palette.ArcaneGoldBrush : Palette.ArcaneGoldDimBrush, reachable ? 3 : 1), rect);

        if (Map.Current == node)   // 現在地マーカー
            context.DrawRectangle(null, new Pen(Palette.EtherealCyanBrush, 2), rect.Inflate(4));

        var brush = node.Visited ? Palette.ArcaneGoldDimBrush : Palette.ParchmentBrush;
        var size = node.Type == MapNodeType.Boss ? 16.0 : 14.0;
        var ft = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);
        context.DrawText(ft, new Point(rect.X + (rect.Width - ft.Width) / 2, rect.Y + (rect.Height - ft.Height) / 2));
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
