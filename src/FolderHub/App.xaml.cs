using System.IO;
using System.Windows;
using FolderHub.Services;

namespace FolderHub;

public partial class App : Application
{
    public static HubConfig Config { get; private set; } = new();

    /// <summary>Modo residente nesta execução: bandeja + atalho global.</summary>
    public static bool Background { get; set; }

    /// <summary>Nasce escondido (é assim que a entrada de inicialização chama).</summary>
    public static bool StartHidden { get; private set; }

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

        // Argumento vence a config: permite vários hubs, um atalho por pasta.
        //   FolderHub.exe "D:\Jogos"            abre aquele hub
        //   FolderHub.exe --background          fica residente, escondido
        string? folder = null;
        bool background = Config.Background;

        foreach (string arg in e.Args)
        {
            switch (arg)
            {
                case "--background" or "-b":
                    background = true;
                    StartHidden = true;
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

        folder ??= Config.FolderPath;

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            folder = AskForFolder();
            if (folder == null)
            {
                Shutdown();
                return;
            }

            Config.FolderPath = folder;
            Config.Save();
        }

        var window = new MainWindow(folder);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Background) SingleInstance.ForgetWindow();
        SingleInstance.Release();
        base.OnExit(e);
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
