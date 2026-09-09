using System.IO;
using System.Runtime.InteropServices;
using FolderHub.Interop;

namespace FolderHub.Services;

/// <summary>
/// Arrastar algo para dentro do hub adiciona na pasta: atalhos são copiados,
/// qualquer outra coisa vira um .lnk apontando para o original.
/// </summary>
public static class ShortcutWriter
{
    public static int AddToFolder(IEnumerable<string> paths, string folder)
    {
        int added = 0;

        foreach (string path in paths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    if (CreateLink(path, folder)) added++;
                    continue;
                }

                if (!File.Exists(path)) continue;

                string ext = Path.GetExtension(path);
                if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".url", StringComparison.OrdinalIgnoreCase))
                {
                    string destination = UniquePath(folder, Path.GetFileName(path));
                    File.Copy(path, destination);
                    added++;
                }
                else if (CreateLink(path, folder))
                {
                    added++;
                }
            }
            catch
            {
                // um item problemático não impede os outros
            }
        }

        return added;
    }

    private static bool CreateLink(string target, string folder)
    {
        object? comObj = null;
        try
        {
            comObj = new Native.ShellLink();
            var link = (Native.IShellLinkW)comObj;

            link.SetPath(target);

            string? workingDir = Directory.Exists(target) ? target : Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(workingDir)) link.SetWorkingDirectory(workingDir);

            string name = Directory.Exists(target)
                ? new DirectoryInfo(target).Name
                : Path.GetFileNameWithoutExtension(target);

            string destination = UniquePath(folder, name + ".lnk");
            ((Native.IPersistFile)comObj).Save(destination, true);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (comObj != null && Marshal.IsComObject(comObj)) Marshal.FinalReleaseComObject(comObj);
        }
    }

    private static string UniquePath(string folder, string fileName)
    {
        string candidate = Path.Combine(folder, fileName);
        if (!File.Exists(candidate)) return candidate;

        string stem = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);

        for (int i = 2; i < 1000; i++)
        {
            candidate = Path.Combine(folder, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }

        return Path.Combine(folder, $"{stem} ({Guid.NewGuid():N}){ext}");
    }
}
