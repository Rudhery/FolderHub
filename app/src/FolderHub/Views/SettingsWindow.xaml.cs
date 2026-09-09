using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FolderHub.Controls;
using FolderHub.Models;
using FolderHub.Services;

namespace FolderHub.Views;

/// <summary>Uma seção da navegação lateral.</summary>
public sealed record SettingsSection(string Name, HubGlyph Glyph);

/// <summary>Uma linha da tabela de teclas.</summary>
public sealed record KeyHint(string Keys, string What);

/// <summary>
/// A tela de configuração.
///
/// Ela não guarda estado próprio: edita <see cref="App.Config"/>, que é a mesma
/// instância que o app inteiro usa, e chama <c>Save()</c>. Gravar dispara
/// <see cref="HubConfig.Changed"/>, que o hub já sabe aplicar quente — trocar o
/// atalho global, refazer as abas, redimensionar. Não existe botão de OK: cada
/// ajuste vale no instante em que é feito.
///
/// Por isso também escuta o mesmo evento. A configuração pode mudar por fora —
/// arrastando uma aba no hub, ou editando o JSON à mão — e a tela precisa
/// acompanhar em vez de mostrar um retrato velho.
/// </summary>
public partial class SettingsWindow : HubWindow
{
    /// <summary>
    /// O limite do desenho. Passar disso é possível editando o JSON, e o hub
    /// aguenta; a tela é que deixa de ser legível.
    /// </summary>
    public const int MaxHubs = 8;

    private readonly ObservableCollection<HubTab> _hubs = [];

    /// <summary>
    /// Está gravando agora. Sem isto, o próprio <c>Save()</c> voltaria pelo
    /// evento e recarregaria a tela no meio da edição — perdendo o foco de quem
    /// estivesse digitando o nome de um hub.
    /// </summary>
    private bool _saving;

    private bool _loading;

    public SettingsWindow()
    {
        InitializeComponent();

        HubList.ItemsSource = _hubs;

        Sections.ItemsSource = new[]
        {
            new SettingsSection("Hubs", HubGlyph.Grid),
            new SettingsSection("Geral", HubGlyph.Settings),
            new SettingsSection("Atalhos", HubGlyph.Keyboard),
            new SettingsSection("Aparência", HubGlyph.Appearance),
            new SettingsSection("Sobre", HubGlyph.Info)
        };
        Sections.SelectedIndex = 0;

        KeyReference.ItemsSource = new[]
        {
            new KeyHint("digite", "filtra os cards"),
            new KeyHint("↑ ↓ ← →", "anda pelos cards"),
            new KeyHint("Enter", "abre o card escolhido"),
            new KeyHint("/", "volta o foco para a busca"),
            new KeyHint("Tab", "troca de hub"),
            new KeyHint("Ctrl+1…9", "vai direto para um hub"),
            new KeyHint("Ctrl+O", "troca a pasta do hub aberto"),
            new KeyHint("F5", "relê a pasta e os ícones"),
            new KeyHint("Esc", "limpa a busca, ou fecha")
        };

        SortSelect.ItemsSource = new[]
        {
            "Manual — a ordem da pasta",
            "Nome (A → Z)",
            "Nome (Z → A)",
            "Mais recentes"
        };

        Load();
        HubConfig.Changed += OnConfigChanged;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        FadeIn();
    }

    protected override void OnClosed(EventArgs e)
    {
        HubConfig.Changed -= OnConfigChanged;
        base.OnClosed(e);
    }

    // ------------------------------------------------------------------ carga

    private void OnConfigChanged()
    {
        // A própria gravação volta por aqui. Recarregar agora só tiraria o foco
        // de quem está editando.
        if (_saving) return;

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(OnConfigChanged);
            return;
        }

