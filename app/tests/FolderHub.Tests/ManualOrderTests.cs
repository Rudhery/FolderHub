using FolderHub.Services;
using Xunit;

namespace FolderHub.Tests;

public class ManualOrderTests
{
    public ManualOrderTests() => ManualOrder.ResetCache();

    private static List<Models.AppItem> Read(TempFolder folder)
    {
        var items = FolderScanner.Scan(folder.Path);
        FolderScanner.Sort(items, SortMode.Manual, folder.Path);
        return items;
    }

    private static string[] Names(TempFolder folder) => [.. Read(folder).Select(i => i.Name)];

    [Fact]
    public void Saving_the_order_never_renames_a_file()
    {
        using var folder = new TempFolder();
        folder.Add("Bloco.lnk", "Calc.lnk", "Explorer.lnk");

        var items = Read(folder);
        ManualOrder.Save(folder.Path, items.Skip(1).Append(items[0]));   // primeiro vai para o fim

        Assert.Equal(["Bloco.lnk", "Calc.lnk", "Explorer.lnk"], folder.Files());
        Assert.Equal(["Calc", "Explorer", "Bloco"], Names(folder));
    }

    [Fact]
    public void The_order_survives_reloading_from_disk()
    {
        using var folder = new TempFolder();
        folder.Add("A.lnk", "B.lnk", "C.lnk");

        var items = Read(folder);
        items.Reverse();
        ManualOrder.Save(folder.Path, items);
        ManualOrder.ResetCache();

        Assert.Equal(["C", "B", "A"], Names(folder));
    }

    [Fact]
    public void A_shortcut_recreated_by_an_update_keeps_its_place()
    {
        // O bug que motivou a mudança: o instalador apaga e recria "Steam.lnk".
        using var folder = new TempFolder();
        folder.Add("Discord.lnk", "Steam.lnk", "Zoom.lnk");
        ManualOrder.Save(folder.Path, ["Zoom.lnk", "Steam.lnk", "Discord.lnk"]);

        File.Delete(Path.Combine(folder.Path, "Steam.lnk"));
        folder.Add("Steam.lnk");

        Assert.Equal(["Zoom", "Steam", "Discord"], Names(folder));
        Assert.Equal(3, folder.Files().Length);
    }

    [Fact]
    public void New_shortcuts_go_to_the_end_by_name()
    {
        using var folder = new TempFolder();
        folder.Add("B.lnk", "A.lnk");
        ManualOrder.Save(folder.Path, ["B.lnk", "A.lnk"]);

        folder.Add("Zeta.lnk", "Alfa.lnk");

        Assert.Equal(["B", "A", "Alfa", "Zeta"], Names(folder));
    }

    [Fact]
    public void Without_a_saved_order_it_is_by_name()
    {
        using var folder = new TempFolder();
        folder.Add("item10.lnk", "item2.lnk", "Abc.lnk");

        Assert.Equal(["Abc", "item2", "item10"], Names(folder));
    }

    [Fact]
    public void Migration_strips_old_prefixes_and_keeps_their_order()
    {
        using var folder = new TempFolder();
        folder.Add("01 - Steam.lnk", "02 - Calc.lnk", "03 - Bloco.lnk");

        ManualOrder.MigrateLegacyPrefixes(folder.Path);

        Assert.Equal(["Bloco.lnk", "Calc.lnk", "Steam.lnk"], folder.Files());
        Assert.Equal(["Steam", "Calc", "Bloco"], Names(folder));
    }

    [Fact]
    public void Migration_removes_the_duplicate_an_update_left_behind()
    {
        // "02 - Steam.lnk" é o antigo; "Steam.lnk" é o que a atualização criou.
        using var folder = new TempFolder();
        folder.Add("01 - Calc.lnk", "02 - Steam.lnk", "03 - Bloco.lnk", "Steam.lnk");

        ManualOrder.MigrateLegacyPrefixes(folder.Path);

        Assert.Equal(["Bloco.lnk", "Calc.lnk", "Steam.lnk"], folder.Files());
        Assert.Equal(["Calc", "Steam", "Bloco"], Names(folder));
    }

    [Fact]
    public void Migration_leaves_names_that_only_start_with_a_digit()
    {
        using var folder = new TempFolder();
        folder.Add("7 Days to Die.lnk", "2048.lnk");

        Assert.Equal(0, ManualOrder.MigrateLegacyPrefixes(folder.Path));
        Assert.Equal(["2048.lnk", "7 Days to Die.lnk"], folder.Files());
    }

    [Fact]
    public void Migrating_twice_changes_nothing_the_second_time()
    {
        using var folder = new TempFolder();
        folder.Add("01 - A.lnk", "02 - B.lnk");

        ManualOrder.MigrateLegacyPrefixes(folder.Path);

        Assert.Equal(0, ManualOrder.MigrateLegacyPrefixes(folder.Path));
    }
}
