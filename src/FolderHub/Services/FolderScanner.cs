using System.IO;
using System.Text.RegularExpressions;
using FolderHub.Interop;
using FolderHub.Models;

namespace FolderHub.Services;

/// <summary>
/// A pasta é a configuração: lê os atalhos do diretório e devolve os cards.
/// </summary>
public static partial class FolderScanner
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".lnk", ".url", ".exe", ".bat", ".cmd", ".ps1", ".appref-ms", ".msc"
    };

    /// <summary>Prefixo de ordenação manual: "01 - Steam.lnk" aparece como "Steam".</summary>
    [GeneratedRegex(@"^\s*\d{1,3}\s*[-._)\]]\s*", RegexOptions.CultureInvariant)]
    private static partial Regex OrderPrefix();

    /// <summary>Tira o "01 - " do começo do nome, se houver.</summary>
    public static string StripOrderPrefix(string fileNameWithoutExtension)
    {
        string stripped = OrderPrefix().Replace(fileNameWithoutExtension, string.Empty).Trim();
        return stripped.Length == 0 ? fileNameWithoutExtension : stripped;
    }

    public static List<AppItem> Scan(string folder)
    {
        var items = new List<AppItem>();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return items;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return items;
        }

        foreach (var file in files)
        {
            string ext = Path.GetExtension(file);
            if (!Supported.Contains(ext)) continue;

            DateTime modified;
            try
            {
                var info = new FileInfo(file);
                if (info.Attributes.HasFlag(FileAttributes.Hidden) ||
                    info.Attributes.HasFlag(FileAttributes.System)) continue;
                modified = info.LastWriteTimeUtc;
            }
            catch
            {
                continue;
            }

            items.Add(new AppItem
            {
                Name = StripOrderPrefix(Path.GetFileNameWithoutExtension(file)),
                Path = file,
                Extension = ext.ToLowerInvariant(),
                Modified = modified
            });
        }

        return items;
    }

    public static void Sort(List<AppItem> items, SortMode mode)
    {
        switch (mode)
        {
            case SortMode.NameAsc:
                items.Sort((a, b) => Natural(a.Name, b.Name));
                break;

            case SortMode.NameDesc:
                items.Sort((a, b) => Natural(b.Name, a.Name));
                break;

            case SortMode.Recent:
                items.Sort((a, b) =>
                {
                    int byDate = b.Modified.CompareTo(a.Modified);
                    return byDate != 0 ? byDate : Natural(a.Name, b.Name);
                });
                break;

            default:
                // Nome do arquivo com o prefixo: é assim que "01 - ", "02 - " mandam.
                items.Sort((a, b) => Natural(a.FileName, b.FileName));
                break;
        }
    }

    /// <summary>Ordenação natural, igual à do Explorer ("item2" antes de "item10").</summary>
    private static int Natural(string a, string b)
    {
        try { return Native.StrCmpLogical(a, b); }
        catch { return string.Compare(a, b, StringComparison.OrdinalIgnoreCase); }
    }
}
