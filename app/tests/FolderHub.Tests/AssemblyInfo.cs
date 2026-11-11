using System.Runtime.CompilerServices;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FolderHub.Tests;

internal static class TestSetup
{
    /// <summary>
    /// Manda a configuração para um diretório de teste antes de qualquer coisa
    /// tocá-la.
    ///
    /// Os smoke tests abrem janelas de verdade, e janela de verdade grava
    /// configuração ao ser dispensada. Sem isto uma rodada de testes reescreve
    /// o %APPDATA% de quem está desenvolvendo — e foi assim que uma rodada
    /// trocou os três hubs de alguém por um só.
    ///
    /// Precisa ser ModuleInitializer: o HubConfig.Directory é resolvido na
    /// primeira leitura, e um construtor estático de teste chegaria tarde.
    /// </summary>
    [ModuleInitializer]
    internal static void RedirectConfig()
    {
        string dir = Path.Combine(Path.GetTempPath(), "folderhub-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("FOLDERHUB_CONFIG_DIR", dir);
    }
}
