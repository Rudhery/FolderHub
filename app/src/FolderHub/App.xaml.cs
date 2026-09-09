using System.IO;
using System.Windows;
using System.Windows.Threading;
using FolderHub.Models;
using FolderHub.Services;

namespace FolderHub;

public partial class App : Application
{
    public static HubConfig Config { get; private set; } = new();

    /// <summary>Modo residente nesta execução: bandeja + atalho global.</summary>
    public static bool Background { get; set; }

    /// <summary>Nasce escondido (é assim que a entrada de inicialização chama).</summary>
    public static bool StartHidden { get; private set; }

    private static FileSystemWatcher? _configWatcher;
    private static DispatcherTimer? _configSettle;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "FolderHub", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(1);
        };

        Config = HubConfig.Load();
        ApplyTheme(Config.ThemeFile);

        // Argumento vence a config: permite vários hubs, um atalho por pasta.
        //   FolderHub.exe "D:\Jogos"     abre aquele hub
        //   FolderHub.exe --resident     fica residente e aparece
        //   FolderHub.exe --background   fica residente sem aparecer (entrada de logon)
        string? folder = null;
        bool background = Config.Background;
        bool askedForResident = false;

        foreach (string arg in e.Args)
        {
            switch (arg)
            {
                case "--background" or "-b":
                    background = true;
                    askedForResident = true;
                    StartHidden = true;
                    continue;

                case "--resident":
                    background = true;
                    askedForResident = true;
                    continue;

                case "--foreground":
                    background = false;
                    continue;
            }

            if (folder is null && Directory.Exists(arg)) folder = Path.GetFullPath(arg);
        }

        // Residente é um processo só: é ele que segura o atalho global. Quem
        // chegar depois (clique no ícone fixado) apenas pede para ele aparecer.
        if (background && !SingleInstance.TryAcquire())
        {
            SingleInstance.RequestShow(folder);
            Shutdown();
            return;
        }

        Background = background;
        ShutdownMode = background ? ShutdownMode.OnExplicitShutdown : ShutdownMode.OnMainWindowClose;

        // Quem pediu modo residente uma vez quer ele sempre: sem gravar isso, abrir
        // pelo menu Iniciar voltava a ser não-residente e o atalho global sumia.
        if (askedForResident && !Config.Background)
        {
            Config.Background = true;
            Config.Save();
        }

        // Pasta no argumento abre sozinha, como uma sessão avulsa. Sem argumento,
        // o hub é o conjunto de abas da configuração.
        List<HubTab> tabs = folder is not null
            ? [new HubTab { Path = folder }]
            : [.. Config.Tabs
                    .Where(t => Directory.Exists(t.Path))
                    .Select(t => new HubTab { Path = t.Path, CustomName = t.Name })];

        if (tabs.Count == 0)
        {
            string? chosen = AskForFolder();
            if (chosen == null)
            {
                Shutdown();
                return;
            }

            tabs.Add(new HubTab { Path = chosen });
            Config.Tabs = [new TabConfig { Path = chosen }];
            Config.Save();
        }

        WatchConfigFile();

        var window = new MainWindow(tabs);
        MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// A config também é observada em disco: o próprio app oferece "abrir a
    /// configuração" para edição à mão, e sem isso essas mudanças só valeriam na
    /// próxima abertura. Mesmo espírito de vigiar a pasta dos atalhos.
    /// </summary>
    private static void WatchConfigFile()
    {
        try
        {
            Directory.CreateDirectory(HubConfig.Directory);

            _configWatcher = new FileSystemWatcher(HubConfig.Directory, "config.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            // Uma gravação dispara vários eventos, e ler no meio dela falha o
            // parse. O timer junta a rajada e lê depois que o arquivo assentou.
            _configSettle = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _configSettle.Tick += (_, _) =>
            {
                _configSettle!.Stop();
                ReloadConfigFromDisk();
            };

            _configWatcher.Changed += (_, _) => Current?.Dispatcher.BeginInvoke(() =>
            {
                _configSettle!.Stop();
                _configSettle.Start();
            });
        }
        catch (Exception error)
        {
            Log.Warn("não consegui observar o arquivo de configuração", error);
        }
    }

    private static void ReloadConfigFromDisk()
    {
        // Não conseguiu ler? Desiste. Trocar a config por padrões aqui apagaria
        // as abas de quem está com o hub aberto.
        if (!HubConfig.TryLoad(out var fresh, out var error))
        {
            if (error is not null) Log.Warn("config no disco ilegível, mantendo a que está em uso", error);
            return;
        }

        fresh.Migrate();

        // Gravar dispara o vigia de volta; comparando, a própria gravação do app
        // não vira um segundo evento.
        if (fresh.ToJson() == Config.ToJson()) return;

        Config = fresh;
        HubConfig.NotifyChanged();
        Log.Info("configuração recarregada do disco");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _configSettle?.Stop();
        _configWatcher?.Dispose();
        if (Background) SingleInstance.ForgetWindow();
        SingleInstance.Release();
        base.OnExit(e);
    }

    /// <summary>
    /// Tema externo. Entra depois do tema embutido, então sobrescreve o que quiser
    /// — o WPF procura os dicionários mesclados de trás para a frente.
    /// O arquivo é apontado pelo próprio usuário na config dele, então vale a
    /// mesma confiança da configuração — XAML pode instanciar tipos.
    /// </summary>
    private static void ApplyTheme(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            if (!File.Exists(path))
            {
                Log.Warn($"tema não encontrado: {path}");
                return;
            }

            using var stream = File.OpenRead(path);
            if (System.Windows.Markup.XamlReader.Load(stream) is ResourceDictionary theme)
            {
                Current.Resources.MergedDictionaries.Add(theme);
                Log.Info($"tema aplicado: {path}");
            }
        }
        catch (Exception error)
        {
            Log.Warn($"tema inválido, seguindo com o padrão: {path}", error);
        }
    }

    public static string? AskForFolder(string? initial = null)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Escolha a pasta que vira o seu hub",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
        {
            dialog.InitialDirectory = initial;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
