using System.Windows;
using System.Windows.Input;
using FolderHub.Services;

namespace FolderHub.Views;

/// <summary>
/// O que mudou, mostrado uma vez depois de atualizar.
///
/// Aparece antes do hub e sai do caminho no primeiro clique: quem atualizou
/// queria abrir um atalho, não ler uma tela. Por isso ela é curta — quatro
/// destaques, não o changelog — e o Esc a dispensa como qualquer outra janela
/// da casa.
///
/// Gravar a versão vista é o que garante o "uma vez": acontece ao fechar, não
/// ao abrir, senão um encerramento no meio do caminho consumiria a novidade sem
/// ninguém ter lido.
/// </summary>
public partial class WhatsNewWindow : HubWindow
{
    /// <summary>
    /// Quantos destaques a tela mostra. Quatro é o que o design previu, e o que
    /// cabe sem a janela virar uma lista para rolar.
    /// </summary>
    private const int Highlights = 4;

    public WhatsNewWindow() : base(acrylic: false)
    {
        InitializeComponent();

        string? from = App.Config.SeenVersion is { Length: > 0 } seen ? seen : null;

        VersionPill.Text = $"v{ReleaseNotes.Version}";
        Subtitle.Text = from is null
            ? "O que mudou nesta versão."
            : $"O que mudou desde a {from}.";

        var shown = ReleaseNotes.Top(Highlights);
        Sections.ItemsSource = shown;

        int total = ReleaseNotes.Current.Count;
        int rest = total - shown.Sum(s => s.Notes.Count);
        Trailer.Text = rest > 0
            ? $"e mais {rest} no changelog do repositório"
            : "changelog completo no repositório";

        Loaded += (_, _) => FadeIn();
    }

    /// <summary>
    /// Marca esta versão como vista. Fica no <c>Dismiss</c>, e não no
    /// <c>Closed</c>: o Closed também dispara quando alguém fecha a janela por
    /// código — um teste, por exemplo — e aí abrir e fechar a tela sem ninguém
    /// olhando gravava a configuração do usuário. Já aconteceu.
    ///
    /// Os dois caminhos que uma pessoa tem para sair daqui, o botão e o Esc,
    /// passam por aqui.
    /// </summary>
    protected override void Dismiss()
    {
        if (!Dismissing)
        {
            App.Config.SeenVersion = ReleaseNotes.Version;
            App.Config.Save();
        }

        base.Dismiss();
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Dismiss();

    private void Shell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragFrom(e);
}
