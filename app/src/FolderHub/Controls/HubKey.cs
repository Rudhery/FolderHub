using System.Windows;
using System.Windows.Controls;

namespace FolderHub.Controls;

/// <summary>
/// Uma tecla desenhada: a moldurinha com <c>Tab</c>, <c>/</c>, <c>Esc</c>.
///
/// Era um <c>Border</c> com estilo mais um <c>TextBlock</c> com outro estilo,
/// dois elementos e duas referências a cada aparição. A tela de configuração vai
/// listar um atalho por linha, então isso ia multiplicar.
///
///     &lt;hub:HubKey Text="Tab" /&gt;
/// </summary>
public class HubKey : Control
{
    static HubKey()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HubKey), new FrameworkPropertyMetadata(typeof(HubKey)));

        FocusableProperty.OverrideMetadata(
            typeof(HubKey), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(HubKey),
            new FrameworkPropertyMetadata(string.Empty));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
