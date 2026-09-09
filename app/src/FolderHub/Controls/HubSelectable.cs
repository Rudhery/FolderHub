using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FolderHub.Controls;

/// <summary>
/// A base do que pode ser escolhido — o card e a aba.
///
/// Herda de <see cref="ListBoxItem"/> de propósito: é o próprio recipiente da
/// lista, não algo dentro dele. Um invólucro a mais por item custaria caro numa
/// pasta com centenas de atalhos, que já é o limite conhecido do hub.
///
/// O que ela traz é dirigir os estados visuais. Antes, cada modelo de controle
/// cruzava as camadas de estado com storyboards escritos à mão — vinte e uma
/// animações no card, uma por camada e por destino, que era preciso manter em
/// três cópias coerentes. Aqui o modelo declara três estados e o
/// <see cref="VisualStateManager"/> gera a transição entre eles.
/// </summary>
public abstract class HubSelectable : ListBoxItem
{
    protected const string StatesGroup = "CommonStates";

    protected const string NormalState = "Normal";
    protected const string MouseOverState = "MouseOver";
    protected const string SelectedState = "Selected";

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // Sem transição na primeira vez: um card que abre já escolhido não deve
        // aparecer desbotando do estado normal para o de escolhido.
        UpdateVisualState(useTransitions: false);
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        UpdateVisualState();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        UpdateVisualState();
    }

    protected override void OnSelected(RoutedEventArgs e)
    {
        base.OnSelected(e);
        UpdateVisualState();
    }

    protected override void OnUnselected(RoutedEventArgs e)
    {
        base.OnUnselected(e);
        UpdateVisualState();
    }

    /// <summary>Escolhido vence o ponteiro: passar o mouse por cima do escolhido não o rebaixa.</summary>
    protected void UpdateVisualState(bool useTransitions = true)
    {
        string state = IsSelected ? SelectedState
                     : IsMouseOver ? MouseOverState
                     : NormalState;

        VisualStateManager.GoToState(this, state, useTransitions);
    }
}
