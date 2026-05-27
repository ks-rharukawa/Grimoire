using Avalonia.Media;

namespace Grimoire;

// docs/style-guide.md §2 (color palette) のソース・オブ・トゥルース。
// 値を変える場合は style-guide.md も同時に更新すること。
public static class Palette
{
    public static readonly Color Midnight = Color.Parse("#0a0e2e");
    public static readonly Color MidnightDeep = Color.Parse("#050715");
    public static readonly Color Parchment = Color.Parse("#f4e8c1");
    public static readonly Color ParchmentAged = Color.Parse("#d9c896");
    public static readonly Color ArcaneGold = Color.Parse("#d4af37");
    public static readonly Color ArcaneGoldDim = Color.Parse("#8a6f1f");
    public static readonly Color Crimson = Color.Parse("#c0392b");
    public static readonly Color CrimsonDim = Color.Parse("#6b1c14");
    public static readonly Color EtherealCyan = Color.Parse("#00d4ff");
    public static readonly Color EtherealCyanDim = Color.Parse("#0a6b88");
    public static readonly Color LimeGreen = Color.Parse("#3ddc4c");
    public static readonly Color Forest = Color.Parse("#1a3a1c");

    public static readonly IBrush MidnightBrush = new SolidColorBrush(Midnight);
    public static readonly IBrush MidnightDeepBrush = new SolidColorBrush(MidnightDeep);
    public static readonly IBrush ParchmentBrush = new SolidColorBrush(Parchment);
    public static readonly IBrush ParchmentAgedBrush = new SolidColorBrush(ParchmentAged);
    public static readonly IBrush ArcaneGoldBrush = new SolidColorBrush(ArcaneGold);
    public static readonly IBrush ArcaneGoldDimBrush = new SolidColorBrush(ArcaneGoldDim);
    public static readonly IBrush CrimsonBrush = new SolidColorBrush(Crimson);
    public static readonly IBrush CrimsonDimBrush = new SolidColorBrush(CrimsonDim);
    public static readonly IBrush EtherealCyanBrush = new SolidColorBrush(EtherealCyan);
    public static readonly IBrush EtherealCyanDimBrush = new SolidColorBrush(EtherealCyanDim);
    public static readonly IBrush LimeGreenBrush = new SolidColorBrush(LimeGreen);
    public static readonly IBrush ForestBrush = new SolidColorBrush(Forest);
}
