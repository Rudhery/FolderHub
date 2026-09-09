using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FolderHub.Services;

namespace FolderHub.Controls;

/// <summary>
/// Campo que aprende um atalho pressionando-o.
///
/// Digitar "Ctrl+Alt+Space" à mão é um convite a erro: o texto precisa casar com
/// o que o <see cref="GlobalHotKey.TryParse"/> entende, e um "Espaço" ou um
/// "Ctlr" só falham depois, sem nada na tela dizendo por quê. Aqui você aperta a
/// combinação e ela aparece.
///
/// Recusa uma combinação sem modificador porque o Windows recusa também — um
/// atalho global de tecla solta engoliria aquela tecla no sistema inteiro.
/// </summary>
public class HubHotKeyBox : Control
{
    static HubHotKeyBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HubHotKeyBox), new FrameworkPropertyMetadata(typeof(HubHotKeyBox)));

        FocusableProperty.OverrideMetadata(
            typeof(HubHotKeyBox), new FrameworkPropertyMetadata(true));
    }

    /// <summary>Disparado quando uma combinação válida é confirmada.</summary>
    public event EventHandler? HotKeyChanged;

    public static readonly DependencyProperty HotKeyProperty =
        DependencyProperty.Register(
            nameof(HotKey), typeof(string), typeof(HubHotKeyBox),
            new FrameworkPropertyMetadata(string.Empty, (d, _) => ((HubHotKeyBox)d).UpdateDisplay()));

    public string HotKey
    {
        get => (string)GetValue(HotKeyProperty);
        set => SetValue(HotKeyProperty, value);
    }

    // ------------------------------------------------------------- em captura

    private static readonly DependencyPropertyKey CapturingPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Capturing), typeof(bool), typeof(HubHotKeyBox),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty CapturingProperty = CapturingPropertyKey.DependencyProperty;

    /// <summary>Está esperando a combinação. O modelo troca o texto por um convite.</summary>
    public bool Capturing => (bool)GetValue(CapturingProperty);

    private static readonly DependencyPropertyKey PendingPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Pending), typeof(string), typeof(HubHotKeyBox),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty PendingProperty = PendingPropertyKey.DependencyProperty;

    /// <summary>Os modificadores já segurados, para a tela ir mostrando "Ctrl+Alt+…".</summary>
    public string Pending => (string)GetValue(PendingProperty);

    private static readonly DependencyPropertyKey DisplayPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Display), typeof(string), typeof(HubHotKeyBox),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty DisplayProperty = DisplayPropertyKey.DependencyProperty;

    /// <summary>
    /// O que mostrar, já decidido. Fica aqui e não no modelo do controle porque
    /// a escolha depende de três estados combinados, e um gatilho de XAML para
    /// cada cruzamento seria mais difícil de acertar do que quatro linhas de C#.
    /// </summary>
    public string Display => (string)GetValue(DisplayProperty);

    private void UpdateDisplay()
    {
        string text = Pending.Length > 0 ? Pending
                    : Capturing ? "pressione a combinação"
                    : HotKey.Length > 0 ? HotKey
                    : "nenhum";

        SetValue(DisplayPropertyKey, text);
    }

    // ---------------------------------------------------------------- captura

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        e.Handled = true;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        SetValue(CapturingPropertyKey, true);
        SetValue(PendingPropertyKey, string.Empty);
        UpdateDisplay();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        SetValue(CapturingPropertyKey, false);
        SetValue(PendingPropertyKey, string.Empty);
        UpdateDisplay();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (!Capturing) return;

        // A janela fecha no Esc. Enquanto este campo está capturando, o Esc é
        // dele: serve para desistir sem fechar a configuração inteira.
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Keyboard.ClearFocus();
            MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            return;
        }

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.Back or Key.Delete)
        {
            HotKey = string.Empty;
            SetValue(PendingPropertyKey, string.Empty);
            UpdateDisplay();
            HotKeyChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (HotKeyText.IsModifier(key))
        {
            // Só os modificadores até agora: mostra "Ctrl+Alt+…" enquanto espera.
            string held = HotKeyText.Describe(Keyboard.Modifiers);
            SetValue(PendingPropertyKey, held.Length > 0 ? held + "…" : string.Empty);
            UpdateDisplay();
            return;
        }

        if (!HotKeyText.TryFormat(Keyboard.Modifiers, key, out string spec))
        {
            // Quase sempre é a falta de modificador — o Windows recusa um atalho
            // global de tecla solta, que engoliria aquela tecla no sistema todo.
            SetValue(PendingPropertyKey, "precisa de Ctrl, Alt, Shift ou Win");
            UpdateDisplay();
            return;
        }

        HotKey = spec;
        SetValue(PendingPropertyKey, string.Empty);
        UpdateDisplay();
        HotKeyChanged?.Invoke(this, EventArgs.Empty);
    }
}
