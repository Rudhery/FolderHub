using System.Windows;
using System.Windows.Controls;

namespace FolderHub.Controls;

/// <summary>
/// O botão quadrado de ícone do cabeçalho.
///
///     &lt;hub:HubIconButton Glyph="Reload" ToolTip="Recarregar (F5)" /&gt;
///
/// Antes era um <c>Button</c> com estilo cujo <c>Content</c> era o codepoint
/// cru do glifo. Isso obrigava quem lia a decorar a tabela da fonte e obrigava o
/// estilo a impor a família — que, esquecida em qualquer outro botão, dava um
/// quadrado vazio só em tempo de execução.
/// </summary>
public class HubIconButton : Button
{
    static HubIconButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HubIconButton), new FrameworkPropertyMetadata(typeof(HubIconButton)));
    }

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(
            nameof(Glyph), typeof(HubGlyph), typeof(HubIconButton),
            new FrameworkPropertyMetadata(HubGlyph.None));

    public HubGlyph Glyph
    {
        get => (HubGlyph)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }
}
