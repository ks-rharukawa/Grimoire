using System;
using System.IO;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;

namespace Grimoire;

// dev 用: CombatView を offscreen で描画して PNG に保存する。
// 使い方: GRIMOIRE_CAPTURE=/path/to.png dotnet run
public static class HeadlessCapture
{
    public static bool MaybeCapture()
    {
        var outPath = Environment.GetEnvironmentVariable("GRIMOIRE_CAPTURE");
        if (string.IsNullOrEmpty(outPath)) return false;

        var view = new CombatView
        {
            Width = 1280,
            Height = 720,
        };
        view.Measure(new Size(1280, 720));
        view.Arrange(new Rect(0, 0, 1280, 720));

        var rtb = new RenderTargetBitmap(new PixelSize(1280, 720), new Vector(96, 96));
        rtb.Render(view);

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        using var fs = File.Create(outPath);
        rtb.Save(fs);
        Console.WriteLine($"captured: {outPath}");
        return true;
    }
}
