using System.Windows.Media;
using FolderHub.Models;

namespace FolderHub.Services;

/// <summary>
/// Extrai os ícones fora da thread da interface.
///
/// É uma STA dedicada, e não o pool de threads, porque o COM do Shell é
/// apartment-threaded — no pool cada chamada atravessaria um proxy. Assim a
/// janela abre na hora e os ícones vão entrando conforme ficam prontos.
/// </summary>
public static class IconLoadQueue
{
    /// <param name="onLoaded">
    /// Chamado na thread de carga, não na da interface: quem chama marshala.
    /// </param>
    public static void Start(
        IReadOnlyList<AppItem> items,
        string label,
        Action<AppItem, ImageSource> onLoaded,
        CancellationToken token)
    {
        if (items.Count == 0) return;

        Log.Info($"extraindo {items.Count} ícone(s) de {label}");

        var thread = new Thread(() =>
        {
            foreach (var item in items)
            {
                if (token.IsCancellationRequested) return;

                var icon = IconLoader.Load(item.Path);
                if (icon is null)
                {
                    Log.Warn($"sem ícone para {item.Path}");
                    continue;
                }

                onLoaded(item, icon);
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = "FolderHub.Icons"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }
}
