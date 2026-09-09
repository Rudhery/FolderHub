using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FolderHub.Services;

/// <summary>Uma aba na configuração: uma pasta, com rótulo opcional.</summary>
public sealed class TabConfig
{
    public string Path { get; set; } = string.Empty;

    /// <summary>Rótulo da aba. Vazio = nome da pasta.</summary>
    public string? Name { get; set; }
}

public sealed class HubConfig
{
    /// <summary>
    /// As pastas do hub, uma por aba. É a configuração que importa.
    /// </summary>
    public List<TabConfig> Tabs { get; set; } = [];

    /// <summary>
    /// Config antiga, de pasta única. Fica só para migrar quem já tinha o app;
    /// o <see cref="Migrate"/> move o valor para <see cref="Tabs"/> e zera aqui.
    /// </summary>
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

    /// <summary>
    /// Caminho de um .xaml com um ResourceDictionary. Ele é carregado depois do
    /// tema embutido, então qualquer chave que definir vence — é assim que dá
    /// para trocar cor e tamanho sem recompilar.
    /// </summary>
    public string? ThemeFile { get; set; }

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

    /// <summary>
    /// Disparado depois de gravar. É como uma tela de configuração avisa o hub
    /// sem precisar conhecê-lo — e sem exigir reiniciar o app para valer.
    /// </summary>
    public static event Action? Changed;

    public static HubConfig Load()
    {
        if (TryLoad(out var config, out var error))
        {
            // Grava na hora: senão o formato antigo fica no arquivo para sempre
            // e a migração roda de novo a cada abertura.
            if (config.Migrate()) config.Save();
            return config;
        }

        if (error is not null) Log.Warn($"config ilegível em {FilePath}, usando os padrões", error);

        return new HubConfig();
    }

    /// <summary>
    /// Lê a config dizendo se conseguiu. Existe separado do <see cref="Load"/>
    /// por causa do vigia de arquivo: ler no meio de uma gravação falha o parse,
    /// e trocar a config por padrões nesse instante apagaria as abas de quem
    /// estiver usando. Quem observa o disco precisa poder simplesmente desistir.
    /// </summary>
    public static bool TryLoad(out HubConfig config, out Exception? error)
    {
        config = new HubConfig();
        error = null;

        try
        {
            if (!File.Exists(FilePath)) return false;

            string text = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parsed = JsonSerializer.Deserialize<HubConfig>(text, Options);
            if (parsed is null) return false;

            config = parsed;
            return true;
        }
        catch (Exception failure)
        {
            error = failure;
            return false;
        }
    }

    /// <summary>
    /// Traz a config antiga de pasta única para o formato de abas.
    /// Devolve true se mudou alguma coisa e vale a pena regravar.
    /// </summary>
    public bool Migrate()
    {
        Tabs ??= [];

        bool changed = false;

        if (!string.IsNullOrWhiteSpace(FolderPath))
        {
            if (Tabs.Count == 0) Tabs.Add(new TabConfig { Path = FolderPath });
            FolderPath = null;
            changed = true;
        }

        changed |= Tabs.RemoveAll(t => string.IsNullOrWhiteSpace(t.Path)) > 0;

        return changed;
    }

    /// <summary>O JSON como ele seria gravado. Serve para comparar duas configs.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Avisa quem observa. Usado quando a config muda fora do Save (arquivo editado à mão).</summary>
    public static void NotifyChanged() => Changed?.Invoke();

    public void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
            Changed?.Invoke();
        }
        catch (Exception error)
        {
            // salvar config nunca deve derrubar o hub
            Log.Warn("não consegui salvar a configuração", error);
        }
    }
}
