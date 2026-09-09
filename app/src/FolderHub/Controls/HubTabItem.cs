using System.Windows;

namespace FolderHub.Controls;

/// <summary>
/// A pílula de uma aba: bloquinho, nome e contagem.
///
/// Chama-se HubTabItem, e não HubTab, porque <c>Models.HubTab</c> é a aba em si —
/// a pasta com a sua lista e o seu cache de ícones. Esta é só como ela aparece.
/// </summary>
public class HubTabItem : HubSelectable
{
    static HubTabItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HubTabItem), new FrameworkPropertyMetadata(typeof(HubTabItem)));
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(HubTabItem),
            new FrameworkPropertyMetadata(string.Empty));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Quantos atalhos a aba tem. Texto, e não número, para caber um "—" quando a pasta sumiu.</summary>
    public static readonly DependencyProperty TallyProperty =
        DependencyProperty.Register(
            nameof(Tally), typeof(string), typeof(HubTabItem),
            new FrameworkPropertyMetadata(string.Empty));

    public string Tally
    {
        get => (string)GetValue(TallyProperty);
        set => SetValue(TallyProperty, value);
    }
}
