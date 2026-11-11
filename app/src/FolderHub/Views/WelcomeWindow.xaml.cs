using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FolderHub.Controls;
using FolderHub.Models;
using FolderHub.Services;

namespace FolderHub.Views;

/// <summary>
/// A primeira execução, em três passos: escolher a pasta, definir o atalho,
/// conferir o hub montado.
///
/// Antes disto a primeira execução era um seletor de pasta do Windows e mais
/// nada — sem dizer o que a pasta ia virar, e cancelando o app fechava calado.
/// Os dois ajustes que decidem se o produto funciona como launcher (o atalho
/// global e iniciar com o Windows) ficavam escondidos na configuração, que é
/// justamente onde quem acabou de instalar não vai procurar.
///
/// Ela grava tudo de uma vez no fim, e não passo a passo: desistir no meio não
/// deve deixar meia configuração no disco.
/// </summary>
public partial class WelcomeWindow : HubWindow
{
    /// <summary>Quantas miniaturas o passo 3 mostra. É o que cabe na largura.</summary>
    private const int PreviewCount = 4;

    private readonly List<AppItem> _items = [];
    private CancellationTokenSource? _icons;
    private string? _folder;
    private int _step = 1;

    public WelcomeWindow() : base(acrylic: false)
    {
        InitializeComponent();

        HotKeyBox.HotKey = App.Config.HotKey;
        StartWithWindows.IsChecked = true;

        Loaded += (_, _) => FadeIn();
        Closed += (_, _) => _icons?.Cancel();

        Render();
    }

    /// <summary>A instalação foi até o fim. Falso quando a janela foi dispensada.</summary>
    public bool Completed { get; private set; }

    /// <summary>A pasta escolhida, ou nulo se não houve.</summary>
    public string? Folder => _folder;

    // ------------------------------------------------------------------ passos

    private void Render()
    {
        StepFolder.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        StepHotKey.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        StepDone.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;

        Steps.Current = _step;
        Back.Visibility = _step == 1 ? Visibility.Collapsed : Visibility.Visible;
        Next.Content = _step == 3 ? "Começar a usar" : "Avançar";

        // O "Voltar" nasce escondido, e o vão do HubSpacing foi calculado sem
        // ele: sem este pedido o botão volta encostado no primário.
        HubSpacing.Refresh(Actions);

        // No passo 1 não há para onde avançar antes de existir uma pasta: é a
        // única informação que esta tela não consegue inventar um padrão para.
        Next.IsEnabled = _step != 1 || _folder is not null;

        if (_step == 3) FillSummary();
    }

    private void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        string? chosen = App.AskForFolder(_folder);
        if (chosen is null) return;

        _folder = chosen;

        _items.Clear();
        _items.AddRange(FolderScanner.Scan(chosen));
        FolderScanner.Sort(_items, App.Config.Sort, chosen);

        // O botão continua sendo a ação ("Selecionar pasta", com o "+" do
        // tracejado) e a pasta escolhida aparece abaixo. Trocar o rótulo do
        // botão pelo caminho deixava um "+" na frente dele, lendo como se
        // clicar fosse adicionar outra.
        FolderSummary.Text = $"{Describe(_items.Count)} em {PathDisplay.Shorten(chosen)}";
        FolderSummary.Visibility = Visibility.Visible;

        LoadPreviewIcons();
        Render();
    }

    private void HotKeyBox_HotKeyChanged(object? sender, EventArgs e) => Render();

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_step == 1) return;

        _step--;
        Render();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_step < 3)
        {
            _step++;
            Render();
            return;
        }

        Finish();
    }

    // ------------------------------------------------------------------ passo 3

    private void FillSummary()
    {
        string name = _folder is null ? string.Empty : PathDisplay.Shorten(_folder);
        DoneSummary.Text = $"{Describe(_items.Count)} em {name}.";

        Preview.ItemsSource = _items.Take(PreviewCount).ToList();

        DoneHint.Text = HotKeyBox.HotKey.Length > 0
            ? $"Pressione {string.Join(" + ", HotKeyText.Parts(HotKeyBox.HotKey))} para abrir."
            : "Sem atalho global: o hub abre pelo ícone.";
    }

    /// <summary>
    /// Só os ícones das miniaturas. A pasta pode ter centenas de atalhos, e
    /// extrair todos aqui competiria com a janela do hub, que vai fazer isso de
    /// novo daqui a segundos.
    /// </summary>
    private void LoadPreviewIcons()
    {
        _icons?.Cancel();
        _icons = new CancellationTokenSource();

        var token = _icons.Token;
        var preview = _items.Take(PreviewCount).ToList();
        if (preview.Count == 0 || _folder is null) return;

        IconLoadQueue.Start(preview, _folder, (item, icon) =>
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (token.IsCancellationRequested) return;
                item.Icon = icon;
            }),
            token);
    }

    private static string Describe(int count)
        => count == 1 ? "1 atalho encontrado" : $"{count} atalhos encontrados";

    // ------------------------------------------------------------------- gravar

    /// <summary>
    /// Grava a configuração inteira de uma vez. O modo residente vem junto com
    /// o atalho porque sem ele não há ninguém escutando — um atalho gravado num
    /// app que não fica de pé não funcionaria, e a tela teria prometido algo
    /// que não acontece.
    /// </summary>
    private void Finish()
    {
        if (_folder is null) return;

        App.Config.Tabs = [new TabConfig { Path = _folder }];
        App.Config.HotKey = HotKeyBox.HotKey;
        App.Config.Background = HotKeyBox.HotKey.Length > 0;

        // Quem acabou de instalar já viu o que esta versão tem: seria estranho
        // abrir as novidades de uma versão que nunca foi usada.
        App.Config.SeenVersion = ReleaseNotes.Version;

        bool startup = StartWithWindows.IsChecked == true;
        if (startup && !StartupRegistration.Set(true))
        {
            Log.Warn("não consegui registrar o início com o Windows na primeira execução");
        }
        else if (!startup)
        {
            StartupRegistration.Set(false);
        }

        App.Config.Save();

        Completed = true;
        Close();
    }

    // -------------------------------------------------------------------- casca

    private void Shell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragFrom(e);
}
