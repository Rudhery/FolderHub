namespace FolderHub.Tests;

/// <summary>Pasta descartável para os testes que tocam em arquivo de verdade.</summary>
public sealed class TempFolder : IDisposable
{
    public string Path { get; }

    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "folderhub-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public void Add(params string[] names)
    {
        foreach (string name in names)
        {
            File.WriteAllText(System.IO.Path.Combine(Path, name), "shortcut");
        }
    }

    public string[] Files() =>
        [.. Directory.GetFiles(Path).Select(System.IO.Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch { /* o SO limpa o temp depois */ }
    }
}
