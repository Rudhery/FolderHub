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
}
