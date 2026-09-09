using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using FolderHub.Interop;
using FolderHub.Services;

namespace FolderHub;

/// <summary>
/// Modo residente: o hub fica na bandeja ouvindo o atalho global em vez de
/// encerrar. Tudo que é específico disso mora aqui, separado da janela em si.
/// </summary>
public partial class MainWindow
{
    private readonly GlobalHotKey _hotKey = new();
    private readonly TrayIcon _tray = new();

    private HwndSource? _source;

    private nint Handle => _source?.Handle ?? nint.Zero;

    // ------------------------------------------------------------ ciclo

    private void SetUpBackgroundMode()
    {
        _source = (HwndSource?)PresentationSource.FromVisual(this);
        _source?.AddHook(WndProc);

        if (App.Background) EnableResidentMode();
    }

    private void TearDownBackgroundMode()
    {
        _hotKey.Dispose();
        _tray.Dispose();
        _source?.RemoveHook(WndProc);
    }

    private void EnableResidentMode()
    {
        if (Handle == nint.Zero) return;

        // ShowInTaskbar NÃO pode mudar aqui: o WPF recria o HWND quando essa
        // propriedade muda, e o atalho global e o hook ficariam presos à janela
        // antiga. Ela é decidida no construtor, antes do handle existir.
        SingleInstance.PublishWindow(Handle);

        RegisterHotKey();
        _tray.Add(Handle, TrayTooltip());
    }

    /// <summary>Registra (ou re-registra) o atalho global. Trocável em tempo de execução.</summary>
    private void RegisterHotKey()
    {
        if (Handle == nint.Zero) return;

        _hotKeyFailed = !_hotKey.Register(Handle, App.Config.HotKey);
        _tray.UpdateTooltip(TrayTooltip());

        if (_hotKeyFailed) Log.Warn($"atalho global indisponível: {App.Config.HotKey}");
    }

    private string TrayTooltip() => _hotKeyFailed
        ? "FolderHub (atalho global indisponível)"
        : $"FolderHub — {App.Config.HotKey}";

    private void DisableResidentMode()
    {
        _hotKey.Unregister();
        _tray.Dispose();
        _hotKeyFailed = false;
    }

    private bool _hotKeyFailed;

    // ------------------------------------------------------------ mensagens

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (GlobalHotKey.IsHotKeyMessage(message, wParam))
        {
            ToggleHub();
            handled = true;
            return nint.Zero;
        }

        if (message == unchecked((int)SingleInstance.ShowMessage))
        {
            ShowHub(SingleInstance.TakeRequestedFolder());
            handled = true;
            return nint.Zero;
        }

        if (message == Native.WM_TRAYICON)
        {
            switch ((int)(lParam & 0xFFFF))
            {
                case Native.WM_LBUTTONUP:
                    ToggleHub();
                    break;
                case Native.WM_RBUTTONUP:
                case Native.WM_CONTEXTMENU:
                    ShowTrayMenu();
                    break;
            }

            handled = true;
            return nint.Zero;
        }

