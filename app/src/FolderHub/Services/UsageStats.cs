using System.IO;
using System.Text.Json;
using FolderHub.Models;

namespace FolderHub.Services;

/// <summary>Um atalho de "Mais usados", com o texto que vai embaixo do nome.</summary>
public sealed record FrequentItem(AppItem Item, int Week, int Month)
{
    public string Meta => Week > 0 ? $"{Week}× na semana" : $"{Month}× no mês";
}

/// <summary>
/// Quantas vezes cada atalho foi aberto, para a fileira "Mais usados".
///
/// Fica em <c>%APPDATA%\FolderHub\usage.json</c>, só neste PC, ao lado da
/// config e da ordem. Guarda o horário de cada abertura (no máximo
/// <see cref="Keep"/> por atalho, nenhuma com mais de 30 dias): uma contagem
/// simples nunca esqueceria o app que você usou muito no mês passado.
/// </summary>
public static class UsageStats
{
    private const int Keep = 60;
    private static readonly TimeSpan Window = TimeSpan.FromDays(30);
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };
    private static readonly object Gate = new();
    private static Dictionary<string, List<long>>? _cache;

    public static string FilePath => Path.Combine(HubConfig.Directory, "usage.json");

    private static string Key(string path)
    {
        try { path = Path.GetFullPath(path); } catch { /* fica como veio */ }
        return path.ToLowerInvariant();
    }

    private static Dictionary<string, List<long>> Store()
    {
        if (_cache is not null) return _cache;

        var store = new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(FilePath) &&
                JsonSerializer.Deserialize<Dictionary<string, List<long>>>(File.ReadAllText(FilePath), Options) is { } parsed)
            {
                foreach (var (path, stamps) in parsed) store[path] = stamps ?? [];
            }
        }
        catch (Exception error)
        {
            Log.Warn($"contagem de uso ilegível em {FilePath}, começando do zero", error);
        }

        return _cache = store;
    }

    /// <summary>Conta uma abertura. Nunca lança: contar é detalhe, abrir o app é o que importa.</summary>
    public static void Record(string path, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        lock (Gate)
        {
            try
            {
                var store = Store();
                string key = Key(path);
                if (!store.TryGetValue(key, out var stamps)) store[key] = stamps = [];

                stamps.Add(now.ToUnixTimeSeconds());
                long oldest = (now - Window).ToUnixTimeSeconds();
                stamps.RemoveAll(s => s < oldest);
                if (stamps.Count > Keep) stamps.RemoveRange(0, stamps.Count - Keep);

                // Atalho que saiu da janela de 30 dias não precisa ficar no arquivo.
                foreach (var stale in store.Where(p => p.Value.All(s => s < oldest)).Select(p => p.Key).ToList())
                {
                    store.Remove(stale);
                }

                Directory.CreateDirectory(HubConfig.Directory);
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(store, Options));
                File.Move(temp, FilePath, overwrite: true);
            }
            catch (Exception error)
            {
                Log.Warn("não consegui gravar a contagem de uso", error);
            }
        }
    }

    /// <summary>
    /// Os <paramref name="count"/> atalhos mais abertos nos últimos 30 dias,
    /// entre os <paramref name="items"/> da pasta. Empate vai para o mais recente.
    /// </summary>
    public static IReadOnlyList<FrequentItem> Top(IEnumerable<AppItem> items, int count, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        long month = (now - Window).ToUnixTimeSeconds();
        long week = (now - Week).ToUnixTimeSeconds();

        lock (Gate)
        {
            var store = Store();
            return [.. items
                .Select(item =>
                {
                    var stamps = store.TryGetValue(Key(item.Path), out var s) ? s : [];
                    return (Item: item,
                            Month: stamps.Count(t => t >= month),
                            Week: stamps.Count(t => t >= week),
                            Last: stamps.Count > 0 ? stamps.Max() : 0);
                })
                .Where(x => x.Month > 0)
                .OrderByDescending(x => x.Month)
                .ThenByDescending(x => x.Last)
                .Take(count)
                .Select(x => new FrequentItem(x.Item, x.Week, x.Month))];
        }
    }

    /// <summary>Esquece o que estava em memória. Só os testes precisam.</summary>
    internal static void ResetCache()
    {
        lock (Gate) _cache = null;
    }
}
