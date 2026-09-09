using System.Diagnostics;
using System.IO;
using FolderHub.Models;

namespace FolderHub.Services;

public static class Launcher
{
    public static bool Launch(AppItem item)
    {
        try
        {
            var psi = new ProcessStartInfo(item.Path) { UseShellExecute = true };

            // .lnk e .url carregam o próprio diretório de trabalho; para os demais,
            // usar a pasta do arquivo evita que o app abra com o cwd do hub.
            if (item.Extension is not (".lnk" or ".url"))
            {
                string? dir = Path.GetDirectoryName(item.Path);
                if (!string.IsNullOrEmpty(dir)) psi.WorkingDirectory = dir;
            }

            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void RevealInExplorer(AppItem item)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Path}\"") { UseShellExecute = true });
        }
        catch
        {
            // ignora
        }
    }

    public static void OpenFolder(string folder)
    {
        try
        {
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch
        {
            // ignora
        }
    }

    public static void RunAsAdmin(AppItem item)
    {
        try
        {
            Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true, Verb = "runas" });
        }
        catch
        {
            // usuário cancelou o UAC
        }
    }
}
