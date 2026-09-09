using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FolderHub.Models;
using FolderHub.Services;

namespace FolderHub;

/// <summary>
/// Ordenação: o menu de modos e o arrastar-para-reordenar, que grava a ordem
/// renomeando os arquivos da pasta.
/// </summary>
public partial class MainWindow
{
    // O formato próprio separa o arraste interno do arraste externo (pasta ou
    // atalhos vindos do Explorer).
    private const string DragFormat = "FolderHub.Reorder";

    private Point _dragOrigin;
    private AppItem? _dragCandidate;
    private AppItem? _dragging;
    private bool _dragCompleted;

    // ----------------------------------------------------------- reordenar

    /// <summary>Só faz sentido arrastar com a ordem manual e a lista inteira à vista.</summary>
    private bool CanReorder => App.Config.Sort == SortMode.Manual && SearchBox.Text.Length == 0;

    private void Cards_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragCompleted = false;
        _dragOrigin = e.GetPosition(this);
        _dragCandidate = CanReorder ? ItemFrom(e.OriginalSource) : null;
    }

    private void Cards_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate is null || e.LeftButton != MouseButtonState.Pressed) return;

        var delta = e.GetPosition(this) - _dragOrigin;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _dragging = _dragCandidate;
        _dragCandidate = null;
        _animateIntro = false;
        Cards.SelectedItem = _dragging;

        try
        {
            DragDrop.DoDragDrop(Cards, new DataObject(DragFormat, _dragging.Path), DragDropEffects.Move);
        }
        finally
        {
            _dragging = null;
            _dragCompleted = true;
        }
    }

    private void Cards_DragOver(object sender, DragEventArgs e)
    {
        if (_dragging is null || !e.Data.GetDataPresent(DragFormat)) return;

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        // Reordena ao vivo: os cards se acomodam sob o cursor, sem adorner.
        if (ItemFrom(e.OriginalSource) is not { } target || ReferenceEquals(target, _dragging)) return;

        int from = _visible.IndexOf(_dragging);
        int to = _visible.IndexOf(target);
        if (from >= 0 && to >= 0 && from != to) _visible.Move(from, to);
    }

    private void Cards_Drop(object sender, DragEventArgs e)
    {
        if (_dragging is null || !e.Data.GetDataPresent(DragFormat)) return;
        e.Handled = true;

        // O caminho muda no disco, o nome não: é por ele que reencontramos o card.
        string moved = _dragging.Name;

        PersistOrder([.. _visible]);
        _dragging = null;

        // Reler é o que mantém caminhos, ordem e ícones coerentes.
        Reload(resize: false);

        int index = _visible.ToList().FindIndex(i => i.Name == moved);
        if (index >= 0) MoveTo(index);
    }

    /// <summary>Grava a ordem renomeando os arquivos com prefixo numérico.</summary>
    private void PersistOrder(IReadOnlyList<AppItem> ordered)
    {
        bool watching = _watcher?.EnableRaisingEvents ?? false;
        if (_watcher != null) _watcher.EnableRaisingEvents = false;

        try
        {
            foreach (var (from, to) in ManualOrder.Apply(ordered))
            {
                if (_iconCache.Remove(from, out var icon)) _iconCache[to] = icon;
            }
        }
        finally
        {
            _reloadDebounce.Stop();
            if (_watcher != null) _watcher.EnableRaisingEvents = watching;
        }
    }

    // ----------------------------------------------------------- menu de ordem

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is not { } menu) return;

        string current = App.Config.Sort.ToString();
        foreach (var entry in menu.Items.OfType<MenuItem>())
        {
            entry.IsChecked = entry.Tag as string == current;
        }

        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void SortMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem entry || entry.Tag is not string tag) return;
        if (!Enum.TryParse(tag, out SortMode mode) || mode == App.Config.Sort) return;

        App.Config.Sort = mode;
        App.Config.Save();
        Reload(resize: false, animate: true);
    }

    private void FreezeOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_all.Count == 0) return;

        PersistOrder(_all);
        App.Config.Sort = SortMode.Manual;
        App.Config.Save();
        Reload(resize: false);
    }
}
