using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FolderHub.Models;
using FolderHub.Services;
using FolderHub.Views;

namespace FolderHub;

public partial class MainWindow : HubWindow
{
    // Card + 5px de margem de cada lado = 10px de gap, como no design. Vem do
    // tema para que um arquivo de tema possa mudar o tamanho dos cards.
    private double CardOuterWidth => ThemeSize("CardWidth", 160) + 10;
    private double CardOuterHeight => ThemeSize("CardHeight", 118) + 10;

    private double ThemeSize(string key, double fallback)
        => TryFindResource(key) is double value && value > 0 ? value : fallback;

    // Só serve para limitar a altura da grade em telas baixas; a altura real da
    // janela vem de SizeToContent, então erro de alguns pixels aqui não importa.
    // Medido na janela real com abas: 244px de tudo que não é grade.
    private const double ApproxChromeHeight = 244;

    // 3 colunas na largura do design (560px).
    private const double MinShellWidth = 562;

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
        Cards.PreviewMouseRightButtonDown += Cards_PreviewMouseRightButtonDown;
        Cards.PreviewMouseLeftButtonDown += Cards_PreviewMouseLeftButtonDown;
        Cards.PreviewMouseMove += Cards_PreviewMouseMove;
        Cards.DragOver += Cards_DragOver;
        Cards.Drop += Cards_Drop;

        BuildTabs(tabs);
        ActivateTab(tabs[0], resize: true, animate: true);
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

        var scanned = FolderScanner.Scan(_folder);
        FolderScanner.Sort(scanned, App.Config.Sort);

        _iconCts.Cancel();
        _iconCts = new CancellationTokenSource();

        _all.Clear();
        foreach (var item in scanned)
        {
            if (_iconCache.TryGetValue(item.Path, out var cached)) item.Icon = cached;
            _all.Add(item);
        }

        UpdateHeader();
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
            }),
            token);
    }

    private void ApplyFilter()
    {
        string query = SearchBox.Text.Trim();

        _visible.Clear();
        foreach (var item in ItemFilter.Apply(_all, query)) _visible.Add(item);

        SearchPlaceholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        bool empty = _visible.Count == 0;
        Cards.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

        if (empty)
        {
            bool filtering = query.Length > 0;
            EmptyTitle.Text = filtering ? "Nada encontrado" : "Nenhum atalho aqui";
            EmptyHint.Text = filtering
                ? $"Nenhum app com \"{query}\" nesta pasta."
                : "Solte atalhos (.lnk, .url, .exe) dentro da pasta e eles aparecem aqui na hora.";
        }

        string count = _visible.Count == _all.Count
            ? $"{_all.Count} {(_all.Count == 1 ? "atalho" : "atalhos")}"
            : $"{_visible.Count} de {_all.Count}";

        // Com mais de um hub o rodapé também diz em qual deles você está — é a
        // única pista de posição quando a aba ativa rolou para fora da faixa.
        int hub = _tab is null ? 0 : _tabs.IndexOf(_tab) + 1;
        CountText.Text = _tabs.Count > 1 && hub > 0
            ? $"{count} · hub {hub} de {_tabs.Count}"
            : count;

        if (_visible.Count > 0) Cards.SelectedIndex = 0;
    }

    private void UpdateHeader()
    {
        TitleText.Text = PathDisplay.FolderName(_folder);
        SubtitleText.Text = PathDisplay.Shorten(_folder);
        Title = $"{TitleText.Text} — FolderHub";

        // A busca varre só o hub aberto; dizer quantos existem evita prometer
        // uma busca global que o app não faz.
        SearchPlaceholder.Text = _tabs.Count > 1
            ? $"Buscar em {_tabs.Count} hubs"
            : "Buscar";
    }

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

        // Sem MaxWidth o WrapPanel usaria toda a largura disponível e quebraria
        // em mais colunas do que o cálculo acima decidiu.
        Cards.MaxWidth = cols * CardOuterWidth;

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
        double wanted = Math.Min(Math.Max(MinShellWidth, cols * CardOuterWidth + 52), work.Width - 40);

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

    /// <summary>Entrada escalonada dos cards — dispara no Loaded de cada container.</summary>
    private void Card_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_animateIntro || sender is not ListBoxItem container) return;

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

    // ----------------------------------------------------------- interação

    private void Launch(AppItem item)
    {
        if (!Launcher.Launch(item)) return;
        if (App.Config.CloseAfterLaunch) Dismiss();
    }

    private AppItem? ItemFrom(object? source)
    {
        if (source is not DependencyObject d) return null;

        var container = ItemsControl.ContainerFromElement(Cards, d) as ListBoxItem;
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

    private void Cards_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ItemFrom(e.OriginalSource) is { } item) Cards.SelectedItem = item;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled) return;

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        switch (e.Key)
        {
            case Key.Escape:
                if (SearchBox.Text.Length > 0) SearchBox.Clear();
                else Dismiss();
                e.Handled = true;
                return;

            case Key.Enter:
                if (Cards.SelectedItem is AppItem selected) Launch(selected);
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

            case Key.Tab:
                MoveTab(shift ? -1 : 1);
                e.Handled = true;
                return;

            case >= Key.D1 and <= Key.D9 when ctrl:
                ActivateTabAt(e.Key - Key.D1);
                e.Handled = true;
                return;

            case Key.Left:
                Move(-1);
                e.Handled = true;
                return;

            case Key.Right:
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

            case Key.Home:
                MoveTo(0);
                e.Handled = true;
                return;

            case Key.End:
                MoveTo(_visible.Count - 1);
                e.Handled = true;
                return;
        }
    }

    private int VisibleColumns() => Math.Max(1, Math.Min(_columns, _visible.Count));

    private void Move(int delta)
    {
        if (_visible.Count == 0) return;
        int index = Cards.SelectedIndex < 0 ? 0 : Cards.SelectedIndex + delta;
        MoveTo(index);
    }

    private void MoveTo(int index)
    {
        if (_visible.Count == 0) return;
        index = Math.Clamp(index, 0, _visible.Count - 1);
        Cards.SelectedIndex = index;
        Cards.ScrollIntoView(_visible[index]);
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

    private void ChangeFolder_Click(object sender, RoutedEventArgs e) => ChangeFolder();

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        _iconCache.Clear();
        Reload(resize: true, animate: true);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Dismiss();

    private void OpenFolder_Click(object sender, RoutedEventArgs e) => Launcher.OpenFolder(_folder);

    private void RunAsAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (Cards.SelectedItem is AppItem item)
        {
            Launcher.RunAsAdmin(item);
            if (App.Config.CloseAfterLaunch) Dismiss();
        }
    }

    private void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (Cards.SelectedItem is AppItem item) Launcher.RevealInExplorer(item);
    }
}
