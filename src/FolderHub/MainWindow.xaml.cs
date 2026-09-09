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

namespace FolderHub;

public partial class MainWindow : Window
{
    // Card de 160x132 com 5px de margem de cada lado = 10px de gap, como no design.
    private const double CardOuterWidth = 170;
    private const double CardOuterHeight = 142;

    // Só serve para limitar a altura da grade em telas baixas; a altura real da
    // janela vem de SizeToContent, então erro de alguns pixels aqui não importa.
    private const double ApproxChromeHeight = 166;

    // 3 colunas na largura do design (560px).
    private const double MinShellWidth = 562;

    private readonly ObservableCollection<AppItem> _visible = [];
    private readonly List<AppItem> _all = [];
    private readonly Dictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly DispatcherTimer _reloadDebounce;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource _iconCts = new();

    private string _folder;
    private int _columns = 4;
    private bool _suppressBlurClose;
    private bool _closing;

    // Reordenar arrastando. O formato próprio separa o arraste interno do
    // arraste externo (pasta ou atalhos vindos do Explorer).
    private const string DragFormat = "FolderHub.Reorder";
    private Point _dragOrigin;
    private AppItem? _dragCandidate;
    private AppItem? _dragging;
    private bool _dragCompleted;
    private bool _animateIntro;

    public MainWindow(string folder)
    {
        InitializeComponent();

        // Precisa ser antes do handle nascer: mudar depois faz o WPF recriar o HWND.
        ShowInTaskbar = !App.Background;

        _folder = folder;

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

        Reload(resize: true, animate: true);
    }

    // ----------------------------------------------------------- ciclo de vida

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowEffects.ApplyAcrylic(this);
        SetUpBackgroundMode();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        StartWatching();

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
        if (App.Config.CloseOnBlur && !_suppressBlurClose && !_closing) Dismiss();
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
        if (pending.Count == 0) return;

