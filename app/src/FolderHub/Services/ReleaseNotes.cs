using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using FolderHub.Controls;

namespace FolderHub.Services;

/// <summary>Um destaque da versão: a etiqueta de onde veio, o que é, e o texto.</summary>
public sealed record ReleaseNote(string Tag, string Title, string Text)
{
    /// <summary>O ícone da etiqueta. Desconhecida fica com o neutro.</summary>
    public HubGlyph Glyph => Tag.ToLowerInvariant() switch
    {
        "features" => HubGlyph.Add,
        "fix" => HubGlyph.Check,
        _ => HubGlyph.Info
    };

    /// <summary>A etiqueta como se lê na tela.</summary>
    public string Label => Tag.ToLowerInvariant() switch
    {
        "features" => "NOVIDADES",
        "fix" => "CORREÇÕES",
        _ => Tag.ToUpperInvariant()
    };
}

/// <summary>Uma etiqueta e os destaques dela.</summary>
public sealed record ReleaseSection(string Label, HubGlyph Glyph, IReadOnlyList<ReleaseNote> Notes);

/// <summary>
/// O que mudou nesta versão, lido do RELEASE_NOTES.md que viaja dentro do exe.
///
/// Embutido, e não buscado do GitHub, por duas razões. A primeira é que a tela
/// só precisa do texto da versão que está rodando — e essa versão é esta
/// compilação, então a rede não traria nada que o arquivo já não tenha. A
/// segunda é que o app promete não fazer requisição nenhuma, e essa promessa
/// está escrita no SECURITY.md, na landing e na própria configuração.
///
/// O mesmo arquivo vai no corpo da release no GitHub. Um texto só para a mesma
/// pessoa: quem baixou o instalador.
///
/// A versão vem do assembly, então a tela não consegue anunciar novidade de uma
/// versão que não é a que está rodando — e um bloco que não existe no arquivo
/// simplesmente não tem novidade para mostrar, em vez de mostrar a errada.
/// </summary>
public static partial class ReleaseNotes
{
    /// <summary>A versão que está rodando.</summary>
    public static string Version => App.Version;

    private static IReadOnlyList<ReleaseNote>? _current;

    /// <summary>Os destaques desta versão, na ordem em que o arquivo os traz.</summary>
    public static IReadOnlyList<ReleaseNote> Current => _current ??= Load();

    /// <summary>
    /// Os primeiros destaques, agrupados por etiqueta para a tela desenhar.
    ///
    /// Tem limite porque a tela é um resumo e não o changelog: sem ele uma
    /// versão movimentada abria uma janela de mil pixels de altura na cara de
    /// quem só queria abrir um atalho. O que sobra segue no repositório, e o
    /// rodapé diz quanto é.
    ///
    /// O corte respeita a ordem do arquivo, então o que vem primeiro no
    /// RELEASE_NOTES.md é o que aparece — é a ordem que quem escreveu escolheu.
    /// </summary>
    public static IReadOnlyList<ReleaseSection> Top(int count) =>
    [
        .. Current
            .Take(Math.Max(0, count))
            .GroupBy(n => n.Label)
            .Select(g => new ReleaseSection(g.Key, g.First().Glyph, [.. g]))
    ];

    /// <summary>Tem destaque para mostrar nesta versão.</summary>
    public static bool Any => Current.Count > 0;

    /// <summary>
    /// Esta versão já foi mostrada.
    ///
    /// Note que "não registrado" conta como não vista, e é de propósito: quem
    /// atualiza de uma versão anterior a este campo não tem registro nenhum, e
    /// tratá-lo como já-visto faria toda essa gente perder as novidades sem
    /// nada na tela. Quem é primeira instalação é decidido por quem chama —
    /// lá se sabe se a instalação guiada acabou de falar.
    /// </summary>
    public static bool AlreadySeen(string? seen)
        => string.Equals(seen, Version, StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ leitura

    /// <summary>
    /// Um bloco de código cercado por crases. O arquivo tem um, mostrando como
    /// escrever os outros — e um exemplo com aparência de versão viraria
    /// destaque de verdade na tela de quem atualizou.
    /// </summary>
    [GeneratedRegex(@"^```.*?^```", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FencedBlock();

    /// <summary>O bloco "## 1.2.0" de uma versão, até o próximo bloco de versão.</summary>
    [GeneratedRegex(@"^##\s+(?<version>\d+\.\d+\.\d+)\s*$", RegexOptions.Multiline)]
    private static partial Regex VersionHeading();

    /// <summary>A seção "### Features" dentro do bloco.</summary>
    [GeneratedRegex(@"^###\s+(?<tag>.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex TagHeading();

    /// <summary>
    /// Um item: "- **Título** — texto". O travessão é o separador; sem ele a
    /// linha inteira vira título, que é melhor do que perder a linha.
    /// </summary>
    [GeneratedRegex(@"^-\s+(?:\*\*(?<title>.+?)\*\*\s*(?:—|--)\s*)?(?<text>.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex Bullet();

    private static List<ReleaseNote> Load()
    {
        try
        {
            string markdown = Read();
            return markdown.Length == 0 ? [] : Parse(markdown, Version);
        }
        catch (Exception error)
        {
            // Sem notas a tela não aparece, e é o comportamento certo: é melhor
            // não dizer nada do que dizer a novidade de outra versão.
            Log.Warn("não consegui ler as notas de versão embutidas", error);
            return [];
        }
    }

    private static string Read()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("FolderHub.ReleaseNotes.md");
        if (stream is null)
        {
            Log.Warn("RELEASE_NOTES.md não está embutido no assembly");
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Separado da leitura para poder ser testado com um texto de mentira — é a
    /// única parte disto que tem como dar errado de um jeito interessante.
    /// </summary>
    internal static List<ReleaseNote> Parse(string markdown, string version)
    {
        var notes = new List<ReleaseNote>();

        // A documentação de como escrever o arquivo mora no próprio arquivo.
        markdown = FencedBlock().Replace(markdown, string.Empty);

        string? block = BlockFor(markdown, version);
        if (block is null) return notes;

        // As seções de etiqueta, cada uma até o começo da próxima.
        var headings = TagHeading().Matches(block);
        for (int i = 0; i < headings.Count; i++)
        {
            var heading = headings[i];
            string tag = heading.Groups["tag"].Value.Trim();

            int start = heading.Index + heading.Length;
            int end = i + 1 < headings.Count ? headings[i + 1].Index : block.Length;

            foreach (var bullet in Bullet().Matches(block[start..end]).Cast<Match>())
            {
                string text = bullet.Groups["text"].Value.Trim();
                string title = bullet.Groups["title"].Success
                    ? bullet.Groups["title"].Value.Trim()
                    : text;

                // Sem travessão o título é a linha toda, e repetir o mesmo
                // texto embaixo dele não informa nada.
                if (title == text) text = string.Empty;

                if (title.Length > 0) notes.Add(new ReleaseNote(tag, title, text));
            }
        }

        return notes;
    }

    /// <summary>O trecho do arquivo que pertence a uma versão, ou nulo se ela não está lá.</summary>
    private static string? BlockFor(string markdown, string version)
    {
        var headings = VersionHeading().Matches(markdown);

        for (int i = 0; i < headings.Count; i++)
        {
            if (!string.Equals(headings[i].Groups["version"].Value, version, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int start = headings[i].Index + headings[i].Length;
            int end = i + 1 < headings.Count ? headings[i + 1].Index : markdown.Length;
            return markdown[start..end];
        }

        return null;
    }
}
