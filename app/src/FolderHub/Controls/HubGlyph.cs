namespace FolderHub.Controls;

/// <summary>
/// Os ícones que o FolderHub usa, por nome.
///
/// O XAML antes trazia <c>&amp;#xE721;</c> junto com a família de fonte repetida
/// em cada uso. Ninguém lê um codepoint, então uma troca de ícone virava
/// tentativa e erro — e a família errada em um lugar só aparecia rodando.
///
/// Os valores são da Segoe Fluent Icons, que vem no Windows 11.
/// </summary>
public enum HubGlyph
{
    None = 0,

    Search = 0xE721,
    Reload = 0xE72C,
    Settings = 0xE713,
    Close = 0xE8BB,
    Folder = 0xE8B7,
    Sort = 0xE8CB,
    Check = 0xE73E,

    Add = 0xE710,
    Remove = 0xE738,
    Delete = 0xE74D,
    Edit = 0xE70F,
    More = 0xE712,

    ChevronRight = 0xE76C,
    ChevronDown = 0xE70D,
    Back = 0xE72B,
    OpenExternal = 0xE8A7,

    Warning = 0xE7BA,
    Info = 0xE946,
    Error = 0xEA39,

    Keyboard = 0xE765,
    Appearance = 0xE790,
    Language = 0xE774,
    Update = 0xE895,
    Startup = 0xE7E8,
    Pin = 0xE718,

    App = 0xECAA,
    Grid = 0xE80A
}

public static class HubGlyphs
{
    /// <summary>O caractere que a fonte de ícones desenha.</summary>
    public static string Text(this HubGlyph glyph)
        => glyph == HubGlyph.None ? string.Empty : char.ConvertFromUtf32((int)glyph);
}
