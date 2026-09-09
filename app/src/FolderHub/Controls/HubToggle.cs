using System.Windows;
using System.Windows.Controls.Primitives;

namespace FolderHub.Controls;

/// <summary>
/// A chavinha de ligar e desligar.
///
///     &lt;hub:HubToggle IsChecked="{Binding ...}" /&gt;
///
/// Herda de <see cref="ToggleButton"/> porque tudo de que ela precisa —
/// <c>IsChecked</c>, <c>Checked</c>, <c>Unchecked</c>, o clique, o espaço — já
/// está lá. O que muda é só o desenho.
/// </summary>
public class HubToggle : ToggleButton
{
    static HubToggle()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HubToggle), new FrameworkPropertyMetadata(typeof(HubToggle)));
    }
}
