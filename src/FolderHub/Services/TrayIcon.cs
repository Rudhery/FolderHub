using FolderHub.Interop;

namespace FolderHub.Services;

/// <summary>
/// Ícone na área de notificação, direto no Shell_NotifyIcon — sem trazer o
/// WinForms junto só por causa do NotifyIcon. O menu que aparece é o
/// ContextMenu do WPF, então ele mantém o visual do resto do app.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private Native.NOTIFYICONDATA _data;
    private nint _icon;
    private bool _added;

    public bool Add(nint hwnd, string tooltip)
    {
        if (_added) return true;

        _icon = LoadAppIcon();

        _data = new Native.NOTIFYICONDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.NOTIFYICONDATA>(),
            hWnd = hwnd,
            uID = 1,
            uFlags = Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP,
            uCallbackMessage = Native.WM_TRAYICON,
            hIcon = _icon,
            szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };

        _added = Native.Shell_NotifyIcon(Native.NIM_ADD, ref _data);
        return _added;
    }

    public void UpdateTooltip(string tooltip)
    {
        if (!_added) return;

        _data.szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip;
        _data.uFlags = Native.NIF_TIP;
        Native.Shell_NotifyIcon(Native.NIM_MODIFY, ref _data);
        _data.uFlags = Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP;
    }

    /// <summary>Ícone do próprio executável, no tamanho que a bandeja pede.</summary>
    private static nint LoadAppIcon()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return nint.Zero;

            int size = Native.GetSystemMetrics(Native.SM_CXSMICON);
            if (size <= 0) size = 16;

            var handles = new nint[1];
            var ids = new int[1];
            if (Native.PrivateExtractIcons(exe, 0, size, size, handles, ids, 1, 0) > 0)
            {
                return handles[0];
            }
        }
        catch
        {
            // sem ícone é melhor que sem bandeja
        }

        return nint.Zero;
    }

    public void Dispose()
    {
        if (_added)
        {
            Native.Shell_NotifyIcon(Native.NIM_DELETE, ref _data);
            _added = false;
        }

        if (_icon != nint.Zero)
        {
            Native.DestroyIcon(_icon);
            _icon = nint.Zero;
        }
    }
}
