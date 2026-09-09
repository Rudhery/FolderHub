using System.IO;
using System.Windows.Media;

namespace FolderHub.Models;

/// <summary>
/// Uma aba do hub: uma pasta com sua própria lista, seu próprio cache de ícones
/// e seu próprio estado de carregamento.
///
/// A divisão que torna o lazy loading possível: <see cref="Count"/> vem de uma
/// contagem barata (só olhar a extensão dos nomes) feita para todas as abas na
/// abertura, porque é ela que dimensiona a janela. Já a lista completa e,
/// principalmente, a extração dos ícones — que é o que custa caro — só acontece
/// quando a aba é aberta pela primeira vez.
/// </summary>
public sealed class HubTab
{
    public required string Path { get; set; }

    /// <summary>Rótulo da aba. Vazio na config = nome da pasta.</summary>
    public string? CustomName { get; set; }

    public string Name => string.IsNullOrWhiteSpace(CustomName) ? FolderName : CustomName!;

    public string FolderName
    {
        get
        {
            try
            {
                string name = new DirectoryInfo(Path).Name;
                return string.IsNullOrWhiteSpace(name) ? Path : name;
            }
            catch
            {
                return Path;
            }
        }
    }

    /// <summary>Itens da aba. Fica vazia até a aba ser aberta.</summary>
    public List<AppItem> Items { get; } = [];

    /// <summary>Ícones já extraídos, por caminho de arquivo.</summary>
    public Dictionary<string, ImageSource> Icons { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Quantos atalhos a pasta tem, pela contagem barata da abertura.</summary>
    public int Count { get; set; }

    /// <summary>Já foi aberta alguma vez? Enquanto não, nenhum ícone foi extraído.</summary>
    public bool Visited { get; set; }

    public bool Exists
    {
        get
        {
            try { return Directory.Exists(Path); }
            catch { return false; }
        }
    }
}
