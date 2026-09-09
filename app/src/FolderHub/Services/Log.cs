using System.IO;

namespace FolderHub.Services;

/// <summary>
/// Log em arquivo. O app engole falha de propósito em vários pontos — um atalho
/// quebrado não pode derrubar o hub — mas engolir sem registrar deixa o usuário
/// (e quem abre uma issue) sem nada para investigar.
/// </summary>
public static class Log
{
    private const long MaxBytes = 256 * 1024;

    private static readonly Lock Gate = new();

    public static string FilePath { get; } = Path.Combine(HubConfig.Directory, "folderhub.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? error = null) => Write("WARN", message, error);

    private static void Write(string level, string message, Exception? error)
    {
        try
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level,-4} {message}";
            if (error is not null) line += $"  ::  {error.GetType().Name}: {error.Message}";

            lock (Gate)
            {
                Directory.CreateDirectory(HubConfig.Directory);
                Rotate();
                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Falhar ao registrar uma falha não pode virar uma terceira falha.
        }
    }

    /// <summary>Mantém um arquivo anterior e recomeça, para o log não crescer sem fim.</summary>
    private static void Rotate()
    {
        var current = new FileInfo(FilePath);
        if (!current.Exists || current.Length < MaxBytes) return;

        string previous = FilePath + ".1";
        if (File.Exists(previous)) File.Delete(previous);
        File.Move(FilePath, previous);
    }
}
