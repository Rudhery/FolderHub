using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FolderHub.Services;

/// <summary>Marca de seleção do menu: falso some, e some sem deixar buraco.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        if (Invert) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Aparece só enquanto o valor é nulo. É o quadrado da inicial na grade: some
/// quando o ícone de verdade chega, e o ícone fica solto, sem caixa em volta.
/// </summary>
public sealed class NullToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Texto vazio some, e some sem deixar buraco.
///
/// Um item das notas de versão pode vir só com título, quando a linha do
/// RELEASE_NOTES.md não traz o travessão. Sem isto o parágrafo vazio ainda
/// ocupava a altura de uma linha, e a caixa ficava com um degrau a mais embaixo
/// de umas e não de outras.
/// </summary>
public sealed class EmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
