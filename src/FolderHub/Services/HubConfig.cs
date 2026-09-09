using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FolderHub.Services;

public sealed class HubConfig
{
    /// <summary>Pasta que vira o hub. É a única "configuração" que importa.</summary>
    public string? FolderPath { get; set; }

    /// <summary>Fecha o hub depois de abrir um app (comportamento de launcher).</summary>
    public bool CloseAfterLaunch { get; set; } = true;

    /// <summary>Fecha o hub quando ele perde o foco.</summary>
    public bool CloseOnBlur { get; set; }

    /// <summary>Fica residente na bandeja ouvindo o atalho global.</summary>
    public bool Background { get; set; }

    /// <summary>Atalho global. Ex.: "Ctrl+Alt+Space", "Alt+Q", "Win+Shift+H".</summary>
    public string HotKey { get; set; } = "Ctrl+Alt+Space";

    /// <summary>Como os cards são ordenados. "Manual" segue os prefixos da pasta.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SortMode Sort { get; set; } = SortMode.Manual;

    /// <summary>Máximo de colunas do grid.</summary>
    public int MaxColumns { get; set; } = 7;

    // ------------------------------------------------------------------

    [JsonIgnore]
    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FolderHub");

    [JsonIgnore]
    public static string FilePath { get; } = Path.Combine(Directory, "config.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static HubConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var cfg = JsonSerializer.Deserialize<HubConfig>(File.ReadAllText(FilePath), Options);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // config corrompida: recomeça do zero em vez de travar o app
        }

        return new HubConfig();
    }

    public void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch
        {
            // salvar config nunca deve derrubar o hub
        }
    }
}
