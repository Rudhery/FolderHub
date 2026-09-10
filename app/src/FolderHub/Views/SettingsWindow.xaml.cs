using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FolderHub.Controls;
using FolderHub.Models;
using FolderHub.Services;

namespace FolderHub.Views;

/// <summary>Uma seção da navegação lateral, com o que o cabeçalho mostra ao abri-la.</summary>
public sealed record SettingsSection(string Name, string Title, string Subtitle);

/// <summary>Uma tecla do atalho global. <c>Joiner</c> desenha o "+" antes dela.</summary>
public sealed record KeyPart(string Key, bool Joiner);

/// <summary>
/// A tela de configuração.
///
/// Ela não guarda estado próprio: edita <see cref="App.Config"/>, que é a mesma
/// instância que o app inteiro usa, e chama <c>Save()</c>. Gravar dispara
/// <see cref="HubConfig.Changed"/>, que o hub já sabe aplicar quente. Não há
/// botão de OK — "Concluir" só fecha, porque tudo já valeu.
///
/// Por isso também escuta o mesmo evento: a configuração pode mudar por fora,
/// arrastando uma aba no hub ou editando o JSON à mão, e a tela precisa
/// acompanhar em vez de mostrar um retrato velho.
/// </summary>
public partial class SettingsWindow : HubWindow
{
    /// <summary>
    /// O limite do desenho. Passar disso é possível editando o JSON, e o hub
    /// aguenta; a faixa de abas é que deixa de caber.
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

    /// <summary>Esperando a combinação do atalho global.</summary>
    private bool _recording;

    private Point _dragStart;
    private HubTab? _dragging;

    /// <summary>
    /// Sem acrílico de propósito — veja <see cref="HubWindow"/>. A superfície
    /// dela é quase opaca, e o canto de 22px do design só fecha certo quando a
    /// forma é desenhada pelo conteúdo.
    /// </summary>
    public SettingsWindow() : base(acrylic: true)
    {
        _loading = true;
        InitializeComponent();

        HubList.ItemsSource = _hubs;

        Sections.ItemsSource = new[]
        {
            new SettingsSection("Hubs", "Hubs", "Como o Folder Hub monta os cards de atalho"),
            new SettingsSection("Geral", "Geral", "Comportamento do aplicativo no Windows"),
            new SettingsSection("Atalhos", "Atalhos", "Teclas para abrir o hub e navegar entre os cards"),
            new SettingsSection("Aparência", "Aparência", "Tema, densidade e transparência do modal"),
            new SettingsSection("Sobre", "Sobre", "Versão, licença e onde reportar problemas")
        };
        Sections.SelectedIndex = 0;

        SortSelect.ItemsSource = new[]
        {
            "Manual — a ordem da pasta",
            "Nome (A → Z)",
            "Nome (Z → A)",
            "Mais recentes"
        };

        LanguageSelect.ItemsSource = new[] { "Português (Brasil)" };
        LanguageSelect.SelectedIndex = 0;

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
                _hubs.Add(new HubTab
                {
                    Path = tab.Path,
                    CustomName = tab.Name,
                    Count = FolderScanner.CountSupported(tab.Path)
                });
            }

            RenumberHubs();

            ModeSingle.IsChecked = config.SingleFolder;
            ModeMulti.IsChecked = !config.SingleFolder;

            StartupToggle.IsChecked = StartupRegistration.IsEnabled;
            ResidentToggle.IsChecked = config.Background;
            CloseAfterToggle.IsChecked = config.CloseAfterLaunch;
            CloseOnBlurToggle.IsChecked = config.CloseOnBlur;
            LastHubToggle.IsChecked = config.RememberLastHub;
            TabSwitchToggle.IsChecked = config.TabSwitchesHub;

            ShowPathToggle.IsChecked = config.ShowPath;
            ShowCountToggle.IsChecked = config.ShowCount;
            ReduceMotionToggle.IsChecked = config.ReduceMotion;

            ShowHotKey(config.HotKey ?? string.Empty);
            UpdateHotKeyWarning();

            SortSelect.SelectedIndex = config.Sort switch
            {
                SortMode.NameAsc => 1,
                SortMode.NameDesc => 2,
                SortMode.Recent => 3,
                _ => 0
            };

            DensitySegment.SelectedIndex = config.Density switch
            {
                CardDensity.Compact => 0,
                CardDensity.Large => 2,
                _ => 1
            };

            ColumnsStepper.Value = Math.Clamp(config.MaxColumns, 3, 10);

            TransparencySlider.Value = Math.Clamp(config.Transparency, 0, 90);
            TransparencyValue.Text = $"{(int)TransparencySlider.Value}%";