        // Shell COM é apartment-threaded: uma STA dedicada é o caminho mais previsível.
        var thread = new Thread(() =>
        {
            foreach (var item in pending)
            {
                if (token.IsCancellationRequested) return;

                var icon = IconLoader.Load(item.Path);
                if (icon is null) continue;

                Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                {
                    if (token.IsCancellationRequested) return;
                    _iconCache[item.Path] = icon;
                    item.Icon = icon;
                });
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = "FolderHub.Icons"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private void ApplyFilter()
    {
        string query = SearchBox.Text.Trim();

        _visible.Clear();
        foreach (var item in _all)
        {
            if (query.Length == 0 || item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                _visible.Add(item);
            }
        }

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

        CountText.Text = _visible.Count == _all.Count
            ? $"{_all.Count} {(_all.Count == 1 ? "atalho" : "atalhos")}"
            : $"{_visible.Count} de {_all.Count}";

        if (_visible.Count > 0) Cards.SelectedIndex = 0;
    }

    private void UpdateHeader()
    {
        string name = new DirectoryInfo(_folder).Name;
        TitleText.Text = string.IsNullOrWhiteSpace(name) ? "Hub" : name;
        SubtitleText.Text = PrettyPath(_folder);
        Title = $"{TitleText.Text} — FolderHub";
    }

    /// <summary>Caminho curto o bastante para caber no cabeçalho sem virar sopa de letras.</summary>
    private static string PrettyPath(string path)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length > 0 && path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
        {
            path = "~" + path[home.Length..];
        }

        if (path.Length <= 46) return path;

        var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 3) return path;

        return $"{parts[0]}{Path.DirectorySeparatorChar}…{Path.DirectorySeparatorChar}" +
               string.Join(Path.DirectorySeparatorChar, parts[^2..]);
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

    private void SetFolder(string folder, bool persist = true)
    {
        _folder = folder;

        if (persist)
        {
            App.Config.FolderPath = folder;
            App.Config.Save();
        }

        _iconCache.Clear();
        SearchBox.Clear();
        Reload(resize: true, animate: true);
        StartWatching();
    }

    // ----------------------------------------------------------- layout

    private void ResizeToContent(int count)
    {
        int max = Math.Clamp(App.Config.MaxColumns, 3, 10);
        int cols = count <= 0 ? 3 : BalancedColumns(count, max);
        _columns = cols;

        int rows = count <= 0 ? 1 : (int)Math.Ceiling(count / (double)cols);

        var work = SystemParameters.WorkArea;

        // Sem MaxWidth o WrapPanel usaria toda a largura disponível e quebraria
        // em mais colunas do que o cálculo acima decidiu.
        Cards.MaxWidth = cols * CardOuterWidth;

        // Altura explícita da grade + SizeToContent: a janela fecha exatamente
        // em volta das linhas, sem depender de estimar cabeçalho e rodapé.
        double roomForCards = Math.Max(CardOuterHeight, work.Height * 0.84 - ApproxChromeHeight);
        Cards.Height = Math.Min(rows * CardOuterHeight, roomForCards);

        Width = Math.Min(Math.Max(MinShellWidth, cols * CardOuterWidth + 52), work.Width - 40);

        if (IsLoaded) ClampIntoWorkArea(work);
    }

    /// <summary>
    /// Escolhe o nº de colunas perto do formato quadrado, mas preferindo
    /// grades em que a última linha fica cheia (sem buracos feios).
    /// </summary>
    private static int BalancedColumns(int count, int max)
    {
        int ideal = Math.Clamp((int)Math.Ceiling(Math.Sqrt(count * 1.6)), 3, max);

        int best = ideal;
        int bestScore = int.MaxValue;

        for (int cols = Math.Max(3, ideal - 1); cols <= Math.Min(max, ideal + 1); cols++)
        {
            int remainder = count % cols;
            int holes = remainder == 0 ? 0 : cols - remainder;
            int score = holes * 2 + Math.Abs(cols - ideal);

            if (score < bestScore)
            {
                bestScore = score;
                best = cols;
            }
        }

        return best;
    }

    private void ClampIntoWorkArea(Rect work)
    {
        if (Left + Width > work.Right) Left = Math.Max(work.Left, work.Right - Width);
        if (Top + Height > work.Bottom) Top = Math.Max(work.Top, work.Bottom - Height);
        if (Left < work.Left) Left = work.Left;
        if (Top < work.Top) Top = work.Top;
    }

    // ----------------------------------------------------------- animações

    private void AnimateShellIn()
    {
        Shell.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));

        ShellLift.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

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

    /// <summary>Some da tela: fecha de vez, ou só esconde se estiver residente.</summary>
    private void Dismiss()
    {
        if (_closing) return;
        _closing = true;

        var fade = new DoubleAnimation(Shell.Opacity, 0, TimeSpan.FromMilliseconds(100));
        fade.Completed += (_, _) =>
        {
            if (App.Background) HideHub();
            else Close();
        };
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

    // ----------------------------------------------------------- reordenar

    /// <summary>Só faz sentido arrastar com a ordem manual e a lista inteira à vista.</summary>
    private bool CanReorder => App.Config.Sort == SortMode.Manual && SearchBox.Text.Length == 0;

    private void Cards_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragCompleted = false;
        _dragOrigin = e.GetPosition(this);
        _dragCandidate = CanReorder ? ItemFrom(e.OriginalSource) : null;
    }

