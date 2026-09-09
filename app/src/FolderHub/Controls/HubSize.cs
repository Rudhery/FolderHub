using System.Windows;

namespace FolderHub.Controls;

/// <summary>
/// A escala de espaçamento do FolderHub, em passos de 4px.
///
/// Existe para que uma tela nova não invente números. Antes disto, cada margem
/// era um literal no XAML e não dava para saber, olhando, se o 14 de um lugar
/// era o mesmo 14 conceitual do outro ou coincidência.
///
/// O design tem alguns valores fora da escala (7, 9, 11, 15, 18, 22) em lugares
/// onde a medida veio do desenho e não de uma regra — esses continuam literais
/// no estilo do controle. A escala é para o que é decisão, não para o que é
/// desenho.
/// </summary>
public enum HubSize
{
    /// <summary>Não mexe no valor que o estilo já definiu.</summary>
    Inherit = -1,

    None = 0,
    XSmall = 4,
    Small = 8,
    Medium = 12,
    Large = 16,
    XLarge = 20,
    XXLarge = 24
}

public static class HubSizes
{
    /// <summary>Pixels da escala. <see cref="HubSize.Inherit"/> vira 0.</summary>
    public static double Pixels(this HubSize size) => size == HubSize.Inherit ? 0 : (int)size;

    /// <summary>Espessura uniforme, ou <c>null</c> quando é para não mexer.</summary>
    public static Thickness? ToThickness(this HubSize size)
        => size == HubSize.Inherit ? null : new Thickness(size.Pixels());
}
