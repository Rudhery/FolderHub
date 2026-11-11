using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FolderHub.Controls;
using FolderHub.Models;
using FolderHub.Services;
using FolderHub.Views;

namespace FolderHub;

public partial class MainWindow : HubWindow
{
    // Célula + 2px de margem de cada lado. Sai do dicionário vivo, onde a
    // densidade escolhida já foi aplicada por cima do que o tema pediu — assim
    // existe uma medida só, e não duas concorrendo.
    private double CardOuterWidth => ThemeSize("CardWidth", 104) + 4;
    private double CardOuterHeight => ThemeSize("CardHeight", 108) + 4;

    private double ThemeSize(string key, double fallback)
        => TryFindResource(key) is double value && value > 0 ? value : fallback;

    // Só serve para limitar a altura da grade em telas baixas; a altura real da
    // janela vem de SizeToContent, então erro de alguns pixels aqui não importa.
    // Busca, Mais usados, cabeçalho de Todos e barra de ações.
    private const double ApproxChromeHeight = 330;

    // A busca e a barra de ações precisam de espaço mesmo numa pasta com três atalhos.
    private const double MinShellWidth = 600;

    // Barra lateral + a borda dela. Some no modo de pasta única.
    private double RailWidth => Rail.Visibility == Visibility.Visible ? 73 : 0;

    private readonly ObservableCollection<AppItem> _visible = [];

    // Apontam para as coleções da aba ativa; trocar de aba só troca a referência,
    // e o resto do código segue sem saber que existe aba.
    private List<AppItem> _all = [];
    private Dictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly DispatcherTimer _reloadDebounce;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource _iconCts = new();

    private string _folder = string.Empty;
    private int _columns = 4;
    private bool _suppressBlurClose;

    private bool _animateIntro;

    public MainWindow(IReadOnlyList<HubTab> tabs)
    {
        InitializeComponent();

        // Precisa ser antes do handle nascer: mudar depois faz o WPF recriar o HWND.
        ShowInTaskbar = !App.Background;

        _reloadDebounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(280)
        };
        _reloadDebounce.Tick += (_, _) =>
        {
            _reloadDebounce.Stop();
            Reload(resize: true);
        };

        Cards.ItemsSource = _visible;
        Cards.PreviewMouseLeftButtonUp += Cards_PreviewMouseLeftButtonUp;
        Cards.PreviewMouseRightButtonDown += List_PreviewMouseRightButtonDown;
        Cards.PreviewMouseLeftButtonDown += Cards_PreviewMouseLeftButtonDown;
        Cards.PreviewMouseMove += Cards_PreviewMouseMove;
        Cards.DragOver += Cards_DragOver;
        Cards.Drop += Cards_Drop;

        Results.ItemsSource = _visible;
        Results.PreviewMouseLeftButtonUp += Results_PreviewMouseLeftButtonUp;
        Results.PreviewMouseRightButtonDown += List_PreviewMouseRightButtonDown;

        BuildTabs(tabs);

