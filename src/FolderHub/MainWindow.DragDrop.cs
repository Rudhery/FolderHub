using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using FolderHub.Services;

namespace FolderHub;

/// <summary>
/// Soltar coisas na janela: uma pasta vira aba, arquivos entram na pasta da aba
/// ativa. O arraste interno de reordenar é filtrado pelo formato próprio.
/// </summary>
public partial class MainWindow
{
    // ----------------------------------------------------------- drag & drop

    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        ShowDropOverlay(e);
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        ShowDropOverlay(e);
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        base.OnDragLeave(e);
        HideDropOverlay();
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        if (e.Data.GetDataPresent(DragFormat)) return;

        HideDropOverlay();

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;

        // Pasta solta na janela vira aba nova (ou vai para a que já existe).
        if (paths.Length == 1 && Directory.Exists(paths[0]))
        {
            AddTab(paths[0]);
            return;
        }

        int added = ShortcutWriter.AddToFolder(paths, _folder);
        if (added > 0) Reload(resize: true);
    }

    private void ShowDropOverlay(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DragFormat))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        bool isFolder = paths.Length == 1 && Directory.Exists(paths[0]);
        DropText.Text = isFolder ? "Solte para abrir esta pasta em uma aba" : "Solte para adicionar ao hub";
        e.Effects = isFolder ? DragDropEffects.Link : DragDropEffects.Copy;
        e.Handled = true;

        if (DropOverlay.Visibility == Visibility.Visible) return;

        DropOverlay.Visibility = Visibility.Visible;
        DropOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
    }

    private void HideDropOverlay()
    {
        if (DropOverlay.Visibility != Visibility.Visible) return;

        var fade = new DoubleAnimation(DropOverlay.Opacity, 0, TimeSpan.FromMilliseconds(140));
        fade.Completed += (_, _) => DropOverlay.Visibility = Visibility.Collapsed;
        DropOverlay.BeginAnimation(OpacityProperty, fade);
    }
}
