using Microsoft.Win32;

namespace FolderHub.Services;

/// <summary>
/// Liga/desliga "iniciar com o Windows" na chave Run do usuário — só HKCU,
/// nada de escrever em área de máquina nem pedir elevação.
/// </summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "FolderHub";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string value && value.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return false;

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;

            key.SetValue(ValueName, $"\"{exe}\" --background", RegistryValueKind.String);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
