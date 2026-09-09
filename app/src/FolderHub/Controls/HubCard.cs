using System.Windows;
using System.Windows.Media;

namespace FolderHub.Controls;

/// <summary>Quanto peso o card tem na tela.</summary>
public enum HubCardVariant
{
    /// <summary>O card do hub: fundo quase invisível, borda de 1px. O padrão.</summary>
    Flat,

    /// <summary>
    /// Sobe um degrau: fundo e borda mais fortes, com o anel externo sempre
    /// aceso. Para o que a tela quer que você olhe primeiro — o passo atual da
    /// primeira instalação, a seção aberta da configuração.
    /// </summary>
    Raised,

    /// <summary>
    /// Sem fundo nem borda até o ponteiro chegar. Para linha de lista, onde uma
    /// borda por item viraria grade.
    /// </summary>
    Quiet
}

/// <summary>Qual das duas formas o card está usando.</summary>
public enum HubCardLayout
{
    /// <summary>Conteúdo livre, como qualquer <c>ContentControl</c>.</summary>
    Content,

    /// <summary>Bloco de ícone em cima, rótulo embaixo — o card do hub.</summary>
    Tile
}

/// <summary>
/// O card do FolderHub: uma superfície com estado e peso.
///
///     &lt;hub:HubCard Variant="Raised" Spacing="Medium"&gt;
///         &lt;TextBlock Text="Comece escolhendo uma pasta" /&gt;
///     &lt;/hub:HubCard&gt;
///
///     &lt;hub:HubCard Icon="{Binding Icon}" IconText="{Binding Initial}" Label="{Binding Name}" /&gt;
///
/// Duas formas, um controle. Dizer <see cref="Icon"/>, <see cref="IconText"/>,
/// <see cref="Glyph"/> ou <see cref="Label"/> escolhe a forma de bloco; sem
/// nenhum deles, o card é um <c>ContentControl</c> comum.
///
/// A precedência existe por um motivo prático: dentro de uma lista, o WPF põe o
/// próprio item de dados em <c>Content</c>. Se as duas formas convivessem, o
/// card do hub mostraria o <c>ToString()</c> do <c>AppItem</c> por baixo do
/// ícone.
///
/// Herda de <see cref="HubSelectable"/>, então já sabe se está escolhido e sob o
/// ponteiro, e o modelo cruza os estados pelo <c>VisualStateManager</c>.
/// </summary>
public class HubCard : HubSelectable
{
    static HubCard()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HubCard), new FrameworkPropertyMetadata(typeof(HubCard)));
    }

    // ---------------------------------------------------------------- aspecto

    public static readonly DependencyProperty VariantProperty =
        DependencyProperty.Register(
            nameof(Variant), typeof(HubCardVariant), typeof(HubCard),
            new FrameworkPropertyMetadata(HubCardVariant.Flat));

    public HubCardVariant Variant
    {
        get => (HubCardVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>
    /// Respiro interno pela escala. Sem valor, vale o do modelo — que no card do
    /// hub é <c>8,20,8,15</c>, medida vinda do desenho e não de uma regra.
    /// </summary>
    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.Register(
            nameof(Spacing), typeof(HubSize), typeof(HubCard),
            new FrameworkPropertyMetadata(HubSize.Inherit, OnSpacingChanged));

    public HubSize Spacing
    {
        get => (HubSize)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((HubSize)e.NewValue).ToThickness() is { } padding) ((HubCard)d).Padding = padding;
    }

    // ------------------------------------------------------------------ bloco

    /// <summary>A imagem do atalho.</summary>
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon), typeof(ImageSource), typeof(HubCard),
            new FrameworkPropertyMetadata(null, OnTileSourceChanged));

    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>A inicial do nome, enquanto a imagem não chegou.</summary>
    public static readonly DependencyProperty IconTextProperty =
        DependencyProperty.Register(
            nameof(IconText), typeof(string), typeof(HubCard),
            new FrameworkPropertyMetadata(null, OnTileSourceChanged));

    public string? IconText
    {
        get => (string?)GetValue(IconTextProperty);
        set => SetValue(IconTextProperty, value);
    }

    /// <summary>Um ícone da casa, quando o card não representa um atalho.</summary>
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(
            nameof(Glyph), typeof(HubGlyph), typeof(HubCard),
            new FrameworkPropertyMetadata(HubGlyph.None, OnTileSourceChanged));

    public HubGlyph Glyph
    {
        get => (HubGlyph)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(HubCard),
            new FrameworkPropertyMetadata(null, OnTileSourceChanged));

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    // ----------------------------------------------------------------- forma

    private static readonly DependencyPropertyKey LayoutPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Layout), typeof(HubCardLayout), typeof(HubCard),
            new FrameworkPropertyMetadata(HubCardLayout.Content));

    public static readonly DependencyProperty LayoutProperty = LayoutPropertyKey.DependencyProperty;

    /// <summary>Qual forma venceu. O modelo do controle usa isto para escolher o que mostrar.</summary>
    public HubCardLayout Layout => (HubCardLayout)GetValue(LayoutProperty);

    private static void OnTileSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (HubCard)d;

        bool tile = card.Icon is not null
                    || card.Glyph != HubGlyph.None
                    || !string.IsNullOrEmpty(card.IconText)
                    || !string.IsNullOrEmpty(card.Label);

        card.SetValue(LayoutPropertyKey, tile ? HubCardLayout.Tile : HubCardLayout.Content);
    }
}
