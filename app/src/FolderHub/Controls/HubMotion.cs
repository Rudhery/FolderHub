using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FolderHub.Controls;

/// <summary>
/// O movimento do FolderHub em um lugar só.
///
/// Antes disto cada animação era montada à mão onde era usada, com o seu próprio
/// número de milissegundos e a sua própria curva. Havia quatro "fade de saída"
/// no código, três deles com 100ms e um com 140, sem que a diferença fosse
/// intencional.
///
/// É a fonte única do tempo: o XAML lê daqui com <c>{x:Static}</c> em vez de
/// repetir os valores. Movimento é personalidade do app, não tema — por isso
/// mora em código e não em um arquivo de tema sobrescrevível.
/// </summary>
public static class HubMotion
{
    /// <summary>Quando ligado, as transições são instantâneas.</summary>
    public static bool Reduced { get; private set; }

    public static void SetReduced(bool reduced) => Reduced = reduced;

    /// <summary>Troca de estado (hover, seleção). Curto o bastante para não atrasar o clique.</summary>
    public static Duration State => new(TimeSpan.FromMilliseconds(Reduced ? 0 : 150));

    /// <summary>Algo entrando na tela.</summary>
    public static Duration Enter => new(TimeSpan.FromMilliseconds(Reduced ? 0 : 200));

    /// <summary>O deslize que acompanha a entrada — um pouco mais longo, para assentar depois do fade.</summary>
    public static Duration Slide => new(TimeSpan.FromMilliseconds(Reduced ? 0 : 280));

    /// <summary>Algo saindo. Sempre mais rápido que a entrada: esperar para sumir irrita.</summary>
    public static Duration Exit => new(TimeSpan.FromMilliseconds(Reduced ? 0 : 100));

    /// <summary>Véu do arrastar e soltar, que aparece e some no mesmo ritmo.</summary>
    public static Duration Veil => new(TimeSpan.FromMilliseconds(Reduced ? 0 : 140));

    /// <summary>Atraso entre um card e o próximo na entrada escalonada.</summary>
    public const int StaggerStepMs = 12;

    /// <summary>
    /// Depois deste índice o escalonamento para de crescer. Sem o teto, o
    /// último card de uma pasta com 800 atalhos entraria dez segundos depois
    /// do primeiro.
    /// </summary>
    public const int StaggerCap = 42;

    /// <summary>A curva da casa: sai rápido e desacelera. Congelada, então é uma instância só.</summary>
    public static readonly IEasingFunction EaseOut = Freeze(new CubicEase { EasingMode = EasingMode.EaseOut });

    private static IEasingFunction Freeze(CubicEase ease)
    {
        ease.Freeze();
        return ease;
    }

    // ------------------------------------------------------------- ajudantes

    /// <summary>Aparece.</summary>
    public static void FadeIn(UIElement element, Duration? duration = null)
    {
        if (Reduced)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 1;
            return;
        }

        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duration ?? Enter));
    }

    /// <summary>Some, e avisa quando terminou — é onde o fechar de verdade acontece.</summary>
    public static void FadeOut(UIElement element, Action? then = null, Duration? duration = null)
    {
        if (Reduced)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 0;
            then?.Invoke();
            return;
        }

        var fade = new DoubleAnimation(element.Opacity, 0, duration ?? Exit);
        if (then is not null) fade.Completed += (_, _) => then();
        element.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>
    /// Entra subindo: o fade e o deslize juntos. O deslize precisa de um
    /// <see cref="TranslateTransform"/>; se o elemento não tiver um, ganha um.
    /// </summary>
    public static void RiseIn(UIElement element, double lift = 8, TimeSpan delay = default)
    {
        var transform = element.RenderTransform as TranslateTransform;
        if (transform is null || transform.IsFrozen)
        {
            transform = new TranslateTransform(0, lift);
            element.RenderTransform = transform;
        }

        if (Reduced)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 1;
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            transform.Y = 0;
            return;
        }

        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, Enter) { BeginTime = delay });

        transform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(lift, 0, Slide) { BeginTime = delay, EasingFunction = EaseOut });
    }

    /// <summary>O atraso do card de índice <paramref name="index"/> na entrada escalonada.</summary>
    public static TimeSpan Stagger(int index)
        => TimeSpan.FromMilliseconds(Math.Clamp(index, 0, StaggerCap) * StaggerStepMs);
}
