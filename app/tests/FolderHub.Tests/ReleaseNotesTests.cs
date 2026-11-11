using FolderHub.Controls;
using FolderHub.Services;
using Xunit;

namespace FolderHub.Tests;

/// <summary>
/// O RELEASE_NOTES.md é escrito à mão e lido pelo app. Estes testes fecham essa
/// volta: o que a documentação manda escrever é o que o parser entende.
/// </summary>
public class ReleaseNotesTests
{
    private const string Sample = """
        # Notas de versão

        Um preâmbulo que não é de versão nenhuma e não deve virar destaque.

        ## 1.2.0

        ### Features
        - **Tema claro** — O hub vem em claro também.
        - **Densidade** — Três tamanhos de card.

        ### Fix
        - **Abas esticadas** — O raio agora é a metade da altura.

        ## 1.1.0

        ### Features
        - **Coisa antiga** — De uma versão que não é a de agora.
        """;

    [Fact]
    public void le_so_o_bloco_da_versao_pedida()
    {
        var notes = ReleaseNotes.Parse(Sample, "1.2.0");

        Assert.Equal(3, notes.Count);
        Assert.DoesNotContain(notes, n => n.Title == "Coisa antiga");
    }

    /// <summary>
    /// O arquivo documenta o próprio formato com um exemplo cercado por crases.
    /// Se esse exemplo virar destaque, quem atualizou vê "Título curto" na tela.
    /// </summary>
    [Fact]
    public void o_exemplo_da_documentacao_nao_vira_destaque()
    {
        const string withDocs = """
            ## Como escrever

            ```markdown
            ## 3.0.0

            ### Features
            - **Título de mentira** — do exemplo, não de uma versão.
            ```

            ## 3.0.0

            ### Features
            - **Título de verdade** — esta é a versão mesmo.
            """;

        var notes = ReleaseNotes.Parse(withDocs, "3.0.0");

        Assert.Single(notes);
        Assert.Equal("Título de verdade", notes[0].Title);
    }

    /// <summary>
    /// O arquivo de verdade tem esse exemplo. Este teste é o que garante que
    /// mexer nele não vaza para a tela.
    /// </summary>
    [Fact]
    public void o_arquivo_embutido_nao_traz_o_exemplo()
        => Assert.DoesNotContain(ReleaseNotes.Current, n => n.Title.Contains("Título curto"));

    [Fact]
    public void o_preambulo_nao_vira_destaque()
        => Assert.DoesNotContain(ReleaseNotes.Parse(Sample, "1.2.0"), n => n.Title.Contains("preâmbulo"));

    [Fact]
    public void separa_titulo_e_texto_pelo_travessao()
    {
        var note = ReleaseNotes.Parse(Sample, "1.2.0")[0];

        Assert.Equal("Tema claro", note.Title);
        Assert.Equal("O hub vem em claro também.", note.Text);
    }

    [Fact]
    public void cada_destaque_sabe_de_que_etiqueta_veio()
    {
        var notes = ReleaseNotes.Parse(Sample, "1.2.0");

        Assert.Equal(["Features", "Features", "Fix"], notes.Select(n => n.Tag));
    }

    /// <summary>
    /// A versão que não está no arquivo não tem novidade — e é melhor assim do
    /// que mostrar a de outra versão.
    /// </summary>
    [Fact]
    public void versao_ausente_nao_tem_destaque()
        => Assert.Empty(ReleaseNotes.Parse(Sample, "9.9.9"));

    /// <summary>
    /// O bloco da última versão vai até o fim do arquivo, sem próximo título
    /// para servir de limite.
    /// </summary>
    [Fact]
    public void o_ultimo_bloco_do_arquivo_e_lido_inteiro()
    {
        var notes = ReleaseNotes.Parse(Sample, "1.1.0");

        Assert.Single(notes);
        Assert.Equal("Coisa antiga", notes[0].Title);
    }

    [Fact]
    public void sem_travessao_a_linha_inteira_e_o_titulo()
    {
        var notes = ReleaseNotes.Parse("## 1.0.0\n### Fix\n- Só uma frase solta\n", "1.0.0");

        Assert.Single(notes);
        Assert.Equal("Só uma frase solta", notes[0].Title);
        Assert.Empty(notes[0].Text);
    }

    [Theory]
    [InlineData("Features", "NOVIDADES", HubGlyph.Add)]
    [InlineData("Fix", "CORREÇÕES", HubGlyph.Check)]
    [InlineData("fix", "CORREÇÕES", HubGlyph.Check)]
    public void as_etiquetas_conhecidas_tem_rotulo_e_icone(string tag, string label, HubGlyph glyph)
    {
        var note = new ReleaseNote(tag, "t", "x");

        Assert.Equal(label, note.Label);
        Assert.Equal(glyph, note.Glyph);
    }

    /// <summary>
    /// Inventar uma etiqueta não quebra nada: ela aparece com o próprio nome e
    /// um ícone neutro, como o RELEASE_NOTES.md promete.
    /// </summary>
    [Fact]
    public void etiqueta_desconhecida_aparece_com_o_proprio_nome()
    {
        var note = new ReleaseNote("Performance", "t", "x");

        Assert.Equal("PERFORMANCE", note.Label);
        Assert.Equal(HubGlyph.Info, note.Glyph);
    }

    // ------------------------------------------------------- o arquivo de verdade

    /// <summary>
    /// O arquivo embutido tem o bloco da versão que está rodando. Sem isto a
    /// tela de novidades ficaria muda numa release — e calada, porque não ter
    /// novidade é um estado válido.
    /// </summary>
    [Fact]
    public void o_arquivo_embutido_cobre_a_versao_atual()
    {
        Assert.NotEmpty(ReleaseNotes.Current);
        Assert.All(ReleaseNotes.Current, n => Assert.NotEmpty(n.Title));
    }

    [Fact]
    public void os_destaques_saem_agrupados_por_etiqueta()
    {
        var sections = ReleaseNotes.Top(int.MaxValue);

        Assert.NotEmpty(sections);
        Assert.Equal(ReleaseNotes.Current.Count, sections.Sum(s => s.Notes.Count));
        Assert.All(sections, s => Assert.NotEmpty(s.Label));
    }

    /// <summary>
    /// O limite existe para a janela não crescer sem fim numa versão
    /// movimentada, e ele corta o total, não cada seção.
    /// </summary>
    [Fact]
    public void o_limite_corta_o_total_de_destaques()
        => Assert.Equal(2, ReleaseNotes.Top(2).Sum(s => s.Notes.Count));

    [Fact]
    public void limite_zero_nao_mostra_nada()
        => Assert.Empty(ReleaseNotes.Top(0));
}
