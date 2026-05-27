using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace particle;

public class ParticleCanvas : Control
{
    private double _x = 0;
    private const double Y = 200;
    private const double Speed = 3.0;
    private const double Radius = 6;
    private bool _running = true;
    private readonly DispatcherTimer _timer;

    public ParticleCanvas()
    {
        Focusable = true;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_running) return;
        _x += Speed;
        if (_x > Bounds.Width + Radius) _x = -Radius;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _running = !_running;
        InvalidateVisual();
        base.OnPointerPressed(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));

        context.DrawLine(
            new Pen(Brushes.DimGray, 1),
            new Point(0, Y),
            new Point(Bounds.Width, Y));

        context.DrawEllipse(Brushes.White, null, new Point(_x, Y), Radius, Radius);

        var status = new FormattedText(
            _running ? "Running — click to pause" : "Paused — click to resume",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            14,
            Brushes.White);
        context.DrawText(status, new Point(12, Bounds.Height - 28));
    }
}
