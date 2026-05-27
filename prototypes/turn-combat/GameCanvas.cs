using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace turn_combat;

public enum GameState
{
    Idle,
    Operating,
    Resolving,
    EnemyTurn,
    Won,
    Defeated
}

public class GameCanvas : Control
{
    private readonly DispatcherTimer _timer;

    private const double StageTop = 100;
    private const double StageBottom = 440;
    private const double ParticleY = 280;
    private const double ParticleR = 12;

    private readonly Rect _cardRect = new(260, 470, 140, 130);
    private readonly Rect _endTurnRect = new(700, 500, 150, 70);

    // Player
    private const int PlayerHpMax = 30;
    private int _playerHp = PlayerHpMax;
    private const int EnergyMax = 3;
    private int _energy = EnergyMax;

    // Enemy
    private const int EnemyHpMax = 30;
    private int _enemyHp = EnemyHpMax;
    private const int EnemyIntent = 5;

    // State
    private GameState _state = GameState.Idle;

    // Operating
    private double _particleX;
    private double _particleVelX;
    private int _operationTimeRemaining;
    private const int OperationTimeoutTicks = 150;
    private bool _operationSuccess;

    // Resolving
    private int _resolveTicks;
    private const int ResolveTicksMax = 50;
    private int _damageDealt;

    // EnemyTurn
    private int _enemyTurnTicks;
    private const int EnemyTurnTicksMax = 70;
    private int _damageReceived;
    private bool _enemyTurnApplied;

    // Idle hint pulse
    private double _hintPulse;

    public GameCanvas()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _hintPulse += 0.1;

        switch (_state)
        {
            case GameState.Operating:
                _particleX += _particleVelX;
                _operationTimeRemaining--;
                if (_particleX > Bounds.Width + 20 || _operationTimeRemaining <= 0)
                {
                    _operationSuccess = false;
                    StartResolve(0);
                }
                break;

            case GameState.Resolving:
                _resolveTicks++;
                if (_resolveTicks >= ResolveTicksMax)
                {
                    _state = _enemyHp <= 0 ? GameState.Won : GameState.Idle;
                }
                break;

            case GameState.EnemyTurn:
                _enemyTurnTicks++;
                if (_enemyTurnTicks >= EnemyTurnTicksMax / 2 && !_enemyTurnApplied)
                {
                    _playerHp = Math.Max(0, _playerHp - EnemyIntent);
                    _damageReceived = EnemyIntent;
                    _enemyTurnApplied = true;
                }
                if (_enemyTurnTicks >= EnemyTurnTicksMax)
                {
                    if (_playerHp <= 0)
                        _state = GameState.Defeated;
                    else
                    {
                        _energy = EnergyMax;
                        _state = GameState.Idle;
                    }
                }
                break;
        }

