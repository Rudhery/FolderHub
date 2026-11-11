using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using FolderHub.Interop;
using FolderHub.Models;

namespace FolderHub.Services;

/// <summary>
/// A ordem manual de cada pasta, guardada num arquivo interno
/// (<c>%APPDATA%\FolderHub\order.json</c>), por nome de arquivo.
///
/// Antes a ordem ia no próprio nome ("01 - Steam.lnk"). Quando um app se
/// atualizava, o instalador recriava "Steam.lnk" sem o prefixo e o hub passava
/// a ter dois Steam — a cada atualização, mais um. Guardando a ordem fora da
/// pasta, o atalho recriado tem o mesmo nome e volta para o mesmo lugar.
/// </summary>
public static partial class ManualOrder
{
    /// <summary>Prefixo que versões antigas gravavam: "01 - ", "002 - " (e "01.", "01_", "01)").</summary>
    [GeneratedRegex(@"^\d{2,3}\s*[-._)\]]\s*", RegexOptions.CultureInvariant)]
    private static partial Regex LegacyPrefix();

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static readonly object Gate = new();
    private static Dictionary<string, List<string>>? _cache;

    public static string FilePath => Path.Combine(HubConfig.Directory, "order.json");

    /// <summary>Chave estável da pasta: caminho completo, sem barra no fim, sem diferença de caixa.</summary>
    private static string Key(string folder)
    {
        try { folder = Path.GetFullPath(folder); } catch { /* fica como veio */ }
        return folder.TrimEnd('\\', '/').ToLowerInvariant();
    }

    private static Dictionary<string, List<string>> Store()
    {
        if (_cache is not null) return _cache;

        var store = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(FilePath))
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(FilePath), Options);
                if (parsed is not null)
                {
                    foreach (var (folder, names) in parsed) store[folder] = names ?? [];
                }
            }
        }
        catch (Exception error)
        {
            Log.Warn($"ordem ilegível em {FilePath}, começando do zero", error);
        }

        return _cache = store;
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(HubConfig.Directory);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_cache, Options));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception error)
        {
            Log.Warn("não consegui salvar a ordem dos atalhos", error);
        }
    }

    /// <summary>A ordem gravada da pasta (nomes de arquivo). Vazia se nunca foi arrumada.</summary>
    public static IReadOnlyList<string> Get(string folder)
    {
        lock (Gate)
        {
            return Store().TryGetValue(Key(folder), out var names) ? [.. names] : [];
        }
    }

    /// <summary>Grava a ordem dos itens. Não toca em arquivo nenhum da pasta.</summary>
    public static void Save(string folder, IEnumerable<AppItem> items) =>
        Save(folder, items.Select(i => i.FileName));

    public static void Save(string folder, IEnumerable<string> fileNames)
    {
        lock (Gate)
        {
            Store()[Key(folder)] = [.. fileNames.Distinct(StringComparer.OrdinalIgnoreCase)];
            Save();
        }
    }

    /// <summary>Esquece o que estava em memória. Só os testes precisam.</summary>
    internal static void ResetCache()
    {
        lock (Gate) _cache = null;
    }

    /// <summary>
    /// Ordena pela ordem gravada. O que não está nela (atalho novo) vai para o
    /// fim, em ordem de nome — é o lugar previsível para algo que acabou de chegar.
    /// </summary>
    public static void Sort(List<AppItem> items, IReadOnlyList<string> order, Comparison<string> byName)
    {
        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < order.Count; i++) rank.TryAdd(order[i], i);

        items.Sort((a, b) =>
        {
            bool hasA = rank.TryGetValue(a.FileName, out int ra);
            bool hasB = rank.TryGetValue(b.FileName, out int rb);
            if (hasA && hasB) return ra.CompareTo(rb);
            if (hasA) return -1;
            if (hasB) return 1;
            return byName(a.Name, b.Name);
        });
    }

    /// <summary>
    /// Tira os prefixos "01 - " que versões antigas gravaram, levando a ordem
    /// deles para o arquivo interno.
    ///
    /// Se o nome limpo já existe, é a duplicata que a atualização de um app
    /// criou: o atalho novo fica (no lugar do antigo) e o antigo vai para a
    /// Lixeira — dá para recuperar. Devolve quantos arquivos mudaram.
    /// </summary>
    public static int MigrateLegacyPrefixes(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return 0;

        var items = FolderScanner.Scan(folder);
        if (!items.Any(i => LegacyPrefix().IsMatch(i.FileName))) return 0;

        lock (Gate)
        {
            // Sem ordem gravada, a ordem antiga era o nome do arquivo com prefixo.
            var stored = Get(folder);
            List<string> order = stored.Count > 0
                ? [.. stored]
                : [.. items.Select(i => i.FileName).OrderBy(n => n, Comparer<string>.Create(FolderScanner.Natural))];

            foreach (var name in items.Select(i => i.FileName))
            {
                if (!order.Contains(name, StringComparer.OrdinalIgnoreCase)) order.Add(name);
            }

            int changed = 0;
            foreach (var item in items)
            {
                string old = item.FileName;
                if (!LegacyPrefix().IsMatch(old)) continue;

                string clean = LegacyPrefix().Replace(Path.GetFileNameWithoutExtension(old), string.Empty).Trim();
                if (clean.Length == 0) continue;
                clean += Path.GetExtension(old);

                string from = item.Path;
                string to = Path.Combine(folder, clean);
                int at = order.FindIndex(n => string.Equals(n, old, StringComparison.OrdinalIgnoreCase));

                try
                {
                    if (File.Exists(to))
                    {
                        if (!Native.Recycle(from)) continue;
                        Log.Info($"duplicata de atualização: {old} foi para a Lixeira, fica {clean}");
                        order.RemoveAll(n => string.Equals(n, clean, StringComparison.OrdinalIgnoreCase));
                        at = order.FindIndex(n => string.Equals(n, old, StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        File.Move(from, to);
                    }

                    if (at >= 0) order[at] = clean;
                    changed++;
                }
                catch (Exception error)
                {
                    Log.Warn($"não consegui tirar o prefixo de {old}", error);
                }
            }

            Save(folder, order);
            return changed;
        }
    }
}