        return nint.Zero;
    }

    // ------------------------------------------------------------ mostrar/ocultar

    private void ToggleHub()
    {
        if (IsVisible && !Dismissing) HideHub();
        else ShowHub(null);
    }

    private void ShowHub(string? folder)
    {
        // A pasta pedida por outro lançamento vira aba (ou ativa a que já existe).
        if (!string.IsNullOrWhiteSpace(folder) &&
            !string.Equals(folder, _folder, StringComparison.OrdinalIgnoreCase))
        {
            AddTab(folder);
        }

        Dismissing = false;
        SearchBox.Clear();

        CenterOnActiveMonitor();
        Show();
        Activate();

        // O WM_HOTKEY dá direito de foreground a este processo, então aqui o
        // SetForegroundWindow funciona (fora dessa janela ele costuma ser ignorado).
        if (Handle != nint.Zero) Native.SetForegroundWindow(Handle);

        SearchBox.Focus();
        MoveTo(0);
        AnimateShellIn();
    }

    private void HideHub()
    {
        Shell.BeginAnimation(OpacityProperty, null);
        Shell.Opacity = 0;
        Hide();
        Dismissing = false;

        TrimWorkingSet();
    }

    /// <summary>
    /// Escondido, o hub fica parado esperando o atalho e não precisa das páginas
    /// residentes. Isso devolve o working set ao sistema; o Windows pagina de
    /// volta o necessário na próxima abertura, que continua instantânea porque a
    /// janela e os ícones seguem montados.
    /// </summary>
    private static void TrimWorkingSet()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Native.SetProcessWorkingSetSizeEx(Native.GetCurrentProcess(), -1, -1, 0);
        }
        catch (Exception error)
        {
            Log.Warn("não consegui devolver memória ao esconder", error);
        }
    }

    /// <summary>Abre no monitor onde o mouse está, não sempre no primário.</summary>
    private void CenterOnActiveMonitor()
    {
        try
        {
            if (!Native.GetCursorPos(out var cursor)) return;

            nint monitor = Native.MonitorFromPoint(cursor, Native.MONITOR_DEFAULTTONEAREST);
            var info = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
            if (!Native.GetMonitorInfo(monitor, ref info)) return;

            // rcWork vem em pixels físicos; a janela raciocina em unidades do WPF.
            var transform = _source?.CompositionTarget?.TransformFromDevice;
            double sx = transform?.M11 ?? 1.0;
            double sy = transform?.M22 ?? 1.0;

            double left = info.rcWork.Left * sx;
            double top = info.rcWork.Top * sy;
            double width = (info.rcWork.Right - info.rcWork.Left) * sx;
            double height = (info.rcWork.Bottom - info.rcWork.Top) * sy;

            Left = left + (width - Width) / 2;
            Top = top + (height - Height) / 2;
        }
        catch
        {
            // posição atual serve
        }
    }

    // ------------------------------------------------------------ menus

    private void ShowTrayMenu()
    {
        if (FindResource("TrayMenu") is not ContextMenu menu) return;

        SyncOptionItems(menu);

        // Sem isso o menu não some ao clicar fora dele.
        if (Handle != nint.Zero) Native.SetForegroundWindow(Handle);

        PlaceAtCursor(menu);
        menu.IsOpen = true;
    }

    private void Header_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindResource("OptionsMenu") is not ContextMenu menu) return;

        SyncOptionItems(menu);
        PlaceAtCursor(menu);
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void PlaceAtCursor(ContextMenu menu)
    {
        menu.Placement = PlacementMode.AbsolutePoint;

        if (Native.GetCursorPos(out var cursor))
        {
            var transform = _source?.CompositionTarget?.TransformFromDevice;
            menu.HorizontalOffset = cursor.X * (transform?.M11 ?? 1.0);
            menu.VerticalOffset = cursor.Y * (transform?.M22 ?? 1.0);
        }
    }

    /// <summary>Marca os itens que refletem estado (residente, iniciar com o Windows).</summary>
    private void SyncOptionItems(ContextMenu menu)
    {
        foreach (var entry in menu.Items.OfType<MenuItem>())
        {
            switch (entry.Tag as string)
            {
                case "resident":
                    entry.IsChecked = App.Background;
                    entry.Header = _hotKeyFailed
                        ? $"Modo residente (atalho {App.Config.HotKey} em uso)"
                        : $"Modo residente — {App.Config.HotKey}";
                    break;

                case "startup":
                    entry.IsChecked = StartupRegistration.IsEnabled;
                    break;
            }
        }
    }

    // ------------------------------------------------------------ ações do menu

    private void ToggleResident_Click(object sender, RoutedEventArgs e)
    {
        bool enable = !App.Background;

        App.Background = enable;
        App.Config.Background = enable;
        App.Config.Save();

        if (enable)
        {
            SingleInstance.TryAcquire();
            EnableResidentMode();
        }
        else
        {
            DisableResidentMode();
        }
    }

    private void ToggleStartup_Click(object sender, RoutedEventArgs e)
    {
        bool enable = !StartupRegistration.IsEnabled;
        StartupRegistration.Set(enable);

        // Iniciar com o Windows só faz sentido residente.
        if (enable && !App.Background) ToggleResident_Click(sender, e);
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        App.Config.Save();
        Launcher.OpenFolder(HubConfig.Directory);
    }

    private void TrayOpen_Click(object sender, RoutedEventArgs e) => ShowHub(null);

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        _reallyExit = true;
        Application.Current.Shutdown();
    }

    private bool _reallyExit;
}
