using FolderHub.Services;
using Xunit;

namespace FolderHub.Tests;

public class GlobalHotKeyTests
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private const uint VkSpace = 0x20;
    private const uint VkQ = 0x51;
    private const uint VkF5 = 0x74;
    private const uint Vk1 = 0x31;

    [Theory]
    [InlineData("Ctrl+Alt+Space", ModControl | ModAlt, VkSpace)]
    [InlineData("ctrl+alt+space", ModControl | ModAlt, VkSpace)]
    [InlineData("Alt+Q", ModAlt, VkQ)]
    [InlineData("Win+Shift+Q", ModWin | ModShift, VkQ)]
    [InlineData("Ctrl + Shift + F5", ModControl | ModShift, VkF5)]
    [InlineData("Ctrl+1", ModControl, Vk1)]
    public void Parses_the_combinations_a_user_would_write(string spec, uint expectedModifiers, uint expectedKey)
    {
        Assert.True(GlobalHotKey.TryParse(spec, out uint modifiers, out uint key));
        Assert.Equal(expectedModifiers, modifiers);
        Assert.Equal(expectedKey, key);
    }

    [Theory]
    [InlineData("Space")]        // sem modificador engoliria a tecla no sistema inteiro
    [InlineData("Ctrl")]         // só modificador
    [InlineData("Ctrl+Banana")]  // tecla inexistente
    [InlineData("Ctrl+A+B")]     // duas teclas comuns
    [InlineData("")]
    [InlineData(null)]
    public void Refuses_what_cannot_be_registered(string? spec)
    {
        Assert.False(GlobalHotKey.TryParse(spec, out _, out _));
    }

    [Fact]
    public void Esc_and_Enter_accept_the_short_spelling()
    {
        Assert.True(GlobalHotKey.TryParse("Ctrl+Esc", out _, out uint esc));
        Assert.True(GlobalHotKey.TryParse("Ctrl+Enter", out _, out uint enter));

        Assert.Equal(0x1Bu, esc);
        Assert.Equal(0x0Du, enter);
    }
}
