using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using FolderHub.Interop;

namespace FolderHub.Services;

/// <summary>Acrílico do Windows 11, cantos arredondados e barra de título escura.</summary>
public static class WindowEffects
{
    /// <summary>Aplica o backdrop nativo. Retorna false se o SO não suportar (aí o fundo sólido assume).</summary>
    public static bool ApplyAcrylic(Window window)
    {
        var helper = new WindowInteropHelper(window);
        nint hwnd = helper.Handle;
        if (hwnd == nint.Zero) return false;

        // Deixa a superfície do WPF realmente transparente para o DWM compor o acrílico atrás.
        if (HwndSource.FromHwnd(hwnd) is { } source && source.CompositionTarget != null)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        int dark = 1;
        Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        int corner = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        int backdrop = Native.DWMSBT_TRANSIENTWINDOW;
        int hr = Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

        return hr == 0;
    }
}
