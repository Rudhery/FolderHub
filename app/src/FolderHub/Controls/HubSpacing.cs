using System.Windows;
using System.Windows.Controls;

namespace FolderHub.Controls;

/// <summary>
/// Espaçamento sem repetir <c>Margin</c>.
///
/// O CSS tem <c>gap</c>; o WPF não. A consequência era que todo filho de uma
/// pilha carregava a sua própria margem — <c>Margin="0,16,0,0"</c> repetido item
/// a item — e mudar o respiro de uma tela significava caçar e editar cada linha,
/// sem garantia de ter achado todas.
///
/// Aqui a pilha declara o respiro uma vez:
///
///     &lt;StackPanel hub:HubSpacing.Gap="Medium"&gt;
///
/// A margem entra em todos menos o primeiro filho visível, e só no lado do
/// empilhamento — o que o filho já tenha nos outros lados continua valendo.
/// </summary>
public static class HubSpacing
{
    // ------------------------------------------------------------------- Gap

    public static readonly DependencyProperty GapProperty =
        DependencyProperty.RegisterAttached(
            "Gap",
            typeof(HubSize),
            typeof(HubSpacing),
            new FrameworkPropertyMetadata(HubSize.Inherit, OnGapChanged));

    public static HubSize GetGap(DependencyObject element) => (HubSize)element.GetValue(GapProperty);

    public static void SetGap(DependencyObject element, HubSize value) => element.SetValue(GapProperty, value);

    /// <summary>
    /// Re-aplica o respiro. Só é preciso quando o código mexe na visibilidade de
    /// um filho depois da tela montada: o vão de um filho escondido some, e para
    /// saber disso seria preciso um gancho de layout permanente, caro num hub
    /// com centenas de cards.
    /// </summary>
    public static void Refresh(Panel panel) => Apply(panel);

    private static void OnGapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel) return;

        // No XAML a propriedade é lida antes de os filhos existirem, então a
        // aplicação de verdade é no Loaded. A imediata cobre quem já está de pé.
        panel.Loaded -= OnPanelLoaded;
        panel.Loaded += OnPanelLoaded;

        Apply(panel);
    }

    private static void OnPanelLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Panel panel) Apply(panel);
    }

    private static void Apply(Panel panel)
    {
        var gap = GetGap(panel);
        if (gap == HubSize.Inherit) return;

        bool horizontal = panel is StackPanel { Orientation: Orientation.Horizontal }
                          or VirtualizingStackPanel { Orientation: Orientation.Horizontal }
                          or WrapPanel { Orientation: Orientation.Horizontal };

        double px = gap.Pixels();
        bool first = true;

        foreach (var child in panel.Children)
        {
            if (child is not FrameworkElement element) continue;

            // Um filho escondido não deve deixar um buraco atrás de si.
            if (element.Visibility == Visibility.Collapsed) continue;

            double lead = first ? 0 : px;
            first = false;

            var current = element.Margin;
            var wanted = horizontal
                ? new Thickness(lead, current.Top, current.Right, current.Bottom)
                : new Thickness(current.Left, lead, current.Right, current.Bottom);

            if (current != wanted) element.Margin = wanted;
        }
    }

    // ----------------------------------------------------------------- Inset

    /// <summary>
    /// Preenchimento interno pela escala. Vale para qualquer coisa com
    /// <c>Padding</c> — <see cref="Control"/> e <see cref="Border"/>.
    /// </summary>
    public static readonly DependencyProperty InsetProperty =
        DependencyProperty.RegisterAttached(
            "Inset",
            typeof(HubSize),
            typeof(HubSpacing),
            new FrameworkPropertyMetadata(HubSize.Inherit, OnInsetChanged));

    public static HubSize GetInset(DependencyObject element) => (HubSize)element.GetValue(InsetProperty);

    public static void SetInset(DependencyObject element, HubSize value) => element.SetValue(InsetProperty, value);

    private static void OnInsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((HubSize)e.NewValue).ToThickness() is not { } padding) return;

        switch (d)
        {
            case Control control: control.Padding = padding; break;
            case Border border: border.Padding = padding; break;
        }
    }
}
