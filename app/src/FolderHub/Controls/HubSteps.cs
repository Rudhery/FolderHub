using System.Windows;
using System.Windows.Controls;

namespace FolderHub.Controls;

/// <summary>
/// Em que passo você está: um traço por passo, o atual mais comprido.
///
///     &lt;hub:HubSteps Count="3" Current="2" /&gt;
///
/// Os traços saem de <see cref="Marks"/> e não de markup repetido porque o
/// número de passos é do código, não da tela: a primeira execução tem três, e
/// um quarto passo não deveria pedir uma alteração de XAML em dois lugares — o
/// traço novo e o realce dele.
/// </summary>
public class HubSteps : Control
{
    static HubSteps()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HubSteps), new FrameworkPropertyMetadata(typeof(HubSteps)));
    }

    public static readonly DependencyProperty CountProperty =
        DependencyProperty.Register(
            nameof(Count), typeof(int), typeof(HubSteps),
            new FrameworkPropertyMetadata(0, (d, _) => ((HubSteps)d).Rebuild()));

    /// <summary>Quantos passos existem.</summary>
    public int Count
    {
        get => (int)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public static readonly DependencyProperty CurrentProperty =
        DependencyProperty.Register(
            nameof(Current), typeof(int), typeof(HubSteps),
            new FrameworkPropertyMetadata(1, (d, _) => ((HubSteps)d).Rebuild()));

    /// <summary>Qual deles é o de agora, contando de 1.</summary>
    public int Current
    {
        get => (int)GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }

    private static readonly DependencyPropertyKey MarksPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Marks), typeof(bool[]), typeof(HubSteps),
            new FrameworkPropertyMetadata(Array.Empty<bool>()));

    public static readonly DependencyProperty MarksProperty = MarksPropertyKey.DependencyProperty;

    /// <summary>Um item por traço; verdadeiro no atual.</summary>
    public bool[] Marks => (bool[])GetValue(MarksProperty);

    private void Rebuild()
    {
        var marks = new bool[Math.Max(0, Count)];
        for (int i = 0; i < marks.Length; i++) marks[i] = i + 1 == Current;

        SetValue(MarksPropertyKey, marks);
    }
}
