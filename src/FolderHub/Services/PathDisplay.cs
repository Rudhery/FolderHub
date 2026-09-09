using System.IO;

namespace FolderHub.Services;

/// <summary>Encurtar caminho para caber no cabeçalho sem virar sopa de letras.</summary>
public static class PathDisplay
{
    private const int Comfortable = 46;

    public static string Shorten(string? path, string? home = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (!string.IsNullOrEmpty(home) && path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
        {
            path = "~" + path[home.Length..];
        }

        if (path.Length <= Comfortable) return path;

        var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 3) return path;

        // primeiro pedaço + as duas últimas pastas, que é o que localiza a pessoa
        return $"{parts[0]}{Path.DirectorySeparatorChar}…{Path.DirectorySeparatorChar}" +
               string.Join(Path.DirectorySeparatorChar, parts[^2..]);
    }

    /// <summary>Nome que vai no título: a pasta em si.</summary>
    public static string FolderName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "Hub";

        try
        {
            string name = new DirectoryInfo(path).Name;
            return string.IsNullOrWhiteSpace(name) ? path : name;
        }
        catch
        {
            return path;
        }
    }
}
