using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FolderHub.Controls;

/// <summary>
/// Um número com menos e mais.
///
///     &lt;hub:HubStepper Minimum="3" Maximum="10" Value="7" /&gt;
///
/// Escolhido no lugar de um controle deslizante porque os intervalos daqui são
/// curtos e discretos — número de colunas, densidade — e num deslizante de sete
/// posições acertar a do meio é sorte. Aqui cada clique é um passo exato, e os
/// botões apagam sozinhos ao chegar na ponta.
/// </summary>
public class HubStepper : Control
{
    public const string DecreaseButton = "PART_Decrease";
    public const string IncreaseButton = "PART_Increase";

    static HubStepper()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HubStepper), new FrameworkPropertyMetadata(typeof(HubStepper)));
    }

    /// <summary>Disparado quando o valor muda por clique — não quando quem hospeda o define.</summary>
    public event EventHandler? ValueChanged;

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value), typeof(int), typeof(HubStepper),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum), typeof(int), typeof(HubStepper),
            new FrameworkPropertyMetadata(0, OnRangeChanged));

    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum), typeof(int), typeof(HubStepper),
            new FrameworkPropertyMetadata(100, OnRangeChanged));

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>O que aparece ao lado do número. Ex.: "colunas".</summary>
    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(
            nameof(Unit), typeof(string), typeof(HubStepper),
            new FrameworkPropertyMetadata(null));

    public string? Unit
    {
        get => (string?)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    // ---------------------------------------------------------- pontas da faixa

    private static readonly DependencyPropertyKey CanDecreasePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanDecrease), typeof(bool), typeof(HubStepper),
            new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty CanDecreaseProperty = CanDecreasePropertyKey.DependencyProperty;

    public bool CanDecrease => (bool)GetValue(CanDecreaseProperty);

    private static readonly DependencyPropertyKey CanIncreasePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanIncrease), typeof(bool), typeof(HubStepper),
            new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty CanIncreaseProperty = CanIncreasePropertyKey.DependencyProperty;

    public bool CanIncrease => (bool)GetValue(CanIncreaseProperty);

    // ------------------------------------------------------------------ modelo

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild(DecreaseButton) is ButtonBase less)
        {
            less.Click -= OnDecrease;
            less.Click += OnDecrease;
        }

        if (GetTemplateChild(IncreaseButton) is ButtonBase more)
        {
            more.Click -= OnIncrease;
            more.Click += OnIncrease;
        }

        UpdateEnds();
    }

    private void OnDecrease(object sender, RoutedEventArgs e) => Step(-1);

    private void OnIncrease(object sender, RoutedEventArgs e) => Step(1);

    private void Step(int delta)
    {
        int wanted = Math.Clamp(Value + delta, Minimum, Maximum);
        if (wanted == Value) return;

        Value = wanted;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var stepper = (HubStepper)d;
        return Math.Clamp((int)baseValue, stepper.Minimum, stepper.Maximum);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HubStepper)d).UpdateEnds();

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        d.CoerceValue(ValueProperty);
        ((HubStepper)d).UpdateEnds();
    }

    private void UpdateEnds()
    {
        SetValue(CanDecreasePropertyKey, Value > Minimum);
        SetValue(CanIncreasePropertyKey, Value < Maximum);
    }
}
