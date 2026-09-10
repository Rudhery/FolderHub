using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FolderHub.Models;
using FolderHub.Services;

namespace FolderHub;

/// <summary>
/// Abas: o hub aponta para várias pastas, não só uma.
///
/// O carregamento é preguiçoso onde importa. Na abertura, cada aba faz apenas
/// uma contagem barata (olhar a extensão dos nomes) — é o que dimensiona a
/// janela para a maior aba, para ela não pular de tamanho a cada troca. A lista
/// completa e a extração dos ícones, que é a parte cara, só acontecem quando a
/// aba é aberta. Depois disso os ícones ficam no cache da própria aba, então
/// voltar para ela é instantâneo.
/// </summary>
public partial class MainWindow
{
    private readonly ObservableCollection<HubTab> _tabs = [];

    private HubTab? _tab;
    private HubTab? _menuTab;
    private bool _switchingTab;

    // ------------------------------------------------------------ montagem

    private void BuildTabs(IReadOnlyList<HubTab> tabs)
    {
        _tabs.Clear();
        foreach (var tab in tabs) _tabs.Add(tab);

        foreach (var tab in _tabs)
        {
            tab.Count = FolderScanner.CountSupported(tab.Path);
        }

        TabStrip.ItemsSource = _tabs;
        TabRow.Visibility = !App.Config.SingleFolder && _tabs.Count > 1
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>Maior aba, para a janela caber em todas sem redimensionar na troca.</summary>
    private int LargestTabCount()
    {
        int largest = 0;
        foreach (var tab in _tabs) largest = Math.Max(largest, tab.Count);
        return largest;
    }

    // ------------------------------------------------------------ ativação

    private void ActivateTab(HubTab tab, bool resize, bool animate)
    {
        _switchingTab = true;
        try
        {
            _tab = tab;

            int tabIndex = _tabs.IndexOf(tab);
            if (!App.Config.SingleFolder && App.Config.RememberLastHub && tabIndex >= 0
                && App.Config.LastHub != tabIndex)
            {
                App.Config.LastHub = tabIndex;
                App.Config.Save();
            }

            // As coleções da janela passam a apontar para as da aba: assim o
            // resto do código continua trabalhando com _all e _iconCache sem
            // saber que existe aba nenhuma.
            _all = tab.Items;
            _iconCache = tab.Icons;
            _folder = tab.Path;

            bool first = !tab.Visited;
            tab.Visited = true;

            SearchBox.Clear();
            Reload(resize: resize, animate: animate || first);

            tab.Count = _all.Count;

            if (TabStrip.SelectedItem != tab) TabStrip.SelectedItem = tab;
            StartWatching();
        }
        finally
        {
            _switchingTab = false;
        }
    }

    private void TabStrip_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_switchingTab || TabStrip.SelectedItem is not HubTab tab || ReferenceEquals(tab, _tab)) return;

        ActivateTab(tab, resize: false, animate: true);
    }

    private void MoveTab(int delta)
    {
        if (_tabs.Count < 2 || _tab is null) return;

        int index = _tabs.IndexOf(_tab);
        if (index < 0) return;

        index = (index + delta + _tabs.Count) % _tabs.Count;
        ActivateTab(_tabs[index], resize: false, animate: true);
    }

    private void ActivateTabAt(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        if (ReferenceEquals(_tabs[index], _tab)) return;

        ActivateTab(_tabs[index], resize: false, animate: true);
    }

    // ------------------------------------------------------------ editar

    /// <summary>Solta uma pasta na janela: vira aba nova, ou vai para a existente.</summary>
    private void AddTab(string path)
    {
        var existing = _tabs.FirstOrDefault(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            ActivateTab(existing, resize: false, animate: true);
            return;
        }

        var tab = new HubTab { Path = path };
        tab.Count = FolderScanner.CountSupported(path);
        _tabs.Add(tab);

        TabRow.Visibility = !App.Config.SingleFolder && _tabs.Count > 1
            ? Visibility.Visible
            : Visibility.Collapsed;
        PersistTabs();

        ActivateTab(tab, resize: true, animate: true);
        Log.Info($"aba adicionada: {path}");
    }

    private void RemoveTab(HubTab tab)
    {
        if (_tabs.Count < 2) return;   // sempre sobra uma

        int index = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        TabRow.Visibility = !App.Config.SingleFolder && _tabs.Count > 1
            ? Visibility.Visible
            : Visibility.Collapsed;
        PersistTabs();

        if (ReferenceEquals(tab, _tab))
        {
            ActivateTab(_tabs[Math.Clamp(index, 0, _tabs.Count - 1)], resize: true, animate: true);
        }
    }

    private void PersistTabs()
    {
        App.Config.Tabs = [.. _tabs.Select(t => new TabConfig { Path = t.Path, Name = t.CustomName })];
        App.Config.Save();
    }

    // ------------------------------------------------------------ menu da aba

    private void TabItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not HubTab tab) return;
        if (FindResource("TabMenu") is not ContextMenu menu) return;

        _menuTab = tab;
        foreach (var entry in menu.Items.OfType<MenuItem>())
        {
            if (entry.Tag as string == "remove") entry.IsEnabled = _tabs.Count > 1;
        }

        PlaceAtCursor(menu);
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void RemoveTab_Click(object sender, RoutedEventArgs e)
    {
        if (_menuTab is { } tab) RemoveTab(tab);
    }

    private void RevealTab_Click(object sender, RoutedEventArgs e)
    {
        if (_menuTab is { } tab) Launcher.OpenFolder(tab.Path);
    }

    private void AddTab_Click(object sender, RoutedEventArgs e)
    {
        _suppressBlurClose = true;
        try
        {
            string? folder = App.AskForFolder(_folder);
            if (folder is not null) AddTab(folder);
        }
        finally
        {
            _suppressBlurClose = false;
            Activate();
        }
    }
}
