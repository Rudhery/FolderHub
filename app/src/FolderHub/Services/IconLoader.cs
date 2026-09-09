using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FolderHub.Interop;

namespace FolderHub.Services;

/// <summary>
/// Extrai o ícone de melhor qualidade possível para cada atalho.
/// Ordem: ícone declarado no .lnk/.url -> ícone do alvo -> ícone do shell -> null.
/// </summary>
public static class IconLoader
{
    /// <summary>
    /// O card desenha o ícone em 28px lógicos — 56px a 200% de DPI. Guardar 256
    /// gastava 256 KB por atalho (200 MB numa pasta de 800) para jogar 95% dos
    /// pixels fora na hora de desenhar. 96 dá folga até 300% de DPI por 36 KB.
    /// </summary>
    private const int Size = 96;

    public static ImageSource? Load(string path)
    {
        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();

            ImageSource? img = ext switch
            {
                ".lnk" => FromShortcut(path),
                ".url" => FromUrlFile(path),
                _ => null
            };

            img ??= FromShellItem(path);
            return img;
        }
        catch
        {
            return null;
        }
    }

    // ---------------------------------------------------------------- .lnk

    private static ImageSource? FromShortcut(string lnkPath)
    {
        object? comObj = null;
        try
        {
            comObj = new Native.ShellLink();
            var link = (Native.IShellLinkW)comObj;
            ((Native.IPersistFile)comObj).Load(lnkPath, 0);

            // 1) ícone explícito definido no atalho
            var iconPath = new StringBuilder(1024);
            link.GetIconLocation(iconPath, iconPath.Capacity, out int index);
            string icon = Environment.ExpandEnvironmentVariables(iconPath.ToString());
            if (!string.IsNullOrWhiteSpace(icon) && File.Exists(icon))
            {
                var img = FromIconFile(icon, index);
                if (img != null) return img;
            }

            // 2) ícone do executável alvo (evita a setinha de atalho sobreposta)
            var target = new StringBuilder(1024);
            link.GetPath(target, target.Capacity, nint.Zero, 0);
            string targetPath = Environment.ExpandEnvironmentVariables(target.ToString());
            if (!string.IsNullOrWhiteSpace(targetPath) && (File.Exists(targetPath) || Directory.Exists(targetPath)))
            {
                var img = FromShellItem(targetPath);
                if (img != null) return img;
            }
        }
        catch
        {
            // cai no fallback do shell
        }
        finally
        {
            if (comObj != null && Marshal.IsComObject(comObj)) Marshal.FinalReleaseComObject(comObj);
        }

        return null;
    }

    // ---------------------------------------------------------------- .url

    private static ImageSource? FromUrlFile(string urlPath)
    {
        try
        {
            string? iconFile = null;
            int iconIndex = 0;

            foreach (string line in File.ReadLines(urlPath))
            {
                if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                    iconFile = line[9..].Trim();
                else if (line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                    _ = int.TryParse(line[10..].Trim(), out iconIndex);
            }

            if (!string.IsNullOrWhiteSpace(iconFile))
            {
                iconFile = Environment.ExpandEnvironmentVariables(iconFile);
                if (File.Exists(iconFile)) return FromIconFile(iconFile, iconIndex);
            }
        }
        catch
        {
            // ignora
        }

        return null;
    }

    // ---------------------------------------------------------------- shell

    private static ImageSource? FromShellItem(string path)
    {
        object? shellItem = null;
        nint hbmp = nint.Zero;
        try
        {
            Native.SHCreateItemFromParsingName(path, nint.Zero, Native.IID_IShellItemImageFactory, out shellItem);
            if (shellItem is not Native.IShellItemImageFactory factory) return null;

            var size = new Native.SIZE { cx = Size, cy = Size };
            int hr = factory.GetImage(size, Native.SIIGBF_ICONONLY | Native.SIIGBF_SCALEUP, out hbmp);
            if (hr != 0 || hbmp == nint.Zero) return null;

            return FromHBitmap(hbmp);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hbmp != nint.Zero) Native.DeleteObject(hbmp);
            if (shellItem != null && Marshal.IsComObject(shellItem)) Marshal.FinalReleaseComObject(shellItem);
        }
    }

    private static ImageSource? FromIconFile(string file, int index)
    {
        // .ico solto: deixa o WPF escolher o melhor frame
        if (Path.GetExtension(file).Equals(".ico", StringComparison.OrdinalIgnoreCase))
        {
            var direct = FromIcoFile(file);
            if (direct != null) return direct;
        }

        foreach (int wanted in (int[])[Size, 64, 48, 32])
        {
            var handles = new nint[1];
            var ids = new int[1];
            int count;
            try
            {
                count = Native.PrivateExtractIcons(file, index, wanted, wanted, handles, ids, 1, 0);
            }
            catch
            {
                return null;
            }

            if (count > 0 && handles[0] != nint.Zero)
            {
                try
                {
                    var src = Imaging.CreateBitmapSourceFromHIcon(handles[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    return src;
                }
                catch
                {
                    return null;
                }
                finally
                {
                    Native.DestroyIcon(handles[0]);
                }
            }
        }

        return null;
    }

    private static ImageSource? FromIcoFile(string file)
    {
        try
        {
            var decoder = new IconBitmapDecoder(
                new Uri(file, UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            BitmapFrame? best = null;
            foreach (var frame in decoder.Frames)
            {
                if (best == null || frame.PixelWidth > best.PixelWidth) best = frame;
            }

            if (best == null) return null;
            if (best.CanFreeze) best.Freeze();
            return best;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>HBITMAP (PARGB de 32bpp vindo do shell) -> BitmapSource congelado.</summary>
    private static ImageSource? FromHBitmap(nint hbmp)
    {
        var bm = default(Native.BITMAP);
        if (Native.GetObject(hbmp, Marshal.SizeOf<Native.BITMAP>(), ref bm) == 0) return null;

        int w = bm.bmWidth;
        int h = Math.Abs(bm.bmHeight);
        if (w <= 0 || h <= 0 || (long)w * h > 4096L * 4096L) return null;

        int stride = w * 4;
        var buffer = new byte[stride * h];

        var info = default(Native.BITMAPINFO);
        info.bmiHeader.biSize = Marshal.SizeOf<Native.BITMAPINFOHEADER>();
        info.bmiHeader.biWidth = w;
        info.bmiHeader.biHeight = -h; // top-down
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = 0; // BI_RGB

        nint hdc = Native.GetDC(nint.Zero);
        int lines;
        try
        {
            lines = Native.GetDIBits(hdc, hbmp, 0, (uint)h, buffer, ref info, 0);
        }
        finally
        {
            Native.ReleaseDC(nint.Zero, hdc);
        }

        if (lines == 0) return null;

        // Alguns ícones antigos voltam com o canal alfa zerado: nesse caso tratamos como opaco.
        bool hasAlpha = false;
        for (int i = 3; i < buffer.Length; i += 4)
        {
            if (buffer[i] != 0) { hasAlpha = true; break; }
        }
        if (!hasAlpha)
        {
            for (int i = 3; i < buffer.Length; i += 4) buffer[i] = 255;
        }

        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, buffer, stride);
        bitmap.Freeze();
        return bitmap;
    }
}
