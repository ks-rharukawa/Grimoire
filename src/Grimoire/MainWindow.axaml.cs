using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Grimoire;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // GRIMOIRE_LIVE_CAPTURE=<path> が立っていれば、Loaded 後に Window 内容を
        // 実機 (macOS native Skia / フォント解決) で render → PNG 保存 → 終了。
        var capturePath = Environment.GetEnvironmentVariable("GRIMOIRE_LIVE_CAPTURE");
        if (!string.IsNullOrEmpty(capturePath))
        {
            Opened += async (_, _) =>
            {
                // 初回描画と font fallback の解決を待つ
                await Task.Delay(800);
                CaptureWindow(capturePath);
                Dispatcher.UIThread.Post(() =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                        lifetime.Shutdown();
                });
            };
        }
    }

    private void CaptureWindow(string outPath)
    {
        var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
        var rtb = new RenderTargetBitmap(size, new Vector(96, 96));
        rtb.Render(this);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        using var fs = File.Create(outPath);
        rtb.Save(fs);
        Console.WriteLine($"live captured: {outPath}");
    }
}