    private void Cards_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate is null || e.LeftButton != MouseButtonState.Pressed) return;

        var delta = e.GetPosition(this) - _dragOrigin;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _dragging = _dragCandidate;
        _dragCandidate = null;
        _animateIntro = false;
        Cards.SelectedItem = _dragging;

        try
        {
            DragDrop.DoDragDrop(Cards, new DataObject(DragFormat, _dragging.Path), DragDropEffects.Move);
        }
        finally
        {
            _dragging = null;
            _dragCompleted = true;
        }
    }

    private void Cards_DragOver(object sender, DragEventArgs e)
    {
        if (_dragging is null || !e.Data.GetDataPresent(DragFormat)) return;

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        // Reordena ao vivo: os cards se acomodam sob o cursor, sem adorner.
        if (ItemFrom(e.OriginalSource) is not { } target || ReferenceEquals(target, _dragging)) return;

        int from = _visible.IndexOf(_dragging);
        int to = _visible.IndexOf(target);
        if (from >= 0 && to >= 0 && from != to) _visible.Move(from, to);
    }

    private void Cards_Drop(object sender, DragEventArgs e)
    {
        if (_dragging is null || !e.Data.GetDataPresent(DragFormat)) return;
        e.Handled = true;

        // O caminho muda no disco, o nome não: é por ele que reencontramos o card.
        string moved = _dragging.Name;

        PersistOrder([.. _visible]);
        _dragging = null;

        // Reler é o que mantém caminhos, ordem e ícones coerentes.
        Reload(resize: false);

        int index = _visible.ToList().FindIndex(i => i.Name == moved);
        if (index >= 0) MoveTo(index);
    }

    /// <summary>Grava a ordem renomeando os arquivos com prefixo numérico.</summary>
    private void PersistOrder(IReadOnlyList<AppItem> ordered)
    {
        bool watching = _watcher?.EnableRaisingEvents ?? false;
        if (_watcher != null) _watcher.EnableRaisingEvents = false;

        try
        {
            foreach (var (from, to) in ManualOrder.Apply(ordered))
            {
                if (_iconCache.Remove(from, out var icon)) _iconCache[to] = icon;
            }
        }
        finally
        {
            _reloadDebounce.Stop();
            if (_watcher != null) _watcher.EnableRaisingEvents = watching;
        }
    }

    // ----------------------------------------------------------- menu de ordem

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is not { } menu) return;

        string current = App.Config.Sort.ToString();
        foreach (var entry in menu.Items.OfType<MenuItem>())
        {
            entry.IsChecked = entry.Tag as string == current;
        }

        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void SortMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem entry || entry.Tag is not string tag) return;
        if (!Enum.TryParse(tag, out SortMode mode) || mode == App.Config.Sort) return;

        App.Config.Sort = mode;
        App.Config.Save();
        Reload(resize: false, animate: true);
    }

    private void FreezeOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_all.Count == 0) return;

        PersistOrder(_all);
        App.Config.Sort = SortMode.Manual;
        App.Config.Save();
        Reload(resize: false);
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

            case Key.F5:
                _iconCache.Clear();
                Reload(resize: true, animate: true);
                e.Handled = true;
                return;

            case Key.O when ctrl:
                ChangeFolder();
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

        try { DragMove(); }
        catch { /* já solto */ }
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

    // ----------------------------------------------------------- drag & drop

    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        ShowDropOverlay(e);
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        ShowDropOverlay(e);
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        base.OnDragLeave(e);
        HideDropOverlay();
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        if (e.Data.GetDataPresent(DragFormat)) return;

        HideDropOverlay();

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;

        if (paths.Length == 1 && Directory.Exists(paths[0]))
        {
            SetFolder(paths[0]);
            return;
        }

        int added = ShortcutWriter.AddToFolder(paths, _folder);
        if (added > 0) Reload(resize: true);
    }

    private void ShowDropOverlay(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DragFormat))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        bool isFolder = paths.Length == 1 && Directory.Exists(paths[0]);
        DropText.Text = isFolder ? "Solte para usar esta pasta" : "Solte para adicionar ao hub";
        e.Effects = isFolder ? DragDropEffects.Link : DragDropEffects.Copy;
        e.Handled = true;

        if (DropOverlay.Visibility == Visibility.Visible) return;

        DropOverlay.Visibility = Visibility.Visible;
        DropOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
    }

    private void HideDropOverlay()
    {
        if (DropOverlay.Visibility != Visibility.Visible) return;

        var fade = new DoubleAnimation(DropOverlay.Opacity, 0, TimeSpan.FromMilliseconds(140));
        fade.Completed += (_, _) => DropOverlay.Visibility = Visibility.Collapsed;
        DropOverlay.BeginAnimation(OpacityProperty, fade);
    }
}
