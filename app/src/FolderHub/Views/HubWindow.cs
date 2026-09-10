using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using FolderHub.Controls;
using FolderHub.Services;

namespace FolderHub.Views;

/// <summary>
/// A casca comum das janelas do FolderHub: sem barra de título, acrílico do
/// Windows 11, cantos arredondados pelo DWM, Esc fecha, e a janela entra e sai
/// com um fade.
///
/// Existe para que uma tela nova seja só o seu conteúdo. Antes disso, cada
/// janela repetia as mesmas ~28 linhas de configuração de chrome no XAML mais o
/// código de acrílico e de animação — e errar uma delas (ou mexer em
/// ShowInTaskbar depois do handle nascer) quebra coisas de um jeito silencioso.
///
/// A janela derivada só precisa nomear a borda raiz do seu conteúdo como
/// "Shell": é ela que recebe o fade.
/// </summary>
public class HubWindow : Window
{
    private readonly bool _acrylic;

    /// <param name="acrylic">
    /// Usar o acrílico do Windows. Ligado, o DWM desenha o fundo e arredonda a
    /// janela — em cerca de 8px, que é o raio do hub.
    ///
    /// Desligado, a janela fica de verdade transparente e quem define a forma é
    /// a borda do conteúdo. É o que a configuração precisa: ela pede canto de
    /// 22px, e com o DWM arredondando em 8 sobrava uma cunha escura fora do
    /// arco. A superfície dela é 92% opaca, então o acrílico atrás quase não
    /// aparecia — e em troca vem a sombra projetada que o design pede.
    /// </param>
    protected HubWindow(bool acrylic = true)
    {
        _acrylic = acrylic;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;

        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

        if (Application.Current?.TryFindResource("UiFont") is FontFamily font) FontFamily = font;

        if (!acrylic)
        {
            // Precisa ser antes de o handle nascer. Com isto não há moldura
            // nativa nenhuma, então o WindowChrome também não faz falta.
            AllowsTransparency = true;
            return;
        }

        // Tira a moldura nativa sem perder o redimensionamento por código nem a
        // sombra que o DWM desenha em volta.
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            CornerRadius = default,
            GlassFrameThickness = default,
            NonClientFrameEdges = NonClientFrameEdges.None,
            ResizeBorderThickness = default,
            UseAeroCaptionButtons = false
        });
    }

    /// <summary>
    /// A borda raiz do conteúdo, se a janela derivada tiver nomeado uma "Shell".
    /// O nome é HubShell, e não Shell, porque o x:Name da janela derivada gera um
    /// campo com esse nome — os dois colidiriam.
    /// </summary>
    protected FrameworkElement? HubShell => FindName("Shell") as FrameworkElement;

    /// <summary>Já está saindo da tela. Não é `Closing`: esse nome é um evento de Window.</summary>
    protected bool Dismissing { get; set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (_acrylic) WindowEffects.ApplyAcrylic(this);
    }

    /// <summary>Esc fecha. Em <c>OnKeyDown</c>, então quem quiser tratar antes usa o preview.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || e.Key != Key.Escape) return;

        e.Handled = true;
        Dismiss();
    }

    /// <summary>Arrastar a janela pela área que a derivada quiser.</summary>
    protected void DragFrom(MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        try { DragMove(); }
        catch (InvalidOperationException) { /* o botão já foi solto */ }
    }

    protected void FadeIn(double lift = 8, int milliseconds = 160)
    {
        if (HubShell is null) return;

        if (HubMotion.Reduced)
        {
            HubShell.BeginAnimation(OpacityProperty, null);
            HubShell.Opacity = 1;
            if (HubShell.RenderTransform is TranslateTransform instant)
            {
                instant.BeginAnimation(TranslateTransform.YProperty, null);
                instant.Y = 0;
            }
            return;
        }

        HubShell.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(milliseconds)));

        if (HubShell.RenderTransform is TranslateTransform slide)
        {
            slide.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(lift, 0, TimeSpan.FromMilliseconds(milliseconds + 80))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }
    }

    /// <summary>Some da tela. O padrão é fechar; o hub sobrescreve para esconder.</summary>
    protected virtual void Dismiss()
    {
        if (Dismissing) return;
        Dismissing = true;

        if (HubShell is null)
        {
            Close();
            return;
        }

        var fade = new DoubleAnimation(HubShell.Opacity, 0, TimeSpan.FromMilliseconds(100));
        fade.Completed += (_, _) => Close();
        HubShell.BeginAnimation(OpacityProperty, fade);
    }
}
