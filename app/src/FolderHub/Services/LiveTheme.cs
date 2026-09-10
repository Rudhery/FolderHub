using System.Windows;
using System.Windows.Media;
using FolderHub.Controls;

namespace FolderHub.Services;

/// <summary>
/// Os ajustes que mexem no próprio tema em tempo de execução: a densidade dos
/// cards e a transparência da superfície.
///
/// Sobrescrever a chave no dicionário da aplicação é o único jeito de a mudança
/// alcançar quem já está na tela — e por isso os elementos que dependem dessas
/// chaves as leem por <c>DynamicResource</c>, não por <c>StaticResource</c>,
/// que resolve uma vez e nunca mais olha.
///
/// Os valores originais do tema são guardados na primeira chamada. Sem isso,
/// voltar para a densidade padrão devolveria o número embutido no app e não o
/// que um arquivo de tema tivesse pedido.
/// </summary>
public static class LiveTheme
{
    public static ThemeMode AppliedMode { get; private set; } = ThemeMode.Dark;
    private static (double Width, double Height, double Tile)? _themeCards;
    private static Color? _themeSurface;
    private static ThemeMode? _themeSurfaceMode;
    private static ResourceDictionary? _lightTheme;

    /// <summary>Aplica densidade e transparência por cima do tema carregado.</summary>
    public static void Apply(HubConfig config, bool applyTheme = true)
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        if (applyTheme) ApplyThemeMode(resources, config.ThemeMode);
        HubMotion.SetReduced(config.ReduceMotion);
        resources["MotionStateDuration"] = HubMotion.State;
        RememberTheme(resources);

        ApplyDensity(resources, config.Density);
        ApplyTransparency(resources, config.Transparency);
    }

    private static void ApplyThemeMode(ResourceDictionary resources, ThemeMode mode)
    {
        ResourceDictionary? controls = resources.MergedDictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("HubControls.xaml", StringComparison.OrdinalIgnoreCase) == true);
        int controlsIndex = controls is null ? -1 : resources.MergedDictionaries.IndexOf(controls);

        if (controls is not null) resources.MergedDictionaries.Remove(controls);

        if (mode == ThemeMode.Light)
        {
            _lightTheme ??= new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/FolderHub;component/Themes/Light.xaml", UriKind.Absolute)
            };

            if (resources.MergedDictionaries.Contains(_lightTheme))
                resources.MergedDictionaries.Remove(_lightTheme);

            int lightIndex = controlsIndex >= 0
                ? Math.Min(controlsIndex, resources.MergedDictionaries.Count)
                : resources.MergedDictionaries.Count;
            resources.MergedDictionaries.Insert(lightIndex, _lightTheme);
        }
        else if (_lightTheme is not null)
        {
            resources.MergedDictionaries.Remove(_lightTheme);
        }

        if (controls is not null)
        {
            var refreshed = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/FolderHub;component/Themes/HubControls.xaml", UriKind.Absolute)
            };
            int insertAt = Math.Min(controlsIndex < 0 ? resources.MergedDictionaries.Count : controlsIndex + (mode == ThemeMode.Light ? 1 : 0), resources.MergedDictionaries.Count);
            resources.MergedDictionaries.Insert(insertAt, refreshed);
        }

        // A superfície-base muda com o tema. Sem invalidar este cache, o
        // slider de transparência aplicaria a cor grafite anterior sobre o
        // tema claro.
        if (_themeSurfaceMode != mode)
        {
            _themeSurface = null;
            _themeSurfaceMode = mode;
        }

        AppliedMode = mode;
    }

    private static void RememberTheme(ResourceDictionary resources)
    {
        _themeCards ??= (
            Number(resources, "CardWidth", 160),
            Number(resources, "CardHeight", 118),
            Number(resources, "TileSize", 40));

        if (_themeSurface is null && resources["Surface"] is SolidColorBrush surface)
        {
            _themeSurface = surface.Color;
        }
    }

    private static double Number(ResourceDictionary resources, string key, double fallback)
        => resources[key] is double value && value > 0 ? value : fallback;

    private static void ApplyDensity(ResourceDictionary resources, CardDensity density)
    {
        // No padrão vale o que o tema disse; nas outras, a densidade manda.
        var cards = density == CardDensity.Default
            ? _themeCards!.Value
            : density.Metrics();

        resources["CardWidth"] = cards.Width;
        resources["CardHeight"] = cards.Height;
        resources["TileSize"] = cards.Tile;
    }

    private static void ApplyTransparency(ResourceDictionary resources, int percent)
    {
        if (_themeSurface is not { } color) return;

        // O percentual precisa representar transparência de verdade. Antes, 30%
        // deixava alpha 219/255: a superfície só perdia 14% da opacidade e o
        // acrylic parecia apenas uma cor desbotada.
        //
        // No claro preservamos um pouco mais do véu branco para o vidro continuar
        // branco sobre desktops escuros, em vez de virar cinza. O backdrop nativo
        // do DWM continua responsável pelo desfoque atrás da superfície.
        double strength = AppliedMode == ThemeMode.Light ? 1.55 : 2.1;
        byte alpha = (byte)Math.Clamp(
            255 - Math.Clamp(percent, 0, 90) * strength,
            AppliedMode == ThemeMode.Light ? 115 : 55,
            255);

        var tinted = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        tinted.Freeze();

        resources["Surface"] = tinted;
    }
}
