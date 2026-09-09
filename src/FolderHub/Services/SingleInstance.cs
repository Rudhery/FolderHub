using System.IO;
using FolderHub.Interop;

namespace FolderHub.Services;

/// <summary>
/// Em modo residente só faz sentido um processo: é ele que segura o atalho
/// global. Um segundo lançamento (clique no ícone fixado, por exemplo) apenas
/// pede para o que já está rodando aparecer — opcionalmente em outra pasta.
/// </summary>
public static class SingleInstance
{
    private const string MutexName = @"Local\FolderHub.SingleInstance";

    private static Mutex? _mutex;

    /// <summary>Mensagem própria; o broadcast só é entendido por outro FolderHub.</summary>
    public static uint ShowMessage { get; } = Native.RegisterWindowMessage("FolderHub.Show");

    private static string RequestFile => Path.Combine(HubConfig.Directory, "request.txt");

    private static string WindowFile => Path.Combine(HubConfig.Directory, "instance.hwnd");

    /// <summary>true se este processo é o primeiro (e deve seguir rodando).</summary>
    public static bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out bool created);
            if (!created)
            {
                _mutex.Dispose();
                _mutex = null;
            }
            return created;
        }
        catch
        {
            // Sem mutex, seguir sozinho é melhor do que não abrir.
            return true;
        }
    }

    /// <summary>A instância residente publica sua janela para as outras acharem.</summary>
    public static void PublishWindow(nint hwnd)
    {
        try
        {
            Directory.CreateDirectory(HubConfig.Directory);
            File.WriteAllText(WindowFile, hwnd.ToString());
        }
        catch
        {
            // sem isso, um segundo lançamento simplesmente não faz nada
        }
    }

    public static void ForgetWindow()
    {
        try
        {
            if (File.Exists(WindowFile)) File.Delete(WindowFile);
        }
        catch
        {
            // ignora
        }
    }

    /// <summary>Pede para a instância que já roda aparecer, opcionalmente trocando de pasta.</summary>
    public static bool RequestShow(string? folder)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(HubConfig.Directory);
                File.WriteAllText(RequestFile, folder);
            }
        }
        catch
        {
            // sem o arquivo, a outra instância apenas aparece na pasta atual
        }

        // Mensagem direta na janela publicada: broadcast para o sistema inteiro
        // é caro e nem sempre chega em janela sem barra de tarefas.
        try
        {
            if (!File.Exists(WindowFile)) return false;

            if (!nint.TryParse(File.ReadAllText(WindowFile).Trim(), out nint hwnd)) return false;
            if (hwnd == nint.Zero || !Native.IsWindow(hwnd)) return false;

            return Native.PostMessage(hwnd, ShowMessage, 0, 0);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Lê e consome a pasta pedida por outra instância, se houver.</summary>
    public static string? TakeRequestedFolder()
    {
        try
        {
            if (!File.Exists(RequestFile)) return null;

            string folder = File.ReadAllText(RequestFile).Trim();
            File.Delete(RequestFile);

            return Directory.Exists(folder) ? folder : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Release()
    {
        try
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        catch
        {
            // encerrando de qualquer jeito
        }
        finally
        {
            _mutex = null;
        }
    }
}