        InvalidateVisual();
    }

    private void StartCardPlay()
    {
        if (_state != GameState.Idle) return;
        if (_energy <= 0) return;
        _energy--;
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

    private void StartEnemyTurn()
    {
        if (_state != GameState.Idle) return;
        _state = GameState.EnemyTurn;
        _enemyTurnTicks = 0;
        _enemyTurnApplied = false;
    }

    private void Restart()
    {
        _playerHp = PlayerHpMax;
        _enemyHp = EnemyHpMax;
        _energy = EnergyMax;
        _state = GameState.Idle;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(this);

        switch (_state)
        {
            case GameState.Idle:
                if (_cardRect.Contains(pos) && _energy > 0)
                    StartCardPlay();
                else if (_endTurnRect.Contains(pos))
                    StartEnemyTurn();
                break;

            case GameState.Operating:
                var dx = pos.X - _particleX;
                var dy = pos.Y - ParticleY;
                if (Math.Sqrt(dx * dx + dy * dy) < ParticleR + 10)
                {
                    _operationSuccess = true;
                    StartResolve(5);
                }
                break;

            case GameState.Won:
            case GameState.Defeated:
                Restart();
                break;
        }

        base.OnPointerPressed(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(20, 20, 28)), new Rect(Bounds.Size));

        DrawEnemyArea(context);
        DrawStageArea(context);
        DrawPlayerArea(context);
        DrawCard(context);
        DrawEndTurnButton(context);
        DrawStateOverlay(context);
    }

    private void DrawEnemyArea(DrawingContext context)
    {
        var nameFt = new FormattedText("過負荷サーバ (Overload Server)", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 18, Brushes.LightCoral);
        context.DrawText(nameFt, new Point(20, 18));

        const double barX = 20, barY = 50, barW = 280, barH = 18;
        context.DrawRectangle(null, new Pen(Brushes.White, 1), new Rect(barX, barY, barW, barH));
        var hpRatio = (double)_enemyHp / EnemyHpMax;
        context.FillRectangle(Brushes.OrangeRed, new Rect(barX, barY, barW * hpRatio, barH));
        var hpFt = new FormattedText($"HP {_enemyHp}/{EnemyHpMax}", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 13, Brushes.White);
        context.DrawText(hpFt, new Point(barX + barW + 10, barY));

        // Intent
        var intentFt = new FormattedText($"⚔ Next: -{EnemyIntent} HP", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 14, Brushes.IndianRed);
        context.DrawText(intentFt, new Point(barX + barW + 100, barY + 1));
    }

    private void DrawStageArea(DrawingContext context)
    {
        var stageRect = new Rect(20, StageTop, Bounds.Width - 40, StageBottom - StageTop);
        context.DrawRectangle(null, new Pen(Brushes.DimGray, 1), stageRect);

        switch (_state)
        {
            case GameState.Idle:
                var idleMsg = _energy > 0
                    ? "↓ カードをクリックして攻撃 / Energy 0 なら End Turn を押す"
                    : "↑ Energy 0。End Turn でターン終了";
                var alpha = (byte)(150 + Math.Sin(_hintPulse) * 50);
                var ft = new FormattedText(idleMsg, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 16,
                    new SolidColorBrush(Color.FromArgb(alpha, 200, 200, 200)));
                context.DrawText(ft, new Point((Bounds.Width - ft.Width) / 2, StageTop + 130));
                break;

            case GameState.Operating:
                var opMsg = $"飛んできた粒をクリックせよ (残り {_operationTimeRemaining / 60.0:F1}s)";
                var opFt = new FormattedText(opMsg, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 14, Brushes.Yellow);
                context.DrawText(opFt, new Point((Bounds.Width - opFt.Width) / 2, StageTop + 20));

                context.DrawEllipse(Brushes.White, null, new Point(_particleX, ParticleY), ParticleR, ParticleR);

                const double tbarW = 700;
                const double tbarY = StageTop + 50;
                var tbarX = (Bounds.Width - tbarW) / 2;
                var ratio = (double)_operationTimeRemaining / OperationTimeoutTicks;
                context.DrawRectangle(null, new Pen(Brushes.DimGray, 1), new Rect(tbarX, tbarY, tbarW, 8));
                context.FillRectangle(Brushes.Yellow, new Rect(tbarX, tbarY, tbarW * ratio, 8));
                break;

            case GameState.Resolving:
                var rMsg = _operationSuccess
                    ? $"成功! 障害源を {_damageDealt} 弱化"
                    : "失敗… 不発";
                var rColor = _operationSuccess ? Brushes.LightGreen : Brushes.OrangeRed;
                var rFt = new FormattedText(rMsg, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 22, rColor);
                context.DrawText(rFt, new Point((Bounds.Width - rFt.Width) / 2, StageTop + 130));
                break;

            case GameState.EnemyTurn:
                var etMsg = _enemyTurnApplied
                    ? $"敵の攻撃: -{_damageReceived} HP"
                    : "敵のターン...";
                var etFt = new FormattedText(etMsg, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 22, Brushes.IndianRed);
                context.DrawText(etFt, new Point((Bounds.Width - etFt.Width) / 2, StageTop + 130));
                break;
        }
    }

    private void DrawPlayerArea(DrawingContext context)
    {
        const double areaY = 450;

        var nameFt = new FormattedText("Player", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 14, Brushes.LightSkyBlue);
        context.DrawText(nameFt, new Point(20, areaY));

        const double hpBarX = 80, hpBarY = areaY + 2, hpBarW = 200, hpBarH = 14;
        context.DrawRectangle(null, new Pen(Brushes.White, 1), new Rect(hpBarX, hpBarY, hpBarW, hpBarH));
        var ratio = (double)_playerHp / PlayerHpMax;
        context.FillRectangle(Brushes.LimeGreen, new Rect(hpBarX, hpBarY, hpBarW * ratio, hpBarH));
        var hpFt = new FormattedText($"HP {_playerHp}/{PlayerHpMax}", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 12, Brushes.White);
        context.DrawText(hpFt, new Point(hpBarX + hpBarW + 8, hpBarY - 1));

        var energyFt = new FormattedText($"Energy {_energy}/{EnergyMax}", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 14,
            _energy > 0 ? Brushes.Yellow : Brushes.DimGray);
        context.DrawText(energyFt, new Point(hpBarX + hpBarW + 90, areaY));
    }

    private void DrawCard(DrawingContext context)
    {
        var canPlay = _state == GameState.Idle && _energy > 0;
        var cardBg = canPlay ? Brushes.SteelBlue : Brushes.DimGray;
        context.FillRectangle(cardBg, _cardRect);
        context.DrawRectangle(null, new Pen(Brushes.White, 1.5), _cardRect);

        var nameFt = new FormattedText("Probe Request", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 13, Brushes.White);
        context.DrawText(nameFt, new Point(_cardRect.X + (_cardRect.Width - nameFt.Width) / 2, _cardRect.Y + 8));

        var costFt = new FormattedText("1", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 16,
            canPlay ? Brushes.Yellow : Brushes.Gray);
        context.DrawText(costFt, new Point(_cardRect.X + _cardRect.Width - 18, _cardRect.Y + 6));

        var descLines = new[] { "効果:", "障害源を", "5 弱化", "", "(粒クリック)" };
        for (int i = 0; i < descLines.Length; i++)
        {
            var ft = new FormattedText(descLines[i], CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 11, Brushes.WhiteSmoke);
            context.DrawText(ft, new Point(_cardRect.X + 8, _cardRect.Y + 32 + i * 14));
        }
    }

    private void DrawEndTurnButton(DrawingContext context)
    {
        var enabled = _state == GameState.Idle;
        var bg = enabled
            ? (_energy == 0 ? Brushes.DarkOrange : Brushes.DarkSlateGray)
            : Brushes.DimGray;
        context.FillRectangle(bg, _endTurnRect);
        context.DrawRectangle(null, new Pen(Brushes.White, 1.5), _endTurnRect);

        var ft = new FormattedText("End Turn", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 18,
            enabled ? Brushes.White : Brushes.Gray);
        var tx = _endTurnRect.X + (_endTurnRect.Width - ft.Width) / 2;
        var ty = _endTurnRect.Y + (_endTurnRect.Height - ft.Height) / 2;
        context.DrawText(ft, new Point(tx, ty));
    }

    private void DrawStateOverlay(DrawingContext context)
    {
        if (_state == GameState.Won)
        {
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), new Rect(Bounds.Size));
            DrawCenteredBigText(context, "勝利!  クリックで再戦", Brushes.Gold);
        }
        else if (_state == GameState.Defeated)
        {
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), new Rect(Bounds.Size));
            DrawCenteredBigText(context, "敗北…  クリックで再戦", Brushes.Crimson);
        }
    }

    private void DrawCenteredBigText(DrawingContext context, string text, IBrush brush)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 36, brush);
        context.DrawText(ft, new Point((Bounds.Width - ft.Width) / 2, (Bounds.Height - ft.Height) / 2));
    }
}
