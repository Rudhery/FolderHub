using System.Windows;
using System.Windows.Controls;

namespace FolderHub.Controls;

/// <summary>
/// Um bloco de ajustes: título, uma linha explicando, e as linhas em si.
///
///     &lt;hub:HubGroup Title="Sistema"
///                   Description="Como o Folder Hub se comporta ao ligar o computador."&gt;
///         &lt;hub:HubOption … /&gt;
///         &lt;hub:HubOption … /&gt;
///     &lt;/hub:HubGroup&gt;
///
/// O design repete essa estrutura oito vezes na configuração, sempre com o
/// mesmo respiro e o mesmo filete acima da primeira linha. Escrita à mão, seria
/// oito chances de o espaçamento divergir de um bloco para o outro.
/// </summary>
public class HubGroup : ContentControl
{
    static HubGroup()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HubGroup), new FrameworkPropertyMetadata(typeof(HubGroup)));
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(HubGroup),
            new FrameworkPropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(HubGroup),
            new FrameworkPropertyMetadata(null));

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// O que aparece à direita do título — a contagem "3 de 8", por exemplo.
    /// </summary>
    public static readonly DependencyProperty AsideProperty =
        DependencyProperty.Register(
            nameof(Aside), typeof(string), typeof(HubGroup),
            new FrameworkPropertyMetadata(null));

    public string? Aside
    {
        get => (string?)GetValue(AsideProperty);
        set => SetValue(AsideProperty, value);
    }

    /// <summary>
    /// Desenha o filete acima da primeira linha. Ligado para uma lista de
    /// <see cref="HubOption"/>, desligado quando o conteúdo é uma caixa própria
    /// — o quadro do atalho, o controle segmentado — que já tem borda.
    /// </summary>
    public static readonly DependencyProperty RuledProperty =
        DependencyProperty.Register(
            nameof(Ruled), typeof(bool), typeof(HubGroup),
            new FrameworkPropertyMetadata(true));

    public bool Ruled
    {
        get => (bool)GetValue(RuledProperty);
        set => SetValue(RuledProperty, value);
    }
}
