using System.Windows.Input;
using FolderHub.Services;
using Xunit;

namespace FolderHub.Tests;

/// <summary>
/// O campo de atalho escreve o texto; o registrador lê. São dois arquivos
/// diferentes, e a divergência entre eles não produz erro nenhum — o atalho
/// apenas para de funcionar. Estes testes fecham essa volta.
/// </summary>
public class HotKeyTextTests
{
    [Theory]
    [InlineData(ModifierKeys.Control, Key.Space, "Ctrl+Space")]
    [InlineData(ModifierKeys.Control | ModifierKeys.Alt, Key.Space, "Ctrl+Alt+Space")]
    [InlineData(ModifierKeys.Alt, Key.Q, "Alt+Q")]
    [InlineData(ModifierKeys.Windows | ModifierKeys.Shift, Key.H, "Win+Shift+H")]
    [InlineData(ModifierKeys.Control, Key.D1, "Ctrl+1")]
    [InlineData(ModifierKeys.Control, Key.Return, "Ctrl+Enter")]
    public void formata_como_a_configuracao_guarda(ModifierKeys modifiers, Key key, string expected)
    {
        Assert.True(HotKeyText.TryFormat(modifiers, key, out string spec));
        Assert.Equal(expected, spec);
    }

    [Fact]
    public void modificadores_saem_sempre_na_mesma_ordem()
    {
        // Senão a mesma combinação viraria dois textos diferentes conforme a
        // ordem em que as teclas foram pressionadas.
        HotKeyText.TryFormat(ModifierKeys.Alt | ModifierKeys.Control, Key.J, out string a);
        HotKeyText.TryFormat(ModifierKeys.Control | ModifierKeys.Alt, Key.J, out string b);

        Assert.Equal(a, b);
        Assert.Equal("Ctrl+Alt+J", a);
    }

    [Fact]
    public void recusa_tecla_sem_modificador()
    {
        // O Windows recusa também: um atalho global de tecla solta engoliria
        // aquela tecla no sistema inteiro.
        Assert.False(HotKeyText.TryFormat(ModifierKeys.None, Key.Q, out _));
        Assert.False(HotKeyText.TryFormat(ModifierKeys.None, Key.F1, out _));
    }

    [Fact]
    public void recusa_modificador_sozinho()
    {
        foreach (var key in new[] { Key.LeftCtrl, Key.RightAlt, Key.LeftShift, Key.LWin, Key.System, Key.None })
        {
            Assert.True(HotKeyText.IsModifier(key), $"{key} deveria contar como modificador");
            Assert.False(HotKeyText.TryFormat(ModifierKeys.Control, key, out _));
        }
    }

    [Fact]
    public void tudo_que_o_campo_aceita_o_registrador_le_de_volta()
    {
        // A prova de que os dois lados falam a mesma língua: percorre o teclado
        // inteiro, e o que for formatado tem de voltar como a mesma combinação.
        int checados = 0;

        foreach (Key key in Enum.GetValues<Key>())
        {
            if (!HotKeyText.TryFormat(ModifierKeys.Control | ModifierKeys.Alt, key, out string spec)) continue;

            Assert.True(GlobalHotKey.TryParse(spec, out uint modifiers, out uint vk),
                $"{key} virou \"{spec}\", que o registrador não entende");

            Assert.NotEqual(0u, modifiers);
            Assert.NotEqual(0u, vk);

            // E o código de tecla tem de ser o da tecla original, não outro.
            Assert.Equal((uint)KeyInterop.VirtualKeyFromKey(key), vk);

            checados++;
        }

        // Se um dia a formatação parar de aceitar tudo, o número cai e o teste
        // avisa em vez de passar percorrendo uma lista vazia.
        Assert.True(checados > 80, $"só {checados} teclas formataram — algo ficou pelo caminho");
    }

    [Fact]
    public void o_atalho_padrao_do_app_e_valido()
    {
        // O valor que sai de fábrica em HubConfig.HotKey.
        Assert.True(GlobalHotKey.TryParse("Ctrl+Alt+Space", out _, out _));
    }

    // ------------------------------------------------------- teclas na tela

    [Theory]
    [InlineData("Ctrl+Alt+Space", "Ctrl|Alt|Espaço")]
    [InlineData("Win+Shift+H", "Win|Shift|H")]
    [InlineData("Alt+Q", "Alt|Q")]
    [InlineData("Ctrl+Alt+Left", "Ctrl|Alt|←")]
    public void parts_quebra_a_combinacao_com_o_nome_de_tela(string spec, string expected)
        => Assert.Equal(expected, string.Join("|", HotKeyText.Parts(spec)));

    /// <summary>
    /// O que não é combinação não vira tecla desenhada — é por este vazio que o
    /// campo de captura decide mostrar um recado.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+Alt+…")]
    [InlineData("pressione a combinação")]
    [InlineData("Space")]
    public void parts_ignora_o_que_nao_e_combinacao(string text)
        => Assert.Empty(HotKeyText.Parts(text));

    /// <summary>
    /// O nome de tela é de mão única, e o teste mostra por quê: "Espaço" o
    /// parser até aceita, porque ele é leniente de propósito com config editada
    /// à mão, mas a seta não existe como nome de tecla em lugar nenhum. Gravar
    /// o que o <see cref="HotKeyText.Parts"/> devolve daria um atalho que some
    /// sem erro na próxima abertura.
    /// </summary>
    [Fact]
    public void o_nome_de_tela_nao_serve_para_gravar()
    {
        Assert.Equal("Ctrl|Alt|←", string.Join("|", HotKeyText.Parts("Ctrl+Alt+Left")));
        Assert.False(GlobalHotKey.TryParse("Ctrl+Alt+←", out _, out _));

        // Quem grava é o TryFormat, e o que ele escreve o parser lê de volta.
        Assert.True(HotKeyText.TryFormat(ModifierKeys.Control | ModifierKeys.Alt, Key.Left, out string spec));
        Assert.Equal("Ctrl+Alt+Left", spec);
        Assert.True(GlobalHotKey.TryParse(spec, out _, out _));
    }

    /// <summary>
    /// A leniência do parser com acento é intencional: o arquivo é editável à
    /// mão, e "Espaço" é o que se escreveria. Fica um teste para que tirá-la
    /// seja uma decisão e não um efeito colateral.
    /// </summary>
    [Fact]
    public void o_parser_aceita_espaco_escrito_a_mao()
    {
        Assert.True(GlobalHotKey.TryParse("Ctrl+Alt+Space", out _, out uint space));
        Assert.True(GlobalHotKey.TryParse("Ctrl+Alt+Espaço", out _, out uint acentuado));
        Assert.True(GlobalHotKey.TryParse("Ctrl+Alt+espaco", out _, out uint semAcento));

        Assert.Equal(space, acentuado);
        Assert.Equal(space, semAcento);
    }
}
