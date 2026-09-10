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
            Assert.NotNull(window);
            Assert.True(window.ApplyTemplate());
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
            Assert.NotNull(window);
            Assert.True(window.ApplyTemplate());
        });

    [Fact]
    public void tema_claro_e_gravado_no_json()
    {
        var json = new HubConfig { ThemeMode = FolderHub.Services.ThemeMode.Light }.ToJson();
        Assert.Contains("\"themeMode\": \"Light\"", json);
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
