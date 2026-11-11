using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
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
public sealed class HubTab : INotifyPropertyChanged
{
    public required string Path { get; set; }

    private string? _customName;
    private int _count;

    /// <summary>Rótulo da aba. Vazio na config = nome da pasta.</summary>
    public string? CustomName
    {
        get => _customName;
        set
        {
            if (_customName == value) return;
            _customName = value;
            Notify();
            Notify(nameof(Name));
            Notify(nameof(Initial));
            Notify(nameof(Hint));
        }
    }

    public string Name => string.IsNullOrWhiteSpace(CustomName) ? FolderName : CustomName!;

    /// <summary>A letra no quadradinho da barra lateral.</summary>
    public string Initial
    {
        get
        {
            foreach (var c in Name)
            {
                if (char.IsLetterOrDigit(c)) return char.ToUpperInvariant(c).ToString();
            }
            return "•";
        }
    }

    private Brush? _swatch;

    /// <summary>A cor do hub na barra lateral. Quem dá é a posição (<see cref="Services.HubPalette"/>).</summary>
    public Brush? Swatch
    {
        get => _swatch;
        set
        {
            if (ReferenceEquals(_swatch, value)) return;
            _swatch = value;
            Notify();
        }
    }

    /// <summary>A dica da barra lateral: nome, caminho e quantos atalhos.</summary>
    public string Hint => $"{Name} · {Count} {(Count == 1 ? "atalho" : "atalhos")}\n{ShortPath}";

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

    /// <summary>
    /// O caminho como se lê, com a pasta do usuário virando "~". O caminho
    /// inteiro não cabe numa linha de lista e o começo dele é sempre igual.
    /// </summary>
    public string ShortPath => Services.PathDisplay.Shorten(Path);

    /// <summary>Itens da aba. Fica vazia até a aba ser aberta.</summary>
    public List<AppItem> Items { get; } = [];

    /// <summary>Ícones já extraídos, por caminho de arquivo.</summary>
    public Dictionary<string, ImageSource> Icons { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Quantos atalhos a pasta tem, pela contagem barata da abertura.
    ///
    /// Avisa a tela porque a pílula da aba mostra este número: a contagem barata
    /// pode divergir da real, e é ao abrir a aba que ela é corrigida.
    /// </summary>
    public int Count
    {
        get => _count;
        set
        {
            if (_count == value) return;
            _count = value;
            Notify();
            Notify(nameof(Hint));
        }
    }

    /// <summary>Já foi aberta alguma vez? Enquanto não, nenhum ícone foi extraído.</summary>
    public bool Visited { get; set; }

    private string _slot = string.Empty;

    /// <summary>
    /// Qual aba ela é na faixa — "Tab 1", "Tab 2". Só a tela de configuração
    /// usa: dentro do hub a posição é visível, na lista de ajustes não.
    /// </summary>
    public string Slot
    {
        get => _slot;
        set
        {
            if (_slot == value) return;
            _slot = value;
            Notify();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    public bool Exists
    {
        get
        {
            try { return Directory.Exists(Path); }
            catch { return false; }
        }
    }
}