        Load();
    }

    private void Load()
    {
        _loading = true;
        try
        {
            var config = App.Config;

            _hubs.Clear();
            foreach (var tab in config.Tabs)
            {
                _hubs.Add(new HubTab { Path = tab.Path, CustomName = tab.Name });
            }

            UpdateHubCount();

            StartupToggle.IsChecked = StartupRegistration.IsEnabled;
            ResidentToggle.IsChecked = config.Background;
            CloseAfterToggle.IsChecked = config.CloseAfterLaunch;
            CloseOnBlurToggle.IsChecked = config.CloseOnBlur;

            HotKeyBox.HotKey = config.HotKey ?? string.Empty;
            UpdateHotKeyWarning();

            SortSelect.SelectedIndex = config.Sort switch
            {
                SortMode.NameAsc => 1,
                SortMode.NameDesc => 2,
                SortMode.Recent => 3,
                _ => 0
            };

            ColumnsStepper.Value = Math.Clamp(config.MaxColumns, 3, 10);

            UpdateThemePath();

            VersionText.Text = $"versão {App.Version}";
            ConfigPath.Text = HubConfig.FilePath;
            StatusText.Text = PathDisplay.Shorten(HubConfig.Directory);
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Grava a config e deixa o hub reagir, sem que o aviso volte para cá.</summary>
    private void Commit()
    {
        _saving = true;
        try { App.Config.Save(); }
        finally { _saving = false; }
    }

    // --------------------------------------------------------------- navegação

    private void Sections_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int index = Sections.SelectedIndex;
        if (index < 0) return;

        PageHubs.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageGeneral.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageKeys.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        PageLook.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragFrom(e);

    private void Close_Click(object sender, RoutedEventArgs e) => Dismiss();

    // -------------------------------------------------------------------- hubs

    private static HubTab? TabOf(object sender)
        => (sender as FrameworkElement)?.Tag as HubTab;

    private void UpdateHubCount()
    {
        AddHub.IsEnabled = _hubs.Count < MaxHubs;

        HubCountHint.Text = _hubs.Count >= MaxHubs
            ? $"{MaxHubs} é o limite — mais que isso a faixa de abas deixa de caber."
            : $"{_hubs.Count} de {MaxHubs}";
    }

    /// <summary>Passa a lista da tela para a configuração e grava.</summary>
    private void SaveHubs()
    {
        App.Config.Tabs = [.. _hubs.Select(h => new TabConfig
        {
            Path = h.Path,
            Name = string.IsNullOrWhiteSpace(h.CustomName) ? null : h.CustomName
        })];

        UpdateHubCount();
        Commit();
    }

    private void AddHub_Click(object sender, RoutedEventArgs e)
    {
        if (_hubs.Count >= MaxHubs) return;

        string? folder = App.AskForFolder(_hubs.LastOrDefault()?.Path);
        if (folder is null) return;

        if (_hubs.Any(h => string.Equals(h.Path, folder, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText.Text = "essa pasta já é um hub";
            return;
        }

        _hubs.Add(new HubTab { Path = folder });
        SaveHubs();
        Activate();
    }

    private void HubRemove_Click(object sender, RoutedEventArgs e)
    {
        if (TabOf(sender) is not { } tab) return;

        // Um hub sem nenhuma pasta não existe: o app não teria o que mostrar.
        if (_hubs.Count <= 1)
        {
            StatusText.Text = "o último hub não pode sair";
            return;
        }

        _hubs.Remove(tab);
        SaveHubs();
    }

    private void HubUp_Click(object sender, RoutedEventArgs e) => Move(TabOf(sender), -1);

    private void HubDown_Click(object sender, RoutedEventArgs e) => Move(TabOf(sender), 1);

    private void Move(HubTab? tab, int delta)
    {
        if (tab is null) return;

        int from = _hubs.IndexOf(tab);
        int to = from + delta;
        if (from < 0 || to < 0 || to >= _hubs.Count) return;

        _hubs.Move(from, to);
        SaveHubs();
    }

    private void HubReveal_Click(object sender, RoutedEventArgs e)
    {
        if (TabOf(sender) is { } tab) Launcher.OpenFolder(tab.Path);
    }

    private void HubName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        e.Handled = true;
        CommitName(sender as TextBox);
        Keyboard.ClearFocus();
    }

    private void HubName_LostFocus(object sender, RoutedEventArgs e) => CommitName(sender as TextBox);

    private void CommitName(TextBox? box)
    {
        if (box?.Tag is not HubTab tab) return;

        string typed = box.Text.Trim();

        // Vazio, ou igual ao nome da pasta, volta a ser "sem apelido": assim
        // renomear a pasta no Explorer continua refletindo na aba.
        string? wanted = typed.Length == 0 || string.Equals(typed, tab.FolderName, StringComparison.Ordinal)
            ? null
            : typed;

        if (string.Equals(wanted ?? string.Empty, tab.CustomName ?? string.Empty, StringComparison.Ordinal))
        {
            box.Text = tab.Name;
            return;
        }

        tab.CustomName = wanted;
        box.Text = tab.Name;
        SaveHubs();
    }

    // ------------------------------------------------------------------- geral

    private void Startup_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        bool wanted = StartupToggle.IsChecked == true;

        // Isto é registro do Windows, não configuração do app — pode falhar por
        // política da máquina, e nesse caso a chave volta para o que ela é.
        if (!StartupRegistration.Set(wanted))
        {
            StatusText.Text = "não consegui escrever na chave de inicialização";
        }

        StartupToggle.IsChecked = StartupRegistration.IsEnabled;
    }

    private void Resident_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        App.Config.Background = ResidentToggle.IsChecked == true;
        Commit();
        UpdateHotKeyWarning();
    }

    private void CloseAfter_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        App.Config.CloseAfterLaunch = CloseAfterToggle.IsChecked == true;
        Commit();
    }

    private void CloseOnBlur_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        App.Config.CloseOnBlur = CloseOnBlurToggle.IsChecked == true;
        Commit();
    }

    // ----------------------------------------------------------------- atalhos

    private void HotKey_Changed(object? sender, EventArgs e)
    {
        if (_loading) return;

        App.Config.HotKey = HotKeyBox.HotKey;
        Commit();
        UpdateHotKeyWarning();
    }

    /// <summary>
    /// Diz por que o atalho pode não estar valendo. São duas causas diferentes e
    /// nenhuma delas é visível: sem modo residente não há processo escutando, e
    /// uma combinação já tomada por outro app é recusada pelo Windows.
    /// </summary>
    private void UpdateHotKeyWarning()
    {
        string? warning = null;

        if (string.IsNullOrWhiteSpace(App.Config.HotKey))
        {
            warning = "Sem atalho definido.";
        }
        else if (!App.Config.Background)
        {
            warning = "O atalho só funciona com o hub residente na bandeja — ligue em Geral.";
        }
        else if (Application.Current.MainWindow is MainWindow hub && hub.HotKeyFailed)
        {
            warning = $"O Windows recusou {App.Config.HotKey}: outro programa já usa essa combinação.";
        }

        HotKeyWarning.Text = warning ?? string.Empty;
        HotKeyWarning.Visibility = warning is null ? Visibility.Collapsed : Visibility.Visible;
    }

    // --------------------------------------------------------------- aparência

    private void Sort_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;

        App.Config.Sort = SortSelect.SelectedIndex switch
        {
            1 => SortMode.NameAsc,
            2 => SortMode.NameDesc,
            3 => SortMode.Recent,
            _ => SortMode.Manual
        };

        Commit();
    }

    private void Columns_Changed(object? sender, EventArgs e)
    {
        if (_loading) return;

        App.Config.MaxColumns = ColumnsStepper.Value;
        Commit();
    }

    private void PickTheme_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Escolha um arquivo de tema",
            Filter = "Tema do FolderHub (*.xaml)|*.xaml",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true) return;

        App.Config.ThemeFile = dialog.FileName;
        Commit();
        UpdateThemePath();
        Activate();
    }

    private void ClearTheme_Click(object sender, RoutedEventArgs e)
    {
        App.Config.ThemeFile = null;
        Commit();
        UpdateThemePath();
    }

    private void UpdateThemePath()
    {
        string? file = App.Config.ThemeFile;
        bool has = !string.IsNullOrWhiteSpace(file);

        ClearTheme.IsEnabled = has;

        ThemePath.Text = has
            // O tema entra na abertura: trocar aqui não repinta o que já está na tela.
            ? $"{file}  ·  vale na próxima abertura"
            : "nenhum — usando o tema embutido";
    }

    // ------------------------------------------------------------------- sobre

    private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
    {
        // Garante que o arquivo existe antes de abrir a pasta, senão quem nunca
        // gravou encontra uma pasta vazia.
        if (!File.Exists(HubConfig.FilePath)) App.Config.Save();

        Launcher.OpenFolder(HubConfig.Directory);
    }
}
