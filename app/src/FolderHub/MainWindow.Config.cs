using FolderHub.Models;
using FolderHub.Services;

namespace FolderHub;

/// <summary>
/// Reagir a mudanças na configuração.
///
/// Sem isto, uma tela de configuração seria decorativa: mexer no atalho global
/// ou no número de colunas só valeria na próxima abertura do app. Aqui o hub
/// escuta <see cref="HubConfig.Changed"/> e re-sincroniza o que dá para trocar
/// quente.
///
/// O método é idempotente de propósito — compara com o que já está aplicado e
/// só age na diferença. É o que impede laço quando o próprio hub grava a
/// config (ao arrastar uma aba, por exemplo) e recebe o próprio aviso de volta.
/// </summary>
public partial class MainWindow
{
    private string _appliedHotKey = string.Empty;
    private SortMode _appliedSort;
    private int _appliedColumns;
    private bool _appliedBackground;

    private void WatchConfig()
    {
        RememberApplied();
        HubConfig.Changed += OnConfigChanged;
    }

    private void StopWatchingConfig() => HubConfig.Changed -= OnConfigChanged;

    private void RememberApplied()
    {
        _appliedHotKey = App.Config.HotKey ?? string.Empty;
        _appliedSort = App.Config.Sort;
        _appliedColumns = App.Config.MaxColumns;
        _appliedBackground = App.Background;
    }

    private void OnConfigChanged()
    {
        // O aviso pode vir de qualquer thread; a janela só se mexe na dela.
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(OnConfigChanged);
            return;
        }

        ApplyConfig();
    }

    private void ApplyConfig()
    {
        var config = App.Config;

        if (config.Background != _appliedBackground)
        {
            App.Background = config.Background;

            if (config.Background)
            {
                SingleInstance.TryAcquire();
                EnableResidentMode();
            }
            else
            {
                DisableResidentMode();
            }
        }

        if (App.Background &&
            !string.Equals(config.HotKey, _appliedHotKey, StringComparison.OrdinalIgnoreCase))
        {
            RegisterHotKey();
        }

        bool tabsChanged = TabsDiffer(config);
        bool sortChanged = config.Sort != _appliedSort;
        bool columnsChanged = config.MaxColumns != _appliedColumns;

        if (tabsChanged)
        {
            RebuildTabsFromConfig(config);       // já recarrega e redimensiona
        }
        else if (sortChanged)
        {
            Reload(resize: columnsChanged, animate: true);
        }
        else if (columnsChanged)
        {
            ResizeToContent(_all.Count);
        }

        RememberApplied();
    }

    /// <summary>A lista de abas da config é outra do que está na tela?</summary>
    private bool TabsDiffer(HubConfig config)
    {
        var wanted = config.Tabs;
        if (wanted.Count != _tabs.Count) return true;

        for (int i = 0; i < wanted.Count; i++)
        {
            if (!string.Equals(wanted[i].Path, _tabs[i].Path, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.Equals(wanted[i].Name ?? string.Empty, _tabs[i].CustomName ?? string.Empty, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private void RebuildTabsFromConfig(HubConfig config)
    {
        string? current = _tab?.Path;

        var rebuilt = config.Tabs
            .Where(t => !string.IsNullOrWhiteSpace(t.Path))
            .Select(t => new HubTab { Path = t.Path, CustomName = t.Name })
            .ToList();

        if (rebuilt.Count == 0) return;   // um hub sem nenhuma pasta não existe

        BuildTabs(rebuilt);

        // volta para a aba que estava aberta, se ela sobreviveu
        var keep = rebuilt.FirstOrDefault(t => string.Equals(t.Path, current, StringComparison.OrdinalIgnoreCase))
                   ?? rebuilt[0];

        ActivateTab(keep, resize: true, animate: true);
    }
}
