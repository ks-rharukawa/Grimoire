using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace circuit_breaker;

public enum BreakerState
{
    Closed,
    Open,
    HalfOpen
}

public class CircuitBreakerCanvas : Control
{
    private readonly DispatcherTimer _timer;

    private const double AppX = 150;
    private const double ApiX = 750;
    private const double LineY = 280;
    private const double NodeR = 30;
    private const double BreakerX = 450;
    private const double ParticleR = 7;

    private BreakerState _state = BreakerState.Closed;
    private bool _apiHealthy = true;
    private int _failureCount = 0;
    private const int FailureThreshold = 3;
    private int _openTicks = 0;
    private const int OpenDurationTicks = 300;

    private class Particle
    {
        public double X;
        public double VelX = 4.0;
        public bool Returning;
        public bool Blocked;
        public int BlockedTicks;
    }
    private readonly List<Particle> _particles = new();
    private int _spawnCounter = 0;
    private const int SpawnInterval = 35;

    public CircuitBreakerCanvas()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_state == BreakerState.Open)
        {
            _openTicks++;
            if (_openTicks >= OpenDurationTicks)
            {
                _state = BreakerState.HalfOpen;
                _openTicks = 0;
                _failureCount = 0;
            }
        }

        _spawnCounter++;
        if (_spawnCounter >= SpawnInterval)
        {
            _spawnCounter = 0;
            _particles.Add(new Particle { X = AppX + NodeR + ParticleR });
        }

        var toRemove = new List<Particle>();
        foreach (var p in _particles)
        {
            if (p.Blocked)
            {
                p.BlockedTicks++;
                if (p.BlockedTicks > 20)
                    toRemove.Add(p);
                continue;
            }

            if (p.Returning)
            {
                p.X -= Math.Abs(p.VelX);
                if (p.X < AppX)
                    toRemove.Add(p);
                continue;
            }

            p.X += p.VelX;

            if (_state == BreakerState.Open && p.X >= BreakerX - ParticleR)
            {
                p.Blocked = true;
                p.X = BreakerX - ParticleR;
                continue;
            }

            if (p.X >= ApiX - NodeR - ParticleR)
            {
                if (_apiHealthy)
                {
                    if (_state == BreakerState.HalfOpen)
                        _state = BreakerState.Closed;
                    toRemove.Add(p);
                }
                else
                {
                    p.Returning = true;
                    _failureCount++;

                    if (_state == BreakerState.HalfOpen)
                    {
                        _state = BreakerState.Open;
                        _openTicks = 0;
                    }
                    else if (_failureCount >= FailureThreshold && _state == BreakerState.Closed)
                    {
                        _state = BreakerState.Open;
                        _openTicks = 0;
                    }
                }
            }
        }

        foreach (var p in toRemove)
            _particles.Remove(p);

        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);
        var dx = pos.X - ApiX;
        var dy = pos.Y - LineY;
        if (Math.Sqrt(dx * dx + dy * dy) < NodeR + 15)
        {
            _apiHealthy = !_apiHealthy;
            InvalidateVisual();
        }
        base.OnPointerPressed(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));

        DrawLegend(context);
        DrawDiagram(context);
        DrawCaption(context);
        DrawHint(context);
    }

    private void DrawLegend(DrawingContext context)
    {
        var lines = new[]
        {
            "対応表 (Legend):",
            "  左 = 自分のアプリ",
            "  右 = 外部API (クリックで Healthy/Failing をトグル)",
            "  粒 = リクエスト (白=送信 / 橙=失敗で戻る / 黄=ブレーカーで遮断)",
            "  中央バー = サーキットブレーカー (緑=Closed / 赤=Open / 金=Half-Open)",
        };
        for (int i = 0; i < lines.Length; i++)
        {
            var ft = new FormattedText(lines[i], CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 13, Brushes.LightGray);
            context.DrawText(ft, new Point(20, 15 + i * 18));
        }
    }

    private void DrawDiagram(DrawingContext context)
    {
        var lineBrush = _state == BreakerState.Open ? Brushes.DarkRed : Brushes.DimGray;
        context.DrawLine(new Pen(lineBrush, 2),
            new Point(AppX + NodeR, LineY),
            new Point(ApiX - NodeR, LineY));

        context.DrawEllipse(Brushes.LimeGreen, null, new Point(AppX, LineY), NodeR, NodeR);
        DrawCenteredText(context, "App", AppX, LineY, Brushes.Black, 12);

        IBrush breakerColor = _state switch
        {
            BreakerState.Closed => Brushes.Green,
            BreakerState.Open => Brushes.Red,
            BreakerState.HalfOpen => Brushes.Goldenrod,
            _ => Brushes.Gray
        };
        context.FillRectangle(breakerColor, new Rect(BreakerX - 8, LineY - 28, 16, 56));
        DrawCenteredText(context, _state.ToString(), BreakerX, LineY + 45, Brushes.White, 13);

        var apiBrush = _apiHealthy ? Brushes.LightGray : Brushes.Crimson;
        context.DrawEllipse(apiBrush, null, new Point(ApiX, LineY), NodeR, NodeR);
        DrawCenteredText(context, "API", ApiX, LineY, Brushes.Black, 12);
        var healthLabel = _apiHealthy ? "Healthy" : "Failing";
        DrawCenteredText(context, healthLabel, ApiX, LineY + 45,
            _apiHealthy ? Brushes.LightGreen : Brushes.OrangeRed, 12);

        foreach (var p in _particles)
        {
            IBrush brush = p.Returning ? Brushes.OrangeRed :
                p.Blocked ? Brushes.Yellow : Brushes.White;
            context.DrawEllipse(brush, null, new Point(p.X, LineY), ParticleR, ParticleR);
        }
    }

    private void DrawCaption(DrawingContext context)
    {
        var msg = _state switch
        {
            BreakerState.Closed when _apiHealthy =>
                $"字幕: 正常通信中。失敗カウント {_failureCount}/{FailureThreshold}",
            BreakerState.Closed when !_apiHealthy =>
                $"字幕: API が応答せず、リクエストが戻ってきています。失敗カウント {_failureCount}/{FailureThreshold} で遮断",
            BreakerState.Open =>
                $"字幕: ブレーカー Open。遮断中、残り {(OpenDurationTicks - _openTicks) / 60.0:F1}秒で Half-Open へ",
            BreakerState.HalfOpen =>
                "字幕: Half-Open。次のリクエストで Closed か Open かを判定",
            _ => ""
        };
        var ft = new FormattedText(msg, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 15, Brushes.White);
        context.DrawText(ft, new Point(20, Bounds.Height - 60));
    }

    private void DrawHint(DrawingContext context)
    {
        var hint = "操作: 右の API 円をクリックで Healthy/Failing をトグル。3回連続失敗で Open、5秒後 Half-Open へ自動遷移。";
        var ft = new FormattedText(hint, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 12, Brushes.DimGray);
        context.DrawText(ft, new Point(20, Bounds.Height - 28));
    }

    private static void DrawCenteredText(DrawingContext context, string text, double cx, double cy, IBrush brush, double size)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, size, brush);
        context.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
    }
}
