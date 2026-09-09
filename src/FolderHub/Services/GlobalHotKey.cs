using System.Windows.Input;
using FolderHub.Interop;

namespace FolderHub.Services;

/// <summary>
/// Atalho global do Windows (RegisterHotKey). Aceita textos como
/// "Ctrl+Alt+Space", "Alt+Q", "Win+Shift+H".
/// </summary>
public sealed class GlobalHotKey : IDisposable
{
    private const int HotKeyId = 0xB0B0;

    private nint _hwnd;
    private bool _registered;

    /// <summary>Texto que foi de fato registrado, para mostrar no menu.</summary>
    public string? Description { get; private set; }

    public static bool TryParse(string? spec, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;

        if (string.IsNullOrWhiteSpace(spec)) return false;

        foreach (string rawPart in spec.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            string part = rawPart.Trim();

            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= Native.MOD_CONTROL; continue;
                case "alt": modifiers |= Native.MOD_ALT; continue;
                case "shift": modifiers |= Native.MOD_SHIFT; continue;
                case "win" or "windows" or "meta": modifiers |= Native.MOD_WIN; continue;
            }

            if (key != 0) return false; // duas teclas comuns não faz sentido

            if (!TryParseKey(part, out key)) return false;
        }

        // O Windows exige pelo menos um modificador, senão o atalho engoliria
        // a tecla no sistema inteiro.
        return modifiers != 0 && key != 0;
    }

    private static bool TryParseKey(string name, out uint vk)
    {
        vk = 0;

        string normalized = name.ToLowerInvariant() switch
        {
            "esc" => "Escape",
            "enter" or "return" => "Return",
            "space" or "espaco" or "espaço" => "Space",
            "ins" => "Insert",
            "del" => "Delete",
            "pgup" => "PageUp",
            "pgdn" or "pgdown" => "PageDown",
            _ => name
        };

        // "1" é Key.D1 no WPF
        if (normalized.Length == 1 && char.IsDigit(normalized[0])) normalized = "D" + normalized;

        if (!Enum.TryParse(normalized, ignoreCase: true, out Key parsed) || parsed == Key.None) return false;

        int converted = KeyInterop.VirtualKeyFromKey(parsed);
        if (converted == 0) return false;

        vk = (uint)converted;
        return true;
    }

    /// <summary>Registra o atalho. Devolve false se o texto for inválido ou a combinação já estiver em uso.</summary>
    public bool Register(nint hwnd, string? spec)
    {
        Unregister();

        if (!TryParse(spec, out uint modifiers, out uint key)) return false;

        _hwnd = hwnd;
        _registered = Native.RegisterHotKey(hwnd, HotKeyId, modifiers | Native.MOD_NOREPEAT, key);
        Description = _registered ? spec : null;

        return _registered;
    }

    public static bool IsHotKeyMessage(int message, nint wParam)
        => message == Native.WM_HOTKEY && wParam == HotKeyId;

    public void Unregister()
    {
        if (!_registered) return;

        Native.UnregisterHotKey(_hwnd, HotKeyId);
        _registered = false;
        Description = null;
    }

    public void Dispose() => Unregister();
}
