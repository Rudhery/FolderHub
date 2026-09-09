using FolderHub.Services;
using Xunit;

namespace FolderHub.Tests;

public class ManualOrderTests
{
    private static List<Models.AppItem> Read(TempFolder folder)
    {
        var items = FolderScanner.Scan(folder.Path);
        FolderScanner.Sort(items, SortMode.Manual);
        return items;
    }

    [Fact]
    public void Apply_writes_the_order_as_a_numeric_prefix()
    {
        using var folder = new TempFolder();
        folder.Add("Bloco.lnk", "Calc.lnk", "Explorer.lnk");

        var items = Read(folder);
        var moved = items.Skip(1).Append(items[0]).ToList();   // primeiro vai para o fim

        ManualOrder.Apply(moved);

        Assert.Equal(["01 - Calc.lnk", "02 - Explorer.lnk", "03 - Bloco.lnk"], folder.Files());
    }

    [Fact]
    public void The_prefix_never_leaks_into_the_card_name()
    {
        using var folder = new TempFolder();
        folder.Add("Bloco.lnk", "Calc.lnk");

        ManualOrder.Apply(Read(folder));

        Assert.Equal(["Bloco", "Calc"], Read(folder).Select(i => i.Name));
    }

    [Fact]
    public void Applying_the_same_order_twice_touches_nothing()
    {
        using var folder = new TempFolder();
        folder.Add("01 - A.lnk", "02 - B.lnk");

        var renamed = ManualOrder.Apply(Read(folder));

        Assert.Empty(renamed);
    }

    [Fact]
    public void Swapping_two_items_survives_the_name_collision()
    {
        // O nome final de A ("02 - A.lnk") não colide, mas o de B ("01 - B.lnk")
        // exige que A saia de "01 - A.lnk" antes. É o caso que quebra um
        // rename ingênuo de uma passada só.
        using var folder = new TempFolder();
        folder.Add("01 - A.lnk", "02 - B.lnk");

        var items = Read(folder);
        items.Reverse();
        ManualOrder.Apply(items);

        Assert.Equal(["01 - B.lnk", "02 - A.lnk"], folder.Files());
        Assert.Equal(["B", "A"], Read(folder).Select(i => i.Name));
    }

    [Fact]
    public void Apply_leaves_no_temporary_files_behind()
    {
        using var folder = new TempFolder();
        folder.Add("A.lnk", "B.lnk", "C.lnk");

        var items = Read(folder);
        items.Reverse();
        ManualOrder.Apply(items);

        Assert.Empty(Directory.GetFiles(folder.Path, "*.fhtmp"));
    }

    [Fact]
    public void Reordering_keeps_every_file()
    {
        using var folder = new TempFolder();
        folder.Add("A.lnk", "B.lnk", "C.lnk", "D.lnk", "E.lnk");

        var items = Read(folder);
        items.Reverse();
        ManualOrder.Apply(items);

        Assert.Equal(5, Directory.GetFiles(folder.Path).Length);
        Assert.Equal(["E", "D", "C", "B", "A"], Read(folder).Select(i => i.Name));
    }

    [Fact]
    public void Past_ninety_nine_items_the_prefix_grows_to_three_digits()
    {
        using var folder = new TempFolder();
        folder.Add([.. Enumerable.Range(1, 100).Select(i => $"App{i:000}.lnk")]);

        ManualOrder.Apply(Read(folder));

        Assert.StartsWith("001 - ", folder.Files()[0], StringComparison.Ordinal);
    }
}
