using System.Windows;
using System.Windows.Media;

namespace FolderHub.Services;

/// <summary>
/// A cor de cada hub na barra lateral, pela posição: o primeiro é azul, o
/// segundo rosa, e assim por diante. Cor por posição, e não por nome, para dois
/// hubs vizinhos nunca saírem parecidos.
/// </summary>
public static class HubPalette
{
    private static readonly (string Top, string Bottom)[] Pairs =
    [
        ("#60A5FA", "#2563EB"),   // azul
        ("#F472B6", "#DB2777"),   // rosa
        ("#A78BFA", "#6D28D9"),   // violeta
        ("#4ADE80", "#15803D"),   // verde
        ("#FDBA74", "#EA580C"),   // laranja
        ("#5EEAD4", "#0F766E"),   // verde-água
        ("#FCA5A5", "#DC2626"),   // vermelho
        ("#A1A1AA", "#3F3F46")    // grafite
    ];

    public static int Count => Pairs.Length;

    public static Brush For(int index)
    {
        var (top, bottom) = Pairs[((index % Pairs.Length) + Pairs.Length) % Pairs.Length];
        var brush = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(top),
            (Color)ColorConverter.ConvertFromString(bottom),
            new Point(0.2, 0), new Point(0.8, 1));
        brush.Freeze();
        return brush;
    }
}
