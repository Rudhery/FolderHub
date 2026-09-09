using System.IO;
using FolderHub.Models;

namespace FolderHub.Services;

/// <summary>
/// Grava a ordem manual na própria pasta, renomeando os arquivos com o prefixo
/// "01 - ", "02 - "… — o mesmo prefixo que o scanner esconde na exibição.
/// Assim a ordem continua visível no Explorer e sobrevive a qualquer config.
/// </summary>
public static class ManualOrder
{
    /// <summary>
    /// Renomeia os arquivos para refletir <paramref name="items"/>.
    /// Devolve os pares (de, para) efetivamente renomeados — vazio se já estava na ordem.
    /// </summary>
    public static IReadOnlyList<(string From, string To)> Apply(IReadOnlyList<AppItem> items)
    {
        int width = items.Count >= 100 ? 3 : 2;

        var planned = new List<(string From, string To)>();
        for (int i = 0; i < items.Count; i++)
        {
            string path = items[i].Path;
            string dir = Path.GetDirectoryName(path) ?? string.Empty;
            string ext = Path.GetExtension(path);
            string bare = FolderScanner.StripOrderPrefix(Path.GetFileNameWithoutExtension(path));

            string target = Path.Combine(dir, $"{(i + 1).ToString().PadLeft(width, '0')} - {bare}{ext}");
            if (!string.Equals(target, path, StringComparison.OrdinalIgnoreCase))
            {
                planned.Add((path, target));
            }
        }

        if (planned.Count == 0) return [];

        // Duas fases: o nome final de um arquivo pode ser o nome atual de outro.
        var staged = new List<(string Temp, string To, string From)>();
        try
        {
            foreach (var (from, to) in planned)
            {
                string temp = from + ".fhtmp";
                File.Move(from, temp);
                staged.Add((temp, to, from));
            }

            foreach (var (temp, to, _) in staged)
            {
                File.Move(temp, to);
            }

            return planned;
        }
        catch
        {
            // Devolve ao estado original tudo que ainda estiver em nome temporário.
            foreach (var (temp, _, from) in staged)
            {
                try
                {
                    if (File.Exists(temp)) File.Move(temp, from);
                }
                catch
                {
                    // um arquivo travado não deve impedir os outros de voltarem
                }
            }

            return [];
        }
    }
}
