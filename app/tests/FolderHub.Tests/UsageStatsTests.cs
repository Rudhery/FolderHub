using FolderHub.Models;
using FolderHub.Services;
using Xunit;

namespace FolderHub.Tests;

public class UsageStatsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    public UsageStatsTests()
    {
        UsageStats.ResetCache();
        try { File.Delete(UsageStats.FilePath); } catch { /* primeira vez */ }
    }

    private static AppItem Item(string folder, string name) => new()
    {
        Name = name,
        Path = Path.Combine(folder, name + ".lnk"),
        Extension = ".lnk",
        Modified = DateTime.UtcNow
    };

    [Fact]
    public void The_most_opened_come_first()
    {
        using var folder = new TempFolder();
        var a = Item(folder.Path, "A"); var b = Item(folder.Path, "B"); var c = Item(folder.Path, "C");

        UsageStats.Record(b.Path, Now.AddHours(-3));
        UsageStats.Record(b.Path, Now.AddHours(-2));
        UsageStats.Record(a.Path, Now.AddHours(-1));

        var top = UsageStats.Top([a, b, c], 4, Now);

        Assert.Equal(["B", "A"], top.Select(t => t.Item.Name));
        Assert.Equal("2× na semana", top[0].Meta);
    }

    [Fact]
    public void A_tie_goes_to_the_most_recent()
    {
        using var folder = new TempFolder();
        var a = Item(folder.Path, "A"); var b = Item(folder.Path, "B");

        UsageStats.Record(a.Path, Now.AddDays(-2));
        UsageStats.Record(b.Path, Now.AddDays(-1));

        Assert.Equal(["B", "A"], UsageStats.Top([a, b], 4, Now).Select(t => t.Item.Name));
    }

    [Fact]
    public void Opens_older_than_thirty_days_do_not_count()
    {
        using var folder = new TempFolder();
        var a = Item(folder.Path, "A");

        UsageStats.Record(a.Path, Now.AddDays(-40));

        Assert.Empty(UsageStats.Top([a], 4, Now));
    }

    [Fact]
    public void Outside_the_week_it_says_the_month()
    {
        using var folder = new TempFolder();
        var a = Item(folder.Path, "A");

        UsageStats.Record(a.Path, Now.AddDays(-10));
        UsageStats.Record(a.Path, Now.AddDays(-12));

        Assert.Equal("2× no mês", UsageStats.Top([a], 4, Now)[0].Meta);
    }

    [Fact]
    public void Counts_survive_a_restart()
    {
        using var folder = new TempFolder();
        var a = Item(folder.Path, "A");

        UsageStats.Record(a.Path, Now);
        UsageStats.ResetCache();

        Assert.Single(UsageStats.Top([a], 4, Now));
    }

    [Fact]
    public void Only_the_requested_number_comes_back()
    {
        using var folder = new TempFolder();
        var items = Enumerable.Range(1, 6).Select(i => Item(folder.Path, $"App{i}")).ToList();
        foreach (var item in items) UsageStats.Record(item.Path, Now);

        Assert.Equal(4, UsageStats.Top(items, 4, Now).Count);
    }
}
