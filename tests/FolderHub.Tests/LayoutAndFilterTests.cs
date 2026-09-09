using FolderHub.Services;
using Xunit;

namespace FolderHub.Tests;

public class GridLayoutTests
{
    [Theory]
    [InlineData(3, 3)]     // cabe numa linha
    [InlineData(4, 4)]
    [InlineData(6, 3)]     // 3x2 em vez de 4+2
    [InlineData(9, 3)]     // 3x3 certinho
    [InlineData(10, 5)]    // 5x2 sem buraco
    [InlineData(12, 4)]    // 4x3 sem buraco
    [InlineData(20, 5)]    // 5x4 sem buraco
    public void Prefers_grids_whose_last_row_is_full(int count, int expected)
    {
        Assert.Equal(expected, GridLayout.Columns(count, maxColumns: 7));
    }

    [Fact]
    public void Never_goes_below_three_columns()
    {
        Assert.Equal(3, GridLayout.Columns(1, maxColumns: 7));
        Assert.Equal(3, GridLayout.Columns(0, maxColumns: 7));
        Assert.Equal(3, GridLayout.Columns(50, maxColumns: 1));
    }

    [Fact]
    public void Never_goes_past_the_configured_maximum()
    {
        for (int count = 1; count <= 200; count++)
        {
            Assert.InRange(GridLayout.Columns(count, maxColumns: 5), 3, 5);
        }
    }

    [Fact]
    public void Rows_cover_every_item()
    {
        for (int count = 1; count <= 120; count++)
        {
            int columns = GridLayout.Columns(count, maxColumns: 7);
            Assert.True(GridLayout.Rows(count, columns) * columns >= count);
        }
    }

    [Fact]
    public void An_empty_hub_still_occupies_one_row()
    {
        Assert.Equal(1, GridLayout.Rows(0, 3));
    }
}

public class ItemFilterTests
{
    [Theory]
    [InlineData("Bloco de Notas", "blo")]
    [InlineData("Bloco de Notas", "NOTAS")]
    [InlineData("Bloco de Notas", "de no")]
    public void Finds_by_any_stretch_of_the_name(string name, string query)
    {
        Assert.True(ItemFilter.Matches(name, query));
    }

    [Theory]
    [InlineData("Informações do Sistema", "informacoes")]
    [InlineData("Informações do Sistema", "INFORMAÇÕES")]
    [InlineData("Área de Trabalho", "area")]
    [InlineData("Configurações", "configuracoes")]
    public void Ignores_accents_in_both_directions(string name, string query)
    {
        Assert.True(ItemFilter.Matches(name, query));
    }

    [Fact]
    public void An_empty_query_keeps_everything()
    {
        Assert.True(ItemFilter.Matches("Qualquer", ""));
        Assert.True(ItemFilter.Matches("Qualquer", "   "));
        Assert.True(ItemFilter.Matches("Qualquer", null));
    }

    [Fact]
    public void Something_that_is_not_there_does_not_match()
    {
        Assert.False(ItemFilter.Matches("Bloco de Notas", "photoshop"));
    }

    [Fact]
    public void Surrounding_spaces_in_the_query_are_ignored()
    {
        Assert.True(ItemFilter.Matches("Bloco de Notas", "  bloco  "));
    }
}

public class PathDisplayTests
{
    [Fact]
    public void The_home_folder_becomes_a_tilde()
    {
        string shortened = PathDisplay.Shorten(@"C:\Users\ana\Games", home: @"C:\Users\ana");

        Assert.Equal(@"~\Games", shortened);
    }

    [Fact]
    public void A_short_path_is_left_alone()
    {
        Assert.Equal(@"D:\Games", PathDisplay.Shorten(@"D:\Games", home: @"C:\Users\ana"));
    }

    [Fact]
    public void A_long_path_keeps_the_drive_and_the_last_two_folders()
    {
        string shortened = PathDisplay.Shorten(
            @"D:\Projects\Company\Client\Backend\Tools\Scripts", home: @"C:\Users\ana");

        Assert.Equal(@"D:\…\Tools\Scripts", shortened);
    }

    [Fact]
    public void Nothing_in_nothing_out()
    {
        Assert.Equal(string.Empty, PathDisplay.Shorten(null));
        Assert.Equal(string.Empty, PathDisplay.Shorten("   "));
    }

    [Fact]
    public void The_title_is_the_folder_itself()
    {
        Assert.Equal("Games", PathDisplay.FolderName(@"D:\Stuff\Games"));
        Assert.Equal("Hub", PathDisplay.FolderName(null));
    }
}
