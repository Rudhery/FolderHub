using FolderHub.Services;
using Xunit;

namespace FolderHub.Tests;

public class FolderScannerTests
{
    [Fact]
    public void Scan_only_picks_up_launchable_files()
    {
        using var folder = new TempFolder();
        folder.Add("Steam.lnk", "GitHub.url", "Tool.exe", "notes.txt", "photo.png", "script.bat");

        // ordinal: a ordenação aqui é só para comparar, não é a do app
        var found = FolderScanner.Scan(folder.Path).Select(i => i.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["GitHub", "Steam", "Tool", "script"], found);
    }

    [Fact]
    public void Scan_hides_the_ordering_prefix_from_the_card()
    {
        using var folder = new TempFolder();
        folder.Add("01 - Steam.lnk", "02. Discord.lnk", "03_OBS.lnk", "04) Spotify.lnk");

        var names = FolderScanner.Scan(folder.Path).Select(i => i.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["Discord", "OBS", "Spotify", "Steam"], names);
    }

    [Fact]
    public void Scan_ignores_hidden_files()
    {
        using var folder = new TempFolder();
        folder.Add("Visible.lnk", "Hidden.lnk");
        File.SetAttributes(Path.Combine(folder.Path, "Hidden.lnk"), FileAttributes.Hidden);

        var names = FolderScanner.Scan(folder.Path).Select(i => i.Name).ToArray();

        Assert.Equal(["Visible"], names);
    }

    [Fact]
    public void Scan_returns_empty_for_a_folder_that_is_not_there()
    {
        Assert.Empty(FolderScanner.Scan(Path.Combine(Path.GetTempPath(), "folderhub-does-not-exist")));
    }

    [Fact]
    public void Manual_sort_follows_the_numeric_prefix()
    {
        using var folder = new TempFolder();
        folder.Add("03 - Zebra.lnk", "01 - alpha.lnk", "02 - Item10.lnk", "04 - Item2.lnk");

        var items = FolderScanner.Scan(folder.Path);
        FolderScanner.Sort(items, SortMode.Manual);

        Assert.Equal(["alpha", "Item10", "Zebra", "Item2"], items.Select(i => i.Name));
    }

    [Fact]
    public void Name_sort_is_natural_so_Item2_comes_before_Item10()
    {
        using var folder = new TempFolder();
        folder.Add("Item10.lnk", "Item2.lnk", "alpha.lnk", "Zebra.lnk");

        var items = FolderScanner.Scan(folder.Path);
        FolderScanner.Sort(items, SortMode.NameAsc);

        Assert.Equal(["alpha", "Item2", "Item10", "Zebra"], items.Select(i => i.Name));
    }

    [Fact]
    public void Descending_sort_is_the_exact_reverse()
    {
        using var folder = new TempFolder();
        folder.Add("Item10.lnk", "Item2.lnk", "alpha.lnk", "Zebra.lnk");

        var items = FolderScanner.Scan(folder.Path);

        FolderScanner.Sort(items, SortMode.NameAsc);
        var ascending = items.Select(i => i.Name).ToArray();

        FolderScanner.Sort(items, SortMode.NameDesc);
        var descending = items.Select(i => i.Name).ToArray();

        Assert.Equal(ascending.Reverse(), descending);
    }

    [Fact]
    public void Recent_sort_puts_the_newest_first()
    {
        using var folder = new TempFolder();
        folder.Add("Old.lnk", "New.lnk");
        File.SetLastWriteTimeUtc(Path.Combine(folder.Path, "Old.lnk"), DateTime.UtcNow.AddDays(-3));
        File.SetLastWriteTimeUtc(Path.Combine(folder.Path, "New.lnk"), DateTime.UtcNow);

        var items = FolderScanner.Scan(folder.Path);
        FolderScanner.Sort(items, SortMode.Recent);

        Assert.Equal("New", items[0].Name);
    }
}