        int initialIndex = App.Config.SingleFolder || !App.Config.RememberLastHub
            ? 0
            : Math.Clamp(App.Config.LastHub, 0, _tabs.Count - 1);
        ActivateTab(_tabs[initialIndex], resize: true, animate: true);
        WatchConfig();
    }

    // ----------------------------------------------------------- ciclo de vida

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);   // a casca aplica o acrílico
        SetUpBackgroundMode();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // Lançado junto com o Windows: nasce escondido, esperando o atalho.
        if (App.StartHidden)
        {
            HideHub();
            return;
        }

        AnimateShellIn();
        SearchBox.Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        _iconCts.Cancel();
        _watcher?.Dispose();
        _reloadDebounce.Stop();
        StopWatchingConfig();
        TearDownBackgroundMode();
        base.OnClosed(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Residente, o X e o Esc apenas escondem: quem encerra é a bandeja.
        if (App.Background && !_reallyExit)
        {
            e.Cancel = true;
            HideHub();
        }

        base.OnClosing(e);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (App.Config.CloseOnBlur && !_suppressBlurClose && !Dismissing) Dismiss();
    }

    // ----------------------------------------------------------- carregamento

    private void Reload(bool resize, bool animate = false)
    {
        int previousCount = _all.Count;
        _animateIntro = animate;

        // Pastas de versões antigas ainda têm o "01 - " no nome: vira ordem interna uma vez só.
        ManualOrder.MigrateLegacyPrefixes(_folder);

        var scanned = FolderScanner.Scan(_folder);
        FolderScanner.Sort(scanned, App.Config.Sort, _folder);

        _iconCts.Cancel();
        _iconCts = new CancellationTokenSource();

        _all.Clear();
        foreach (var item in scanned)
        {
            if (_iconCache.TryGetValue(item.Path, out var cached)) item.Icon = cached;
            _all.Add(item);
        }

        UpdateHeader();
        UpdateFrequent();
        ApplyFilter();

        if (resize && (_all.Count != previousCount || !IsLoaded)) ResizeToContent(_all.Count);

        StartIconLoad([.. _all.Where(i => i.Icon is null)], _iconCts.Token);
    }

    private void StartIconLoad(List<AppItem> pending, CancellationToken token)
    {
        IconLoadQueue.Start(pending, _folder, (item, icon) =>
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (token.IsCancellationRequested) return;
                _iconCache[item.Path] = icon;
                item.Icon = icon;
                if (ReferenceEquals(item, SelectedItem)) SelectedIcon.Source = icon;
            }),
            token);
    }

    /// <summary>A lista que está na tela agora: a grade, ou os resultados enquanto se digita.</summary>
    private ListBox ActiveList => Results.Visibility == Visibility.Visible ? Results : Cards;

    private AppItem? SelectedItem => ActiveList.SelectedItem as AppItem;

    private void ApplyFilter()
    {
        string query = SearchBox.Text.Trim();
        bool filtering = query.Length > 0;

        _visible.Clear();
        foreach (var item in ItemFilter.Apply(_all, query)) _visible.Add(item);

        SearchPlaceholder.Visibility = filtering ? Visibility.Collapsed : Visibility.Visible;

        bool none = _visible.Count == 0;

        // Hidden, e não Collapsed: a grade segura a altura da janela enquanto a
        // lista de resultados (ou o estado vazio) aparece no mesmo espaço.
        Browse.Visibility = !filtering && _all.Count > 0 ? Visibility.Visible : Visibility.Hidden;
        Results.Visibility = filtering && !none ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = none ? Visibility.Visible : Visibility.Collapsed;

        if (none)
        {
            EmptyTitle.Text = filtering ? "Nada encontrado" : "Nenhum atalho aqui";
            EmptyHint.Text = !filtering
                ? "Solte atalhos (.lnk, .url, .exe) dentro da pasta e eles aparecem aqui na hora."
                : _tabs.Count > 1
                    ? $"Nenhum app com \"{query}\" em {CurrentHubName}. Tab procura no próximo hub."
                    : $"Nenhum app com \"{query}\" nesta pasta.";
            EmptyGlyph.Glyph = filtering ? HubGlyph.Search : HubGlyph.Folder;
            EmptyAction.Visibility = filtering ? Visibility.Collapsed : Visibility.Visible;
        }

        CountText.Text = $"{_all.Count} {(_all.Count == 1 ? "atalho" : "atalhos")}";
        CountText.Visibility = App.Config.ShowCount ? Visibility.Visible : Visibility.Collapsed;

        if (_visible.Count > 0) ActiveList.SelectedIndex = 0;
        UpdateSelection();
    }

    private string CurrentHubName => _tab?.Name ?? PathDisplay.FolderName(_folder);

    private void UpdateHeader()
    {
        // A busca varre só o hub aberto, e o texto dela diz qual é.
        SearchPlaceholder.Text = $"Buscar em {CurrentHubName}";
        Title = $"{CurrentHubName} — FolderHub";

        PathText.Text = PathDisplay.Shorten(_folder);
        PathText.Visibility = App.Config.ShowPath ? Visibility.Visible : Visibility.Collapsed;

        // Lembrar o atalho global só faz sentido quando ele existe: no modo residente.
        string hotKey = App.Config.HotKey ?? string.Empty;
        HotKeyHint.Text = string.Join(" ", HotKeyText.Parts(hotKey));
        HotKeyHint.Visibility = App.Background && hotKey.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// A fileira "Mais usados". Só aparece quando acrescenta algo: com quatro
    /// atalhos ou menos ela repetiria a grade inteira.
    /// </summary>
    private void UpdateFrequent()
    {
        IReadOnlyList<FrequentItem> top = _all.Count > 4 ? UsageStats.Top(_all, 4) : [];
        Frequent.ItemsSource = top;
        FrequentSection.Visibility = top.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>A barra de ações mostra quem está escolhido.</summary>
    private void UpdateSelection()
    {
        if (SelectedItem is { } item)
        {
            SelectedIcon.Visibility = Visibility.Visible;
            SelectedIcon.Source = item.Icon;
            SelectedIcon.Text = item.Initial;
            SelectedName.Text = item.Name;
            SelectedKind.Text = item.Kind;
        }
        else
        {
            SelectedIcon.Visibility = Visibility.Collapsed;
            SelectedIcon.Source = null;
            SelectedIcon.Text = null;
            SelectedName.Text = string.Empty;
            SelectedKind.Text = "nada selecionado";
        }
    }

    private void Selection_Changed(object sender, SelectionChangedEventArgs e) => UpdateSelection();

    private void StartWatching()
    {
        _watcher?.Dispose();
        _watcher = null;

        try
        {
            var watcher = new FileSystemWatcher(_folder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Attributes,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            void Bump(object? _, FileSystemEventArgs __) =>
                Dispatcher.BeginInvoke(() => { _reloadDebounce.Stop(); _reloadDebounce.Start(); });

            watcher.Created += Bump;
            watcher.Deleted += Bump;
            watcher.Changed += Bump;
            watcher.Renamed += (s, e) => Bump(s, e);

            _watcher = watcher;
        }
        catch
        {
            // pastas de rede ou sem permissão: segue sem auto-refresh
        }
    }

    /// <summary>Troca a pasta da aba ativa (Ctrl+O). Para somar uma pasta, use AddTab.</summary>
    private void SetFolder(string folder, bool persist = true)
    {
        if (_tab is null) return;

        _tab.Path = folder;
        _tab.Icons.Clear();
        _tab.Count = FolderScanner.CountSupported(folder);
        _folder = folder;

        if (persist) PersistTabs();

        SearchBox.Clear();
        Reload(resize: true, animate: true);
        StartWatching();
    }

    // ----------------------------------------------------------- layout

    private void ResizeToContent(int count)
    {
        // A janela cabe na maior aba, senão ela pularia de tamanho a cada troca.
        count = Math.Max(count, LargestTabCount());

        int cols = GridLayout.Columns(count, Math.Clamp(App.Config.MaxColumns, 3, 10));
        _columns = cols;

        int rows = GridLayout.Rows(count, cols);

        var work = SystemParameters.WorkArea;

        // Largura exata: o WrapPanel quebra nas colunas que o cálculo acima
        // decidiu, e a grade fica alinhada com o cabeçalho "Todos".
        Cards.Width = cols * CardOuterWidth;

        // Altura explícita da grade + SizeToContent: a janela fecha exatamente
        // em volta das linhas, sem depender de estimar cabeçalho e rodapé.
        //
        // A altura sai sempre múltipla de uma linha. Cortar no meio de uma fazia
        // aparecer barra de rolagem por causa de alguns pixels sobrando, com uma
        // fileira pela metade encostada no rodapé.
        double room = Math.Max(CardOuterHeight, work.Height * 0.84 - ApproxChromeHeight);
        int fit = Math.Max(1, (int)(room / CardOuterHeight));
        Cards.Height = Math.Min(rows, fit) * CardOuterHeight;

        // Com SizeToContent ligado, o WPF recalcula o tamanho no próximo layout e
        // descarta a largura pedida — só a primeira, aplicada antes de a janela
        // aparecer, pegava. Desligar durante a atribuição faz a mudança valer.
        double wanted = Math.Min(Math.Max(MinShellWidth, RailWidth + cols * CardOuterWidth + 30), work.Width - 40);

        var sizing = SizeToContent;
        SizeToContent = SizeToContent.Manual;
        Width = wanted;
        SizeToContent = sizing;

        if (IsLoaded) ClampIntoWorkArea(work);
    }

    private void ClampIntoWorkArea(Rect work)
    {
        if (Left + Width > work.Right) Left = Math.Max(work.Left, work.Right - Width);
        if (Top + Height > work.Bottom) Top = Math.Max(work.Top, work.Bottom - Height);
        if (Left < work.Left) Left = work.Left;
        if (Top < work.Top) Top = work.Top;
    }

    // ----------------------------------------------------------- animações

    private void AnimateShellIn() => FadeIn();

    /// <summary>Entrada escalonada dos ícones da grade — dispara no Loaded de cada container.</summary>
    private void Card_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_animateIntro || App.Config.ReduceMotion || sender is not ListBoxItem container) return;

        int index = Cards.ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0) index = 0;

        var delay = TimeSpan.FromMilliseconds(Math.Min(index, 42) * 12);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var transform = new TranslateTransform(0, 10);
        container.RenderTransform = transform;
        container.Opacity = 0;

        container.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { BeginTime = delay });

        transform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(280)) { BeginTime = delay, EasingFunction = ease });
    }

    /// <summary>Residente o hub só esconde; fora dele, o padrão da casca vale.</summary>
    protected override void Dismiss()
    {
        if (!App.Background)
        {
            base.Dismiss();
            return;
        }

        if (Dismissing) return;
        Dismissing = true;

        var fade = new DoubleAnimation(Shell.Opacity, 0, TimeSpan.FromMilliseconds(100));
        fade.Completed += (_, _) => HideHub();
        Shell.BeginAnimation(OpacityProperty, fade);
    }

    // ----------------------------------------------------------- abrir

    private void Launch(AppItem item)
    {
        if (!Launcher.Launch(item)) return;

        // Conta para "Mais usados". Depois de abrir: se o app não abriu, não conta.
        UsageStats.Record(item.Path);
        if (App.Config.CloseAfterLaunch) Dismiss();
        else UpdateFrequent();
    }

    private void RunSelectedAsAdmin()
    {
        if (SelectedItem is not { } item) return;

        Launcher.RunAsAdmin(item);
        UsageStats.Record(item.Path);
        if (App.Config.CloseAfterLaunch) Dismiss();
    }

    private void CopyPath()
    {
        if (SelectedItem is not { } item) return;

        try { Clipboard.SetText(item.Path); }
        catch (Exception error) { Log.Warn("não consegui copiar o caminho", error); }
    }

    /// <summary>O menu de ações do atalho escolhido, aberto em cima do botão Ações.</summary>
    private void OpenItemMenu()
    {
        if (SelectedItem is null || FindResource("ItemMenu") is not ContextMenu menu) return;

        menu.PlacementTarget = ActionsButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        menu.IsOpen = true;
    }

    // ----------------------------------------------------------- mouse

    private AppItem? ItemFrom(object? source) => ItemFrom(source, Cards);

    private static AppItem? ItemFrom(object? source, ItemsControl list)
    {
        if (source is not DependencyObject d) return null;

        var container = ItemsControl.ContainerFromElement(list, d) as ListBoxItem;
        return container?.DataContext as AppItem;
    }

    private void Cards_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragCandidate = null;

        if (_dragCompleted)
        {
            _dragCompleted = false;
            return;
        }

        if (ItemFrom(e.OriginalSource) is { } item)
        {
            e.Handled = true;
            Launch(item);
        }
    }

    private void Results_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ItemFrom(e.OriginalSource, Results) is { } item)
        {
            e.Handled = true;
            Launch(item);
        }
    }

    private void List_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox list && ItemFrom(e.OriginalSource, list) is { } item) list.SelectedItem = item;
    }

    private void Frequent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FrequentItem frequent }) Launch(frequent.Item);
    }

    // ----------------------------------------------------------- teclado

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled) return;

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool grid = ActiveList == Cards;

        switch (e.Key)
        {
            case Key.Escape:
                if (SearchBox.Text.Length > 0) SearchBox.Clear();
                else Dismiss();
                e.Handled = true;
                return;

            case Key.Enter when ctrl:
                RunSelectedAsAdmin();
                e.Handled = true;
                return;

            case Key.Enter:
                if (SelectedItem is { } selected) Launch(selected);
                e.Handled = true;
                return;

            // Alt+Enter chega como tecla de sistema.
            case Key.System when e.SystemKey == Key.Enter:
                if (SelectedItem is { } reveal) Launcher.RevealInExplorer(reveal);
                e.Handled = true;
                return;

            case Key.K when ctrl:
                OpenItemMenu();
                e.Handled = true;
                return;

            case Key.C when ctrl && shift:
                CopyPath();
                e.Handled = true;
                return;

            // A barra fica em teclas diferentes: Oem2 no teclado americano,
            // AbntC1 (ao lado do Shift direito) no ABNT2 brasileiro.
            case Key.Oem2 or Key.AbntC1 when !SearchBox.IsKeyboardFocused:
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                return;

            case Key.F5:
                _iconCache.Clear();
                Reload(resize: true, animate: true);
                e.Handled = true;
                return;

            case Key.O when ctrl:
                ChangeFolder();
                e.Handled = true;
                return;

            // Tab troca de hub e leva a busca junto: não achou aqui, procura no próximo.
            case Key.Tab when ctrl || App.Config.TabSwitchesHub:
                string query = SearchBox.Text;
                MoveTab(shift ? -1 : 1);
                if (query.Length > 0)
                {
                    SearchBox.Text = query;
                    SearchBox.CaretIndex = query.Length;
                }
                e.Handled = true;
                return;

            case >= Key.D1 and <= Key.D9 when ctrl:
                ActivateTabAt(e.Key - Key.D1);
                e.Handled = true;
                return;

            // Na lista de resultados, esquerda e direita ficam com o cursor do texto.
            case Key.Left when grid:
                Move(-1);
                e.Handled = true;
                return;

            case Key.Right when grid:
                Move(1);
                e.Handled = true;
                return;

            case Key.Up:
                Move(-VisibleColumns());
                e.Handled = true;
                return;

            case Key.Down:
                Move(VisibleColumns());
                e.Handled = true;
                return;

            case Key.Home when grid:
                MoveTo(0);
                e.Handled = true;
                return;

            case Key.End when grid:
                MoveTo(_visible.Count - 1);
                e.Handled = true;
                return;
        }

        // Começou a digitar com o foco em outro lugar: vai para a busca.
        if (!SearchBox.IsKeyboardFocused && !ctrl && IsTypingKey(e.Key)) SearchBox.Focus();
    }

    private static bool IsTypingKey(Key key) =>
        key is >= Key.A and <= Key.Z or >= Key.D0 and <= Key.D9 or >= Key.NumPad0 and <= Key.NumPad9;

    private int VisibleColumns() => ActiveList == Results ? 1 : Math.Max(1, Math.Min(_columns, _visible.Count));

    private void Move(int delta)
    {
        if (_visible.Count == 0) return;
        var list = ActiveList;
        int index = list.SelectedIndex < 0 ? 0 : list.SelectedIndex + delta;
        MoveTo(index);
    }

    private void MoveTo(int index)
    {
        if (_visible.Count == 0) return;
        var list = ActiveList;
        index = Math.Clamp(index, 0, _visible.Count - 1);
        list.SelectedIndex = index;
        list.ScrollIntoView(_visible[index]);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Launcher.OpenFolder(_folder);
            return;
        }

        DragFrom(e);
    }

    private void ChangeFolder()
    {
        _suppressBlurClose = true;
        try
        {
            string? folder = App.AskForFolder(_folder);
            if (folder != null) SetFolder(folder);
        }
        finally
        {
            _suppressBlurClose = false;
            Activate();
        }
    }

    // ----------------------------------------------------------- cliques

    private void ChangeFolder_Click(object sender, RoutedEventArgs e) => ChangeFolder();

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        _iconCache.Clear();
        Reload(resize: true, animate: true);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Dismiss();

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => Launcher.OpenFolder(_folder);

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem is { } item) Launch(item);
    }

    private void Actions_Click(object sender, RoutedEventArgs e) => OpenItemMenu();

    private void RunAsAdmin_Click(object sender, RoutedEventArgs e) => RunSelectedAsAdmin();

    private void CopyPath_Click(object sender, RoutedEventArgs e) => CopyPath();

    private void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem is { } item) Launcher.RevealInExplorer(item);
    }
}
