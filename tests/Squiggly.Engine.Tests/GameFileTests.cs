using System.Linq;

namespace Squiggly.Engine.Tests;

public class GameFileTests
{
    private static readonly string[] SnakeCanvas = Enumerable.Repeat("//|" + new string('.', 40) + "|", 10).ToArray();

    [Fact]
    public void Ready_file_has_only_the_hud()
    {
        var file = GameFile.Read("// squiggly", new[] { "namespace Arcade;" });
        var mark = Assert.Single(file.Marks);
        Assert.Equal(MarkKind.Hud, mark.Kind);
        Assert.Equal(new Mark(MarkKind.Hud, -1, 0, 11, mark.Message), mark);
    }

    [Fact]
    public void Playing_file_has_hud_and_one_mark_per_lit_cell()
    {
        var header = "// squiggly v1 game=snake seed=8812 tick=37 status=playing score=4 dir=R head=10,5 tail=LL food=14,7";
        var file = GameFile.Read(header, SnakeCanvas);
        Assert.True(file.CanvasValid);
        Assert.Equal(
            new[]
            {
                new Mark(MarkKind.Hud, -1, 0, header.Length, "Snake, score 4, length 3, tick 37"),
                new Mark(MarkKind.Secondary, 5, 3 + 8 * 2, 2, "snake"),
                new Mark(MarkKind.Secondary, 5, 3 + 9 * 2, 2, "snake"),
                new Mark(MarkKind.Primary, 5, 3 + 10 * 2, 2, "snake head"),
                new Mark(MarkKind.Accent, 7, 3 + 14 * 2, 2, "food"),
            },
            file.Marks);
    }

    [Fact]
    public void Indented_files_keep_their_offsets()
    {
        var header = "    // squiggly v1 game=snake seed=1 tick=0 status=playing score=0 dir=R head=0,0 tail= food=-";
        var file = GameFile.Read(header, SnakeCanvas.Select(row => "    " + row).ToArray());
        Assert.Equal(new Mark(MarkKind.Hud, -1, 4, header.Length - 4, file.Marks[0].Message), file.Marks[0]);
        Assert.Equal(new Mark(MarkKind.Primary, 0, 7, 2, "snake head"), file.Marks[1]);
        Assert.Equal("    ", file.Indent);
    }

    [Fact]
    public void Game_over_replaces_the_hud()
    {
        var header = "// squiggly v1 game=snake seed=1 tick=9 status=over score=2 dir=R head=19,5 tail=LL food=1,1";
        var file = GameFile.Read(header, SnakeCanvas);
        Assert.Equal(MarkKind.GameOver, file.Marks[0].Kind);
        Assert.DoesNotContain(file.Marks, m => m.Kind == MarkKind.Hud);
        Assert.StartsWith("GAME OVER, score 2", file.Marks[0].Message);
    }

    [Fact]
    public void Bad_header_reports_one_error_and_nothing_else()
    {
        var file = GameFile.Read("// squiggly v1 game=snake seed=oops", SnakeCanvas);
        var mark = Assert.Single(file.Marks);
        Assert.Equal(MarkKind.HeaderError, mark.Kind);
        Assert.Contains("'seed'", mark.Message);
    }

    [Theory]
    [InlineData(0, 40)]
    [InlineData(9, 40)]
    [InlineData(11, 40)]
    [InlineData(10, 38)]
    public void Missing_or_damaged_canvas_reports_one_error(int rows, int width)
    {
        var header = "// squiggly v1 game=snake seed=1 tick=0 status=playing score=0 dir=R head=10,5 tail=LL food=1,1";
        var canvas = Enumerable.Repeat("//|" + new string('.', width) + "|", rows).Concat(new[] { "namespace Arcade;" }).ToArray();
        var mark = Assert.Single(GameFile.Read(header, canvas).Marks);
        Assert.Equal(MarkKind.CanvasError, mark.Kind);
        Assert.Contains(rows == 0 ? "missing" : "damaged", mark.Message);
    }

    [Fact]
    public void Row_without_closing_bar_is_damaged()
    {
        var header = "// squiggly v1 game=snake seed=1 tick=0 status=playing score=0 dir=R head=10,5 tail=LL food=1,1";
        var canvas = SnakeCanvas.ToArray();
        canvas[4] = "//|" + new string('.', 41);
        Assert.False(GameFile.Read(header, canvas).CanvasValid);
    }

    [Fact]
    public void CellAt_inverts_the_cell_column()
    {
        Assert.Equal(new Point(7, 2), GameFile.CellAt("  //|....", 2, Canvas.CellColumn(2, 7)));
    }
}
