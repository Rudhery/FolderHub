using System.Windows;
using FolderHub.Services;
using FolderHub.Views;
using Xunit;

namespace FolderHub.Tests;

/// <summary>
/// Smoke tests para o limite que o compilador não cobre: carregar ResourceDictionaries,
/// templates e janelas WPF de verdade. Cada caso roda em STA, como o dispatcher do app.
/// </summary>
public sealed class WindowSmokeTests
{
    [Fact]
    public void settings_window_abre_no_tema_escuro()
        => OnSta(() =>
        {
            EnsureApplication();
            App.Config.ThemeMode = FolderHub.Services.ThemeMode.Dark;
            App.Config.Tabs = [new TabConfig { Path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) }];
            LiveTheme.Apply(App.Config);

            var window = new SettingsWindow();
            Realize(window);
        });

    [Fact]
    public void settings_window_abre_no_tema_claro()
        => OnSta(() =>
        {
            EnsureApplication();
            App.Config.ThemeMode = FolderHub.Services.ThemeMode.Light;
            App.Config.Tabs = [new TabConfig { Path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) }];
            LiveTheme.Apply(App.Config);

            var window = new SettingsWindow();
            Realize(window);
        });

    /// <summary>
    /// O hub em si, com uma pasta de verdade: barra lateral, Mais usados, grade
    /// e a troca para a lista de resultados ao digitar.
    /// </summary>
    [Fact]
    public void hub_abre_nos_dois_temas_e_busca_em_lista()
        => OnSta(() =>
        {
            using var folder = new TempFolder();
            folder.Add("Bloco.lnk", "Calc.lnk", "Explorer.lnk", "Paint.lnk", "Terminal.lnk", "Zoom.url");
            UsageStats.Record(Path.Combine(folder.Path, "Terminal.lnk"));

            foreach (var mode in new[] { FolderHub.Services.ThemeMode.Dark, FolderHub.Services.ThemeMode.Light })
            {
                EnsureApplication();
                App.Config.ThemeMode = mode;
                App.Config.SingleFolder = false;
                App.Config.Tabs = [new TabConfig { Path = folder.Path }, new TabConfig { Path = folder.Path, Name = "Outro" }];
                LiveTheme.Apply(App.Config);

                var window = new MainWindow([new Models.HubTab { Path = folder.Path }, new Models.HubTab { Path = folder.Path, CustomName = "Outro" }]);
                window.ShowActivated = false;
                window.ShowInTaskbar = false;

                try
                {
                    window.Show();
                    window.UpdateLayout();

                    var results = (System.Windows.Controls.ListBox)window.FindName("Results");
                    var search = (System.Windows.Controls.TextBox)window.FindName("SearchBox");
                    var frequent = (FrameworkElement)window.FindName("FrequentSection");

                    Assert.Equal(Visibility.Visible, frequent.Visibility);
                    Assert.Equal(Visibility.Collapsed, results.Visibility);

                    search.Text = "ca";
                    window.UpdateLayout();

                    Assert.Equal(Visibility.Visible, results.Visibility);
                    Assert.Single(results.Items);
                }
                finally
                {
                    window.Close();
                }
            }
        });

    [Fact]
    public void welcome_window_abre_no_tema_escuro()
        => OnSta(() =>
        {
            EnsureApplication();
            App.Config.ThemeMode = FolderHub.Services.ThemeMode.Dark;
            LiveTheme.Apply(App.Config);

            var window = new WelcomeWindow();
            Realize(window);

            // Sem pasta escolhida não há para onde avançar, e é o único estado
            // desta tela que o usuário não consegue sair sem agir.
            Assert.False(window.Completed);
            Assert.Null(window.Folder);
        });

    [Fact]
    public void welcome_window_abre_no_tema_claro()
        => OnSta(() =>
        {
            EnsureApplication();
            App.Config.ThemeMode = FolderHub.Services.ThemeMode.Light;
            LiveTheme.Apply(App.Config);

            var window = new WelcomeWindow();
            Realize(window);
        });

    [Fact]
    public void whats_new_window_abre_nos_dois_temas()
        => OnSta(() =>
        {
            foreach (var mode in new[] { FolderHub.Services.ThemeMode.Dark, FolderHub.Services.ThemeMode.Light })
            {
                EnsureApplication();
                App.Config.ThemeMode = mode;
                App.Config.SeenVersion = "1.0.0";
                LiveTheme.Apply(App.Config);

                var window = new WhatsNewWindow();
                Realize(window);
            }
        });

    /// <summary>
    /// A versão anunciada sai do assembly, não do arquivo de notas: a tela não
    /// deve conseguir dizer que é uma versão que não está rodando.
    /// </summary>
    [Fact]
    public void as_novidades_sao_da_versao_que_esta_rodando()
        => Assert.Equal(App.Version, ReleaseNotes.Version);

    [Fact]
    public void novidades_nao_reaparecem_na_mesma_versao()
        => Assert.True(ReleaseNotes.AlreadySeen(ReleaseNotes.Version));

    /// <summary>
    /// Sem registro conta como não vista: quem atualiza de uma versão anterior
    /// a este campo perderia as novidades caladamente se fosse o contrário.
    /// Distinguir isso de primeira instalação é do App, que sabe se a
    /// instalação guiada acabou de rodar.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0.0")]
    public void sem_registro_ou_versao_antiga_conta_como_nao_vista(string? seen)
        => Assert.False(ReleaseNotes.AlreadySeen(seen));

    [Fact]
    public void tema_claro_e_gravado_no_json()
    {
        var json = new HubConfig { ThemeMode = FolderHub.Services.ThemeMode.Light }.ToJson();
        Assert.Contains("\"themeMode\": \"Light\"", json);
    }

    /// <summary>
    /// Abre a janela de verdade e fecha.
    ///
    /// Tem que ser <c>Show</c>: medir e organizar uma janela sem handle não
    /// expande o painel de uma <c>ItemsControl</c>, e foi exatamente ali que um
    /// <c>ItemsPanelTemplate</c> inválido passou — compilava, passava no
    /// <c>ApplyTemplate</c> e estourava na cara de quem abriu o app.
    ///
    /// <c>ShowActivated = false</c> para não roubar o foco de quem está
    /// rodando os testes.
    /// </summary>
    private static void Realize(Window window)
    {
        window.ShowActivated = false;
        window.ShowInTaskbar = false;

        try
        {
            window.Show();
            window.UpdateLayout();
        }
        finally
        {
            window.Close();
        }
    }

    private static void EnsureApplication()
    {
        if (Application.Current is null) _ = new Application();

        var dictionaries = Application.Current!.Resources.MergedDictionaries;
        dictionaries.Clear();
        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/FolderHub;component/Themes/HubTheme.xaml", UriKind.Absolute)
        });
        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/FolderHub;component/Themes/HubControls.xaml", UriKind.Absolute)
        });
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }
}
