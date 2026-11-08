using System.Linq;

namespace Squiggly.Engine.Tests;

public class GameFileTests
{
    // Seed 8812 starts with the head at 10,5, the tail at 9,5 and 8,5, and the food at 0,2.
    private const string SnakeHeader = "// squiggly v1 game=snake seed=8812";

    private static readonly string[] SnakeCanvas = Enumerable.Repeat("//|" + new string('.', 40) + "|", 10).Append("// keys:").ToArray();

    private static string[] WithKeys(string keys) => SnakeCanvas.Take(10).Append("// keys: " + keys).ToArray();

    [Fact]
    public void Ready_file_has_only_the_hud()
    {
        var file = GameFile.Read("// squiggly", new[] { "namespace Arcade;" });
        var mark = Assert.Single(file.Marks);
        Assert.Equal(new Mark(MarkKind.Hud, -1, 0, 11, mark.Message), mark);
        Assert.Null(file.State);
    }

    [Fact]
    public void New_game_has_hud_and_one_mark_per_lit_cell()
    {
        var file = GameFile.Read(SnakeHeader, SnakeCanvas);
        Assert.True(file.CanvasValid);
        Assert.Equal(
            new[]
            {
                new Mark(MarkKind.Hud, -1, 0, SnakeHeader.Length, "Snake, score 0, length 3, moves 0. Type w a s d at the end of the // keys: line to steer"),
                new Mark(MarkKind.Accent, 2, 3, 2, "food"),
                new Mark(MarkKind.Secondary, 5, 3 + 8 * 2, 2, "snake"),
                new Mark(MarkKind.Secondary, 5, 3 + 9 * 2, 2, "snake"),
                new Mark(MarkKind.Primary, 5, 3 + 10 * 2, 2, "snake head"),
            },
            file.Marks);
    }

    [Fact]
    public void Typed_keys_are_replayed_from_the_seed()
    {
        var file = GameFile.Read(SnakeHeader, WithKeys("ddw"));
        var snake = (SnakeState)file.State!;
        Assert.Equal(new Point(12, 4), snake.Head);
        Assert.Equal(3, snake.Tick);
        Assert.Equal("ddw", file.Keys);
        Assert.Contains(file.Marks, m => m.Kind == MarkKind.Primary && m.Row == 4 && m.Column == 3 + 12 * 2);
        Assert.Contains("moves 3", file.Marks[0].Message);
    }

    [Fact]
    public void Keys_that_mean_nothing_are_ignored()
    {
        Assert.Equal(GameFile.Read(SnakeHeader, WithKeys("dd")).State, GameFile.Read(SnakeHeader, WithKeys("dXd?! 5")).State);
    }

    [Fact]
    public void Keys_typed_after_game_over_change_nothing()
    {
        // Ten moves right from x=10 hit the wall on the tenth; the extra keys are ignored.
        var file = GameFile.Read(SnakeHeader, WithKeys("dddddddddddddddwwww"));
        Assert.Equal(GameStatus.Over, file.State!.Status);
        Assert.Equal(MarkKind.GameOver, file.Marks[0].Kind);
        Assert.StartsWith("GAME OVER, score 0", file.Marks[0].Message);
        Assert.Equal(GameFile.Read(SnakeHeader, WithKeys("dddddddddd")).State, file.State);
    }

    [Fact]
    public void Indented_files_keep_their_offsets()
    {
        var header = "    " + SnakeHeader;
        var file = GameFile.Read(header, SnakeCanvas.Select(row => "    " + row).ToArray());
        Assert.Equal(new Mark(MarkKind.Hud, -1, 4, SnakeHeader.Length, file.Marks[0].Message), file.Marks[0]);
        Assert.Equal(new Mark(MarkKind.Accent, 2, 7, 2, "food"), file.Marks[1]);
        Assert.Equal("    ", file.Indent);
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
    [InlineData(0, 40, true)]
    [InlineData(9, 40, true)]
    [InlineData(11, 40, true)]
    [InlineData(10, 38, true)]
    [InlineData(10, 40, false)]
    public void Missing_or_damaged_canvas_reports_one_error(int rows, int width, bool keysLine)
    {
        var canvas = Enumerable.Repeat("//|" + new string('.', width) + "|", rows)
            .Concat(keysLine ? new[] { "// keys:" } : new string[0])
            .Append("namespace Arcade;")
            .ToArray();
        var mark = Assert.Single(GameFile.Read(SnakeHeader, canvas).Marks);
        Assert.Equal(MarkKind.CanvasError, mark.Kind);
        Assert.Contains(rows == 0 ? "missing" : "damaged", mark.Message);
    }

    [Fact]
    public void Row_without_closing_bar_is_damaged()
    {
        var canvas = SnakeCanvas.ToArray();
        canvas[4] = "//|" + new string('.', 41);
        Assert.False(GameFile.Read(SnakeHeader, canvas).CanvasValid);
    }

    [Fact]
    public void CellAt_inverts_the_cell_column()
    {
        Assert.Equal(new Point(7, 2), GameFile.CellAt("  //|....", 2, Canvas.CellColumn(2, 7)));
    }
}
