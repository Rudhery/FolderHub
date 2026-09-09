using System.Windows;
using System.Windows.Controls;

namespace FolderHub.Controls;

/// <summary>
/// Uma <see cref="ListBox"/> que produz <see cref="HubCard"/> em vez de
/// <see cref="ListBoxItem"/>.
///
/// É o único gancho que o WPF dá para trocar o tipo do recipiente:
/// <c>ItemContainerStyle</c> muda a aparência, nunca a classe. E precisa ser a
/// classe, porque é ela que dirige os estados visuais.
/// </summary>
public class HubCardList : ListBox
{
    protected override DependencyObject GetContainerForItemOverride() => new HubCard();

    protected override bool IsItemItsOwnContainerOverride(object item) => item is HubCard;
}

/// <summary>A faixa de abas. Mesmo motivo, outro recipiente.</summary>
public class HubTabList : ListBox
{
    protected override DependencyObject GetContainerForItemOverride() => new HubTabItem();

    protected override bool IsItemItsOwnContainerOverride(object item) => item is HubTabItem;
}
