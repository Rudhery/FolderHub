using System.Windows;
using System.Windows.Controls;

namespace FolderHub.Controls;

/// <summary>
/// Uma linha de configuração: o que é à esquerda, o controle à direita.
///
///     &lt;hub:HubOption Title="Iniciar com o Windows"
///                    Description="O hub entra na bandeja quando você liga a máquina."&gt;
///         &lt;hub:HubToggle x:Name="Startup" /&gt;
///     &lt;/hub:HubOption&gt;
///
/// Existe porque a alternativa é repetir a mesma grade de três colunas com os
/// mesmos tamanhos e as mesmas cores em cada ajuste — e a tela de configuração
/// tem uma dúzia deles. Com um controle, uma linha nova são três linhas de XAML
/// e o alinhamento não tem como divergir entre elas.
/// </summary>
public class HubOption : ContentControl
{
    static HubOption()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HubOption), new FrameworkPropertyMetadata(typeof(HubOption)));
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(HubOption),
            new FrameworkPropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>A linha de baixo, que explica o ajuste. Vazia, some.</summary>
    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(HubOption),
            new FrameworkPropertyMetadata(null));

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}