            UpdateThemePath();
            ThemeDark.IsChecked = config.ThemeMode == FolderHub.Services.ThemeMode.Dark;
            ThemeLight.IsChecked = config.ThemeMode == FolderHub.Services.ThemeMode.Light;
            UpdateModeDependents();

            SidebarVersion.Text = $"v{App.Version}";
            VersionText.Text = $"v{App.Version} · .NET 10";
            ConfigPath.Text = HubConfig.FilePath;
            InstallPath.Text = Path.GetDirectoryName(Environment.ProcessPath) ?? "—";
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

    private void Say(string message) => StatusText.Text = message;

    private void SayIdle() => StatusText.Text = "alterações salvas automaticamente";

    // --------------------------------------------------------------- navegação

    private void Sections_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Sections.SelectedItem is not SettingsSection section) return;

        PageTitle.Text = section.Title;
        PageSubtitle.Text = section.Subtitle;

        int index = Sections.SelectedIndex;
        PageHubs.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageGeneral.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageKeys.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        PageLook.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;

        // Sair da aba de atalhos no meio de uma gravação deixaria a tela
        // esperando uma tecla que ninguém mais vai apertar.
        if (index != 2) StopRecording();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragFrom(e);

    private void Close_Click(object sender, RoutedEventArgs e) => Dismiss();

    // -------------------------------------------------------------------- hubs

    private static HubTab? TabOf(object sender) => (sender as FrameworkElement)?.Tag as HubTab;

    private void RenumberHubs()
    {
        for (int i = 0; i < _hubs.Count; i++) _hubs[i].Slot = $"Tab {i + 1}";

        AddHub.IsEnabled = _hubs.Count < MaxHubs;
        HubsGroup.Aside = $"{_hubs.Count} de {MaxHubs}";
    }

    /// <summary>Passa a lista da tela para a configuração e grava.</summary>
    private void SaveHubs()
    {
        App.Config.Tabs = [.. _hubs.Select(h => new TabConfig
        {
            Path = h.Path,
            Name = string.IsNullOrWhiteSpace(h.CustomName) ? null : h.CustomName
        })];

        RenumberHubs();
        Commit();
    }

    private void AddHub_Click(object sender, RoutedEventArgs e)
    {
        if (_hubs.Count >= MaxHubs) return;

        string? folder = App.AskForFolder(_hubs.LastOrDefault()?.Path);
        if (folder is null) return;

        if (_hubs.Any(h => string.Equals(h.Path, folder, StringComparison.OrdinalIgnoreCase)))
        {
            Say("essa pasta já é um hub");
            return;
        }

        _hubs.Add(new HubTab { Path = folder, Count = FolderScanner.CountSupported(folder) });
        SaveHubs();
        SayIdle();
        Activate();
    }

    private void HubRemove_Click(object sender, RoutedEventArgs e)
    {
        if (TabOf(sender) is not { } tab) return;

        // Um hub sem nenhuma pasta não existe: o app não teria o que mostrar.
        if (_hubs.Count <= 1)
        {
            Say("o último hub não pode sair");
            return;
        }

        _hubs.Remove(tab);
        SaveHubs();
        SayIdle();
    }

    // ------------------------------------------------------- arrastar e ordenar

    private void Grip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragging = TabOf(sender);
    }

    private void Grip_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging is null || e.LeftButton != MouseButtonState.Pressed) return;

        // Um tremor de mão não deve virar arrasto.
        var moved = e.GetPosition(this) - _dragStart;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var carried = _dragging;
        _dragging = null;

        DragDrop.DoDragDrop((DependencyObject)sender, carried, DragDropEffects.Move);
    }

    private void HubRow_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(HubTab)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void HubRow_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(HubTab)) is not HubTab carried) return;
        if (TabOf(sender) is not { } target || ReferenceEquals(carried, target)) return;

        int from = _hubs.IndexOf(carried);
        int to = _hubs.IndexOf(target);
        if (from < 0 || to < 0) return;

        _hubs.Move(from, to);
        SaveHubs();
        Say($"{carried.Name} agora é a aba {to + 1}");
        e.Handled = true;
    }

    // ------------------------------------------------------------ nome do hub

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

    // --------------------------------------------------------- modo de trabalho

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        App.Config.SingleFolder = ModeSingle.IsChecked == true;
        Commit();
        UpdateModeDependents();
    }

    /// <summary>
    /// Os ajustes que só fazem sentido com vários hubs apagam no modo pasta
    /// única, em vez de ficarem ligados sem efeito nenhum.
    /// </summary>
    private void UpdateModeDependents()
    {
        bool multi = !App.Config.SingleFolder;

        LastHubRow.IsEnabled = multi;
        TabSwitchRow.IsEnabled = multi;
    }

    // ------------------------------------------------------------------- geral

    private void Startup_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        // Isto é registro do Windows, não configuração do app — pode falhar por
        // política da máquina, e nesse caso a chave volta para o que ela é.
        if (!StartupRegistration.Set(StartupToggle.IsChecked == true))
        {
            Say("não consegui escrever na chave de inicialização");
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

    private void CloseAfter_Click(object sender, RoutedEventArgs e) => Flip(c => c.CloseAfterLaunch = CloseAfterToggle.IsChecked == true);

    private void CloseOnBlur_Click(object sender, RoutedEventArgs e) => Flip(c => c.CloseOnBlur = CloseOnBlurToggle.IsChecked == true);

    private void LastHub_Click(object sender, RoutedEventArgs e) => Flip(c => c.RememberLastHub = LastHubToggle.IsChecked == true);

    private void TabSwitch_Click(object sender, RoutedEventArgs e) => Flip(c => c.TabSwitchesHub = TabSwitchToggle.IsChecked == true);

    private void ShowPath_Click(object sender, RoutedEventArgs e) => Flip(c => c.ShowPath = ShowPathToggle.IsChecked == true);

    private void ShowCount_Click(object sender, RoutedEventArgs e) => Flip(c => c.ShowCount = ShowCountToggle.IsChecked == true);

    private void ReduceMotion_Click(object sender, RoutedEventArgs e) => Flip(c => c.ReduceMotion = ReduceMotionToggle.IsChecked == true);

    private void Flip(Action<HubConfig> change)
    {
        if (_loading) return;

        change(App.Config);
        Commit();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar a configuração",
            Filter = "Configuração do FolderHub (*.json)|*.json",
            FileName = "folderhub-config.json"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, App.Config.ToJson());
            Say("configuração exportada");
        }
        catch (Exception error)
        {
            Log.Warn("não consegui exportar a configuração", error);
            Say("não consegui gravar o arquivo");
        }

        Activate();
    }

    // ----------------------------------------------------------------- atalhos

    private void ShowHotKey(string spec)
    {
        var parts = spec.Split('+', StringSplitOptions.RemoveEmptyEntries)
            .Select((part, index) => new KeyPart(Pretty(part.Trim()), index > 0))
            .ToList();

        HotKeyParts.ItemsSource = parts;
        HotKeyParts.Visibility = parts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>O nome que a config guarda nem sempre é o que se lê numa tecla.</summary>
    private static string Pretty(string part) => part.ToLowerInvariant() switch
    {
        "space" => "Espaço",
        "enter" or "return" => "Enter",
        "escape" or "esc" => "Esc",
        _ => part
    };

    private void HotKeyChange_Click(object sender, RoutedEventArgs e)
    {
        if (_recording) StopRecording();
        else StartRecording();
    }

    private void StartRecording()
    {
        _recording = true;

        HotKeyParts.Visibility = Visibility.Collapsed;
        HotKeyPrompt.Visibility = Visibility.Visible;
        HotKeyButton.Visibility = Visibility.Collapsed;
        HotKeyRecording.Visibility = Visibility.Visible;

        HotKeyBox.Background = (System.Windows.Media.Brush)FindResource("SunkenBgStrong");
        HotKeyBox.BorderBrush = (System.Windows.Media.Brush)FindResource("SunkenBorderStrong");

        Focus();
    }

    private void StopRecording()
    {
        if (!_recording) return;

        _recording = false;

        HotKeyPrompt.Visibility = Visibility.Collapsed;
        HotKeyRecording.Visibility = Visibility.Collapsed;
        HotKeyButton.Visibility = Visibility.Visible;

        HotKeyBox.Background = (System.Windows.Media.Brush)FindResource("SunkenBg");
        HotKeyBox.BorderBrush = (System.Windows.Media.Brush)FindResource("SunkenBorder");

        ShowHotKey(App.Config.HotKey ?? string.Empty);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (!_recording) return;

        // A janela fecha no Esc; enquanto grava, o Esc é da gravação.
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            StopRecording();
            return;
        }

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (HotKeyText.IsModifier(key)) return;

        if (!HotKeyText.TryFormat(Keyboard.Modifiers, key, out string spec))
        {
            // O Windows recusa um atalho global de tecla solta: ele engoliria
            // aquela tecla no sistema inteiro.
            Say("o atalho precisa de Ctrl, Alt, Shift ou Win");
            return;
        }

        App.Config.HotKey = spec;
        Commit();

        StopRecording();
        UpdateHotKeyWarning();
        SayIdle();
    }

    /// <summary>
    /// Diz por que o atalho pode não estar valendo. São duas causas diferentes e
    /// nenhuma delas é visível: sem ícone na bandeja não há processo escutando,
    /// e uma combinação já tomada por outro app é recusada pelo Windows.
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
            warning = "O atalho só funciona com o ícone na bandeja ligado — está em Geral.";
        }
        else if (Application.Current.MainWindow is MainWindow hub && hub.HotKeyFailed)
        {
            warning = $"O Windows recusou {App.Config.HotKey}: outro programa já usa essa combinação.";
        }

        HotKeyWarning.Text = warning ?? string.Empty;
        HotKeyWarning.Visibility = warning is null ? Visibility.Collapsed : Visibility.Visible;
    }

    // --------------------------------------------------------------- aparência

    private void Sort_Changed(object sender, SelectionChangedEventArgs e) => Flip(c => c.Sort = SortSelect.SelectedIndex switch
    {
        1 => SortMode.NameAsc,
        2 => SortMode.NameDesc,
        3 => SortMode.Recent,
        _ => SortMode.Manual
    });

    private void Columns_Changed(object? sender, EventArgs e) => Flip(c => c.MaxColumns = ColumnsStepper.Value);

    private void Density_Changed(object sender, SelectionChangedEventArgs e) => Flip(c => c.Density = DensitySegment.SelectedIndex switch
    {
        0 => CardDensity.Compact,
        2 => CardDensity.Large,
        _ => CardDensity.Default
    });

    private void Transparency_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int value = (int)Math.Round(e.NewValue);
        TransparencyValue.Text = $"{value}%";

        Flip(c => c.Transparency = value);
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
        ThemePath.Text = has ? file : "nenhum";
    }

    public void OpenAppearance() => Sections.SelectedIndex = 3;

    private void Theme_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not RadioButton radio || radio.IsChecked != true) return;

        App.Config.ThemeMode = radio == ThemeLight ? FolderHub.Services.ThemeMode.Light : FolderHub.Services.ThemeMode.Dark;
        Commit();
        if (StatusText is not null) Say("tema salvo — feche e abra o Folder Hub para aplicar");
    }

    private void RestartTheme_Click(object sender, RoutedEventArgs e)
    {
        string? executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)) return;

        try
        {
            App.Config.Save();
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--foreground --appearance",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(executable)
            });
            Application.Current.Shutdown();
        }
        catch (Exception error)
        {
            Log.Warn("não consegui reiniciar para aplicar o tema", error);
            Say("não foi possível reiniciar automaticamente");
        }
    }

    // ------------------------------------------------------------------- sobre

    private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
    {
        // Garante que o arquivo existe antes de abrir a pasta, senão quem nunca
        // gravou encontra uma pasta vazia.
        if (!File.Exists(HubConfig.FilePath)) App.Config.Save();

        Launcher.OpenFolder(HubConfig.Directory);
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e) => Launcher.OpenFolder(HubConfig.Directory);

    private void Repo_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string url) return;

        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception error) { Log.Warn($"não consegui abrir {url}", error); }
    }

    // ------------------------------------------------------- restaurar padrões

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        // Os hubs NÃO voltam ao padrão: eles são o trabalho do usuário, não uma
        // preferência. Restaurar aqui apagaria pastas que ele levou tempo
        // montando, e não haveria como desfazer.
        var fresh = new HubConfig { Tabs = App.Config.Tabs, ThemeFile = App.Config.ThemeFile };

        App.Config.SingleFolder = fresh.SingleFolder;
        App.Config.CloseAfterLaunch = fresh.CloseAfterLaunch;
        App.Config.CloseOnBlur = fresh.CloseOnBlur;
        App.Config.RememberLastHub = fresh.RememberLastHub;
        App.Config.TabSwitchesHub = fresh.TabSwitchesHub;
        App.Config.ShowPath = fresh.ShowPath;
        App.Config.ShowCount = fresh.ShowCount;
        App.Config.ReduceMotion = fresh.ReduceMotion;
        App.Config.Density = fresh.Density;
        App.Config.Transparency = fresh.Transparency;
        App.Config.MaxColumns = fresh.MaxColumns;
        App.Config.Sort = fresh.Sort;
        App.Config.HotKey = fresh.HotKey;
        App.Config.ThemeMode = fresh.ThemeMode;

        Commit();
        Load();
        Say("padrões restaurados — seus hubs continuam aí");
    }
}
