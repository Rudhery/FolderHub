using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace FolderHub.Models;

public sealed class AppItem : INotifyPropertyChanged
{
    private ImageSource? _icon;

    public required string Name { get; init; }

    /// <summary>Caminho do arquivo que será executado (o próprio atalho).</summary>
    public required string Path { get; init; }

    public required string Extension { get; init; }

    /// <summary>Última escrita, para a ordenação "mais recentes".</summary>
    public required DateTime Modified { get; init; }

    /// <summary>Nome do arquivo com o prefixo "01 - " — é ele que define a ordem manual.</summary>
    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>Rótulo curto do tipo, mostrado no card no hover.</summary>
    public string Kind => Extension switch
    {
        ".lnk" => "atalho",
        ".url" => "link",
        ".exe" => "app",
        ".bat" or ".cmd" or ".ps1" => "script",
        ".appref-ms" => "app",
        ".msc" => "console",
        _ => Extension.TrimStart('.')
    };

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (ReferenceEquals(_icon, value)) return;
            _icon = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Inicial usada no tile de fallback enquanto (ou caso) não houver ícone.</summary>
    public string Initial
    {
        get
        {
            foreach (var c in Name)
            {
                if (char.IsLetterOrDigit(c)) return char.ToUpperInvariant(c).ToString();
            }
            return "?";
        }
    }

    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
