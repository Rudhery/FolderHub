using System.Windows;
using FolderHub.Controls;
using Xunit;

namespace FolderHub.Tests;

/// <summary>
/// A camada de design é quase toda XAML, que o compilador só confere na hora de
/// abrir a janela. O que dá para prender em teste é a parte em C#: a escala, o
/// conjunto de ícones e o tempo. São justamente as três coisas que uma tela nova
/// vai consumir sem olhar, e que quebram em silêncio quando divergem.
/// </summary>
public class DesignSystemTests
{
    // ------------------------------------------------------------- espaçamento

    [Theory]
    [InlineData(HubSize.None, 0)]
    [InlineData(HubSize.XSmall, 4)]
    [InlineData(HubSize.Small, 8)]
    [InlineData(HubSize.Medium, 12)]
    [InlineData(HubSize.Large, 16)]
    [InlineData(HubSize.XLarge, 20)]
    [InlineData(HubSize.XXLarge, 24)]
    public void escala_anda_de_quatro_em_quatro(HubSize size, double expected)
        => Assert.Equal(expected, size.Pixels());

    [Fact]
    public void inherit_nao_vira_espessura()
    {
        // É o que faz "não definido" ser diferente de "zero": um card sem
        // Spacing tem de manter o respiro do modelo, não perdê-lo.
        Assert.Null(HubSize.Inherit.ToThickness());
        Assert.Equal(new Thickness(0), HubSize.None.ToThickness());
    }

    [Fact]
    public void espessura_e_uniforme()
    {
        var thickness = HubSize.Medium.ToThickness();

        Assert.NotNull(thickness);
        Assert.Equal(new Thickness(12), thickness!.Value);
    }

    // ------------------------------------------------------------------ ícones

    [Fact]
    public void todo_glifo_desenha_alguma_coisa()
    {
        foreach (HubGlyph glyph in Enum.GetValues<HubGlyph>())
        {
            if (glyph == HubGlyph.None)
            {
                Assert.Equal(string.Empty, glyph.Text());
                continue;
            }

            Assert.False(string.IsNullOrEmpty(glyph.Text()), $"{glyph} não virou caractere");
        }
    }

    [Fact]
    public void nenhum_glifo_repete_codepoint()
    {
        // Dois nomes no mesmo codepoint é quase sempre erro de digitação, e o
        // sintoma seria o ícone errado numa tela só.
        var byCode = Enum.GetValues<HubGlyph>()
            .Where(g => g != HubGlyph.None)
            .GroupBy(g => (int)g)
            .Where(group => group.Count() > 1)
            .Select(group => $"U+{group.Key:X4}: {string.Join(", ", group)}")
            .ToList();

        Assert.Empty(byCode);
    }

    [Fact]
    public void glifos_vivem_na_area_de_uso_privado()
    {
        // A Segoe Fluent Icons desenha na Private Use Area. Um valor fora dela é
        // um codepoint copiado errado, e apareceria como retângulo vazio.
        foreach (HubGlyph glyph in Enum.GetValues<HubGlyph>())
        {
            if (glyph == HubGlyph.None) continue;

            int code = (int)glyph;
            Assert.True(code is >= 0xE000 and <= 0xF8FF, $"{glyph} (U+{code:X4}) está fora da PUA");
        }
    }

    // ---------------------------------------------------------------- movimento

    [Fact]
    public void saida_e_mais_rapida_que_entrada()
    {
        // Esperar para algo sumir incomoda mais do que esperar para aparecer.
        Assert.True(HubMotion.Exit.TimeSpan < HubMotion.Enter.TimeSpan);
        Assert.True(HubMotion.State.TimeSpan < HubMotion.Enter.TimeSpan);
    }

    [Fact]
    public void deslize_assenta_depois_do_fade()
        => Assert.True(HubMotion.Slide.TimeSpan > HubMotion.Enter.TimeSpan);

    [Fact]
    public void escalonamento_cresce_e_para()
    {
        Assert.Equal(TimeSpan.Zero, HubMotion.Stagger(0));
        Assert.Equal(TimeSpan.FromMilliseconds(HubMotion.StaggerStepMs), HubMotion.Stagger(1));
        Assert.True(HubMotion.Stagger(5) < HubMotion.Stagger(10));

        // Sem o teto, o último card de uma pasta com 800 atalhos entraria
        // dez segundos depois do primeiro.
        Assert.Equal(HubMotion.Stagger(HubMotion.StaggerCap), HubMotion.Stagger(800));
        Assert.True(HubMotion.Stagger(800) < TimeSpan.FromMilliseconds(600));
    }

    [Fact]
    public void escalonamento_nao_aceita_indice_negativo()
        => Assert.Equal(TimeSpan.Zero, HubMotion.Stagger(-1));
}
