using FolderHub.Models;
using FolderHub.Services;
using Xunit;

namespace FolderHub.Tests;

public class HubConfigMigrationTests
{
    [Fact]
    public void Old_single_folder_config_becomes_one_tab()
    {
        var config = new HubConfig { FolderPath = @"D:\Games" };

        Assert.True(config.Migrate());

        Assert.Single(config.Tabs);
        Assert.Equal(@"D:\Games", config.Tabs[0].Path);
        Assert.Null(config.FolderPath);
    }

    [Fact]
    public void Migrating_twice_changes_nothing_the_second_time()
    {
        var config = new HubConfig { FolderPath = @"D:\Games" };

        config.Migrate();

        Assert.False(config.Migrate());
        Assert.Single(config.Tabs);
    }

    [Fact]
    public void Tabs_already_set_win_over_the_legacy_field()
    {
        var config = new HubConfig
        {
            FolderPath = @"D:\Old",
            Tabs = [new TabConfig { Path = @"D:\New" }]
        };

        Assert.True(config.Migrate());

        Assert.Single(config.Tabs);
        Assert.Equal(@"D:\New", config.Tabs[0].Path);
        Assert.Null(config.FolderPath);
    }

    [Fact]
    public void Entries_without_a_path_are_dropped()
    {
        var config = new HubConfig
        {
            Tabs = [new TabConfig { Path = @"D:\Games" }, new TabConfig { Path = "  " }]
        };

        Assert.True(config.Migrate());
        Assert.Single(config.Tabs);
    }

    [Fact]
    public void A_config_that_is_already_fine_is_left_alone()
    {
        var config = new HubConfig { Tabs = [new TabConfig { Path = @"D:\Games" }] };

        Assert.False(config.Migrate());
    }
}

public class HubTabTests
{
    [Fact]
    public void Without_a_custom_name_the_tab_is_called_after_the_folder()
    {
        var tab = new HubTab { Path = @"D:\Work\Tools" };

        Assert.Equal("Tools", tab.Name);
    }

    [Fact]
    public void A_custom_name_wins()
    {
        var tab = new HubTab { Path = @"D:\Work\Tools", CustomName = "Trabalho" };

        Assert.Equal("Trabalho", tab.Name);
    }

    [Fact]
    public void Blank_custom_name_falls_back_to_the_folder()
    {
        var tab = new HubTab { Path = @"D:\Work\Tools", CustomName = "   " };

        Assert.Equal("Tools", tab.Name);
    }

    [Fact]
    public void A_new_tab_has_not_been_visited_and_holds_no_icons()
    {
        var tab = new HubTab { Path = @"D:\Work" };

        Assert.False(tab.Visited);
        Assert.Empty(tab.Items);
        Assert.Empty(tab.Icons);
    }
}

public class CountSupportedTests
{
    [Fact]
    public void The_cheap_count_agrees_with_a_full_scan()
    {
        using var folder = new TempFolder();
        folder.Add("Steam.lnk", "GitHub.url", "Tool.exe", "notes.txt", "photo.png", "run.bat");

        Assert.Equal(FolderScanner.Scan(folder.Path).Count, FolderScanner.CountSupported(folder.Path));
    }

    [Fact]
    public void Counting_a_folder_that_is_not_there_gives_zero()
    {
        Assert.Equal(0, FolderScanner.CountSupported(Path.Combine(Path.GetTempPath(), "folderhub-nope")));
    }

    [Fact]
    public void Counting_nothing_gives_zero()
    {
        Assert.Equal(0, FolderScanner.CountSupported(""));
    }
}
