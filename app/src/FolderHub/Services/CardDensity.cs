namespace FolderHub.Services;

/// <summary>
/// O tamanho dos cards do hub.
///
/// Não é só estética: com atalhos de nome longo o card compacto passa a cortar
/// texto, e com poucos atalhos o grande enche melhor a janela. Os números vivem
/// em <see cref="Metrics"/> e não no tema porque o código que dimensiona a
/// janela precisa deles antes de qualquer coisa ser desenhada.
/// </summary>
public enum CardDensity
{
    Compact,
    Default,
    Large
}

public static class CardDensities
{
    /// <summary>Largura, altura e o lado do bloco de ícone, em pixels lógicos.</summary>
    public static (double Width, double Height, double Tile) Metrics(this CardDensity density) => density switch
    {
        // O rótulo continua com duas linhas em todas: é o que impede que um
        // nome comprido troque de altura conforme a densidade.
        // Ícone solto, sem caixa em volta: a célula é pouco mais que o ícone e
        // duas linhas de nome.
        CardDensity.Compact => (92, 96, 36),
        CardDensity.Large => (124, 128, 56),
        _ => (104, 108, 44)
    };
}
