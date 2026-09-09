using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FolderHub.Controls;

/// <summary>Como o <see cref="HubIcon"/> está desenhando agora.</summary>
public enum HubIconMode
{
    Empty,
    Image,
    Glyph,
    Text
}

/// <summary>
/// Um ícone, venha ele de onde vier.
///
/// O hub desenha ícone de três origens: a imagem extraída do atalho, um glifo da
/// fonte do Windows e a inicial do nome quando ainda não há imagem. Antes, cada
/// lugar montava isso na mão — no card era uma <c>Image</c> e um
/// <c>TextBlock</c> empilhados com dois conversores de visibilidade; nos botões,
/// a família de fonte repetida com um codepoint solto.
///
/// Aqui a origem é escolhida por precedência — imagem, glifo, texto — e o resto
/// da tela só diz o que quer:
///
///     &lt;hub:HubIcon Glyph="Reload" /&gt;
///     &lt;hub:HubIcon Source="{Binding Icon}" Text="{Binding Initial}" /&gt;
/// </summary>
public class HubIcon : Control
{
    static HubIcon()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HubIcon), new FrameworkPropertyMetadata(typeof(HubIcon)));

        // Ícone é decoração: não rouba clique de quem está por baixo.
        FocusableProperty.OverrideMetadata(
            typeof(HubIcon), new FrameworkPropertyMetadata(false));
    }

    /// <summary>A imagem do atalho. Tem precedência sobre tudo.</summary>
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(
            nameof(Source), typeof(ImageSource), typeof(HubIcon),
            new FrameworkPropertyMetadata(null, OnAnySourceChanged));

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Um ícone do conjunto da casa.</summary>
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(
            nameof(Glyph), typeof(HubGlyph), typeof(HubIcon),
            new FrameworkPropertyMetadata(HubGlyph.None, OnAnySourceChanged));

    public HubGlyph Glyph
    {
        get => (HubGlyph)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>O que desenhar quando não há imagem nem glifo — a inicial do nome.</summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(HubIcon),
            new FrameworkPropertyMetadata(null, OnAnySourceChanged));

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// O lado da caixa da imagem. Glifo e texto seguem o <c>FontSize</c>, que é a
    /// medida natural deles.
    /// </summary>
    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(
            nameof(Size), typeof(double), typeof(HubIcon),
            new FrameworkPropertyMetadata(20d));

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    // ------------------------------------------------------------------ modo

    private static readonly DependencyPropertyKey ModePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Mode), typeof(HubIconMode), typeof(HubIcon),
            new FrameworkPropertyMetadata(HubIconMode.Empty));

    public static readonly DependencyProperty ModeProperty = ModePropertyKey.DependencyProperty;

    /// <summary>Qual das três origens venceu. O modelo do controle usa isto para escolher o que mostrar.</summary>
    public HubIconMode Mode => (HubIconMode)GetValue(ModeProperty);

    private static readonly DependencyPropertyKey GlyphTextPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(GlyphText), typeof(string), typeof(HubIcon),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty GlyphTextProperty = GlyphTextPropertyKey.DependencyProperty;

    /// <summary>
    /// O caractere do glifo, pronto para o <c>TextBlock</c> do modelo. Precisa
    /// ser propriedade de dependência: uma propriedade CLR comum não avisaria o
    /// binding quando o glifo trocasse.
    /// </summary>
    public string GlyphText => (string)GetValue(GlyphTextProperty);

    private static void OnAnySourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HubIcon)d).UpdateMode();

    private void UpdateMode()
    {
        var mode = Source is not null ? HubIconMode.Image
                 : Glyph != HubGlyph.None ? HubIconMode.Glyph
                 : !string.IsNullOrEmpty(Text) ? HubIconMode.Text
                 : HubIconMode.Empty;

        SetValue(ModePropertyKey, mode);
        SetValue(GlyphTextPropertyKey, Glyph.Text());
    }
}
