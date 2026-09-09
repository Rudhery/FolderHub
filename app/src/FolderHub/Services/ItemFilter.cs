using System.Globalization;
using FolderHub.Models;

namespace FolderHub.Services;

/// <summary>
/// A busca da caixa de texto. Separada da janela para poder ser testada, e
/// ignorando acento: num hub em português, obrigar o usuário a digitar
/// "informações" para achar "Informações do Sistema" é atrito à toa.
/// </summary>
public static class ItemFilter
{
    private const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static bool Matches(string? name, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        if (string.IsNullOrEmpty(name)) return false;

        return CultureInfo.CurrentCulture.CompareInfo.IndexOf(name, query.Trim(), Loose) >= 0;
    }

    public static IEnumerable<AppItem> Apply(IEnumerable<AppItem> items, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return items;

        return items.Where(item => Matches(item.Name, query));
    }
}
