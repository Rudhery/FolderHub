using System.Text;
using System.Windows.Input;

namespace FolderHub.Services;

/// <summary>
/// Transforma uma combinação de teclas no texto que a configuração guarda.
///
/// Vive separado do campo que captura porque forma um par com o
/// <see cref="GlobalHotKey.TryParse"/>: o que este arquivo escreve, aquele
/// precisa saber ler de volta. Um "Espaço" aqui e um "Space" lá é o tipo de
/// divergência que não aparece em lugar nenhum — o atalho simplesmente deixa de
/// funcionar, sem erro. Estando os dois lados alcançáveis, um teste percorre o
/// teclado inteiro e prova que a volta existe.
/// </summary>
public static class HotKeyText
{
    /// <summary>
    /// Os modificadores em ordem fixa, para a mesma combinação dar sempre o
    /// mesmo texto — senão ela viraria dois valores diferentes conforme a ordem
    /// em que as teclas foram apertadas.
    ///
    /// A ordem é a do Windows, que põe a tecla Windows na frente: Win+Shift+S,
    /// Win+Ctrl+D. O parser aceita qualquer ordem na leitura; esta é só a que
    /// vai para o arquivo.
    /// </summary>
    public static string Describe(ModifierKeys modifiers)
    {
        var text = new StringBuilder();

        if (modifiers.HasFlag(ModifierKeys.Windows)) text.Append("Win+");
        if (modifiers.HasFlag(ModifierKeys.Control)) text.Append("Ctrl+");
        if (modifiers.HasFlag(ModifierKeys.Alt)) text.Append("Alt+");
        if (modifiers.HasFlag(ModifierKeys.Shift)) text.Append("Shift+");

        return text.ToString();
    }

    public static bool IsModifier(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift
        or Key.LWin or Key.RWin
        or Key.System or Key.None;

    /// <summary>O nome da tecla como o parser da casa espera lê-lo.</summary>
    public static bool TryName(Key key, out string name)
    {
        name = key switch
        {
            // "1" é Key.D1 no WPF, e o parser desfaz isso do outro lado.
            >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
            Key.Space => "Space",
            Key.Return => "Enter",
            _ => key.ToString()
        };

        return name.Length > 0 && key != Key.None;
    }

    /// <summary>
    /// A combinação inteira, ou false se ela não serve como atalho global. A
    /// validação final é o próprio <see cref="GlobalHotKey.TryParse"/>: assim
    /// nada é aceito aqui que fosse ser recusado na hora de registrar.
    /// </summary>
    public static bool TryFormat(ModifierKeys modifiers, Key key, out string spec)
    {
        spec = string.Empty;

        if (IsModifier(key) || !TryName(key, out string name)) return false;

        string candidate = Describe(modifiers) + name;
        if (!GlobalHotKey.TryParse(candidate, out _, out _)) return false;

        spec = candidate;
        return true;
    }
}
