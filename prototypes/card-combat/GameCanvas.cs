using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace card_combat;

public enum GameState
{
    Idle,
    Operating,
    Resolving,
    Won
}

public class GameCanvas : Control
{
    private readonly DispatcherTimer _timer;

    private const double EnemyAreaTop = 0;
    private const double EnemyAreaBottom = 90;
    private const double StageTop = 100;
    private const double StageBottom = 460;
    private const double ParticleY = 300;
    private const double ParticleR = 12;

    private readonly Rect _cardRect = new(380, 470, 140, 110);

    private int _enemyHpMax = 30;
    private int _enemyHp = 30;

    private GameState _state = GameState.Idle;

    private double _particleX;
    private double _particleVelX;
    private int _operationTimeRemaining;
    private const int OperationTimeoutTicks = 150;
    private bool _operationSuccess;

    private int _resolveTicks;
    private const int ResolveTicksMax = 50;
    private int _damageDealt;

    private double _hintPulse = 0;

    public GameCanvas()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _hintPulse += 0.1;

        if (_state == GameState.Operating)
        {
            _particleX += _particleVelX;
            _operationTimeRemaining--;
            if (_particleX > Bounds.Width + 20 || _operationTimeRemaining <= 0)
            {
                _operationSuccess = false;
                StartResolve(0);
            }
        }
        else if (_state == GameState.Resolving)
        {
            _resolveTicks++;
            if (_resolveTicks >= ResolveTicksMax)
            {
                _state = _enemyHp <= 0 ? GameState.Won : GameState.Idle;
            }
        }

        InvalidateVisual();
    }

    private void StartCardPlay()
    {
        if (_state != GameState.Idle) return;
        _state = GameState.Operating;
        _particleX = -ParticleR;
        _particleVelX = 5.5;
        _operationTimeRemaining = OperationTimeoutTicks;
    }

    private void StartResolve(int damage)
    {
        _state = GameState.Resolving;
        _resolveTicks = 0;
        _damageDealt = damage;
        _enemyHp = Math.Max(0, _enemyHp - damage);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);

        if (_state == GameState.Idle)
        {
            if (_cardRect.Contains(pos))
            {
                StartCardPlay();
            }
        }
        else if (_state == GameState.Operating)
        {
            var dx = pos.X - _particleX;
            var dy = pos.Y - ParticleY;
            if (Math.Sqrt(dx * dx + dy * dy) < ParticleR + 10)
            {
                _operationSuccess = true;
                StartResolve(5);
            }
        }
        else if (_state == GameState.Won)
        {
            _enemyHp = _enemyHpMax;
            _state = GameState.Idle;
        }

        base.OnPointerPressed(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(20, 20, 28)), new Rect(Bounds.Size));

        DrawEnemyArea(context);
        DrawStageArea(context);
        DrawCardArea(context);
        DrawStateOverlay(context);
    }

    private void DrawEnemyArea(DrawingContext context)
    {
        var ft = new FormattedText("過負荷サーバ (Overload Server)", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 18, Brushes.LightCoral);
        context.DrawText(ft, new Point(20, 20));

        const double barX = 20, barY = 52, barW = 300, barH = 18;
        context.DrawRectangle(null, new Pen(Brushes.White, 1), new Rect(barX, barY, barW, barH));
        var hpRatio = (double)_enemyHp / _enemyHpMax;
        context.FillRectangle(Brushes.OrangeRed, new Rect(barX, barY, barW * hpRatio, barH));

        var hpFt = new FormattedText($"HP {_enemyHp}/{_enemyHpMax}", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 13, Brushes.White);
        context.DrawText(hpFt, new Point(barX + barW + 12, barY));
    }

    private void DrawStageArea(DrawingContext context)
    {
        var stageRect = new Rect(20, StageTop, Bounds.Width - 40, StageBottom - StageTop);
        context.DrawRectangle(null, new Pen(Brushes.DimGray, 1), stageRect);

        if (_state == GameState.Idle)
        {
            var hint = "↓ カードをクリックして攻撃";
            var alpha = (byte)(150 + Math.Sin(_hintPulse) * 50);
            var ft = new FormattedText(hint, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 20,
                new SolidColorBrush(Color.FromArgb(alpha, 200, 200, 200)));
            context.DrawText(ft, new Point((Bounds.Width - ft.Width) / 2, StageTop + 130));
        }
        else if (_state == GameState.Operating)
        {
            var msg = $"飛んできた粒をクリックせよ (残り {_operationTimeRemaining / 60.0:F1}s)";
            var ft = new FormattedText(msg, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 14, Brushes.Yellow);
            context.DrawText(ft, new Point((Bounds.Width - ft.Width) / 2, StageTop + 20));

            context.DrawEllipse(Brushes.White, null, new Point(_particleX, ParticleY), ParticleR, ParticleR);

            const double tbarW = 700;
            const double tbarY = StageTop + 50;
            var tbarX = (Bounds.Width - tbarW) / 2;
            var ratio = (double)_operationTimeRemaining / OperationTimeoutTicks;
            context.DrawRectangle(null, new Pen(Brushes.DimGray, 1), new Rect(tbarX, tbarY, tbarW, 8));
            context.FillRectangle(Brushes.Yellow, new Rect(tbarX, tbarY, tbarW * ratio, 8));
        }
        else if (_state == GameState.Resolving)
        {
            var msg = _operationSuccess
                ? $"成功! 障害源を {_damageDealt} 弱化"
                : "失敗… 不発";
            var color = _operationSuccess ? Brushes.LightGreen : Brushes.OrangeRed;
            var ft = new FormattedText(msg, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 24, color);
            context.DrawText(ft, new Point((Bounds.Width - ft.Width) / 2, StageTop + 130));
        }
    }

    private void DrawCardArea(DrawingContext context)
    {
        var cardBg = _state == GameState.Idle ? Brushes.SteelBlue : Brushes.DimGray;
        context.FillRectangle(cardBg, _cardRect);
        context.DrawRectangle(null, new Pen(Brushes.White, 1.5), _cardRect);

        var nameFt = new FormattedText("Probe Request", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 13, Brushes.White);
        context.DrawText(nameFt, new Point(_cardRect.X + (_cardRect.Width - nameFt.Width) / 2, _cardRect.Y + 8));

        var costFt = new FormattedText("1", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 16, Brushes.Yellow);
        context.DrawText(costFt, new Point(_cardRect.X + _cardRect.Width - 18, _cardRect.Y + 6));

        var descLines = new[] { "効果:", "障害源を", "5 弱化", "", "(粒クリック)" };
        for (int i = 0; i < descLines.Length; i++)
        {
            var ft = new FormattedText(descLines[i], CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 11, Brushes.WhiteSmoke);
            context.DrawText(ft, new Point(_cardRect.X + 8, _cardRect.Y + 32 + i * 14));
        }
    }

    private void DrawStateOverlay(DrawingContext context)
    {
        if (_state == GameState.Won)
        {
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                new Rect(Bounds.Size));
            var msg = "勝利!  クリックで再戦";
            var ft = new FormattedText(msg, CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 36, Brushes.Gold);
            context.DrawText(ft, new Point((Bounds.Width - ft.Width) / 2, (Bounds.Height - ft.Height) / 2));
        }
    }
}
