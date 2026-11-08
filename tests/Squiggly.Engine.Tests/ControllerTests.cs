using System.Collections.Generic;
using System.Linq;

namespace Squiggly.Engine.Tests;

public class ControllerTests
{
    private static GameFile File(string header, IEnumerable<string>? block = null) =>
        GameFile.Read(header, (block ?? new string[0]).Concat(new[] { "namespace Arcade;" }).ToArray());

    /// <summary>A file for <paramref name="game"/> from seed 77 with <paramref name="keys"/> on the keys line.</summary>
    private static GameFile Game(IGame game, string keys = "")
    {
        var lines = Canvas.Lines(game, game.NewGame(77), "").ToArray();
        lines[lines.Length - 1] = Canvas.KeysLine("", keys);
        return File(Header.Serialize(game, 77), lines);
    }

    private static string[] Titles(IEnumerable<Command> commands) => commands.Select(c => c.Title).ToArray();

    [Fact]
    public void Ready_offers_every_new_game()
    {
        Assert.Equal(
            new[] { "Squiggly: New game: Snake", "Squiggly: New game: Tetris", "Squiggly: New game: Minesweeper" },
            Titles(Controller.Menu(File("// squiggly"))));
    }

    [Fact]
    public void Playing_snake_offers_moves_then_restart()
    {
        Assert.Equal(
            new[] { "Squiggly: Move up", "Squiggly: Move down", "Squiggly: Move left", "Squiggly: Move right", "Squiggly: Wait", "Squiggly: Restart" },
            Titles(Controller.Menu(Game(SnakeGame.Instance))));
    }

    [Fact]
    public void Playing_tetris_offers_its_actions()
    {
        Assert.Equal(
            new[] { "Squiggly: Move left", "Squiggly: Move right", "Squiggly: Rotate", "Squiggly: Soft drop", "Squiggly: Hard drop", "Squiggly: Restart" },
            Titles(Controller.Menu(Game(TetrisGame.Instance))));
    }

    [Fact]
    public void Over_offers_play_again_and_switch_game()
    {
        Assert.Equal(new[] { "Squiggly: Play again", "Squiggly: Switch game" }, Titles(Controller.Menu(Game(SnakeGame.Instance, "dddddddddd"))));
    }

    [Fact]
    public void Bad_header_offers_reset_only()
    {
        var file = File("// squiggly v1 game=pong seed=1");
        var menu = Controller.Menu(file);
        Assert.Equal(new[] { "Squiggly: Reset game" }, Titles(menu));
        Assert.Equal(new FileEdit("// squiggly", null), Controller.Apply(file, menu[0], 1));
    }

    [Fact]
    public void Damaged_canvas_offers_repair_which_keeps_the_moves()
    {
        var game = SnakeGame.Instance;
        var file = File(Header.Serialize(game, 3), new[] { "//|....|", "// keys: ddw" });
        Assert.False(file.CanvasValid);
        var menu = Controller.Menu(file);
        Assert.Equal(new[] { "Squiggly: Repair canvas" }, Titles(menu));

        var edit = Controller.Apply(file, menu[0], 99);
        Assert.Equal(Header.Serialize(game, 3), edit.Header);
        Assert.Equal(game.Canvas(game.NewGame(3)), edit.CanvasRows);
        Assert.Equal("ddw", edit.Keys);
    }

    [Fact]
    public void Equivalence_keys_are_stable_and_unique()
    {
        var keys = Controller.Menu(Game(SnakeGame.Instance)).Select(c => c.Key).ToArray();
        Assert.Equal(new[] { "squiggly.snake.up", "squiggly.snake.down", "squiggly.snake.left", "squiggly.snake.right", "squiggly.snake.wait", "squiggly.restart" }, keys);
    }

    [Fact]
    public void New_game_writes_header_canvas_and_empty_keys_from_the_given_seed()
    {
        var file = File("// squiggly");
        var edit = Controller.Apply(file, Controller.Menu(file)[0], 4242);
        Assert.Equal("// squiggly v1 game=snake seed=4242", edit.Header);
        Assert.Equal(10, edit.CanvasRows!.Count);
        Assert.All(edit.CanvasRows, row => Assert.Equal(new string('.', 40), row));
        Assert.Equal("", edit.Keys);
    }

    [Theory]
    [InlineData(0, "dd", "ddw")]
    [InlineData(3, "dd", "ddd")]
    [InlineData(4, "ddw", "ddww")]
    public void Menu_moves_are_recorded_on_the_keys_line(int entry, string keys, string expected)
    {
        var file = Game(SnakeGame.Instance, keys);
        var edit = Controller.Apply(file, Controller.Menu(file)[entry], 1);
        Assert.Equal(new FileEdit(Header.Serialize(SnakeGame.Instance, 77), null, expected), edit);
    }

    [Fact]
    public void Switch_game_returns_to_ready()
    {
        var file = Game(TetrisGame.Instance, new string('x', 60));
        Assert.Equal(GameStatus.Over, file.State!.Status);
        var edit = Controller.Apply(file, Controller.Menu(file)[1], 1);
        Assert.Equal("// squiggly", edit.Header);
    }

    [Fact]
    public void Minesweeper_cells_offer_reveal_and_flag_and_record_the_move()
    {
        var file = Game(MinesGame.Instance);
        Assert.Equal(new[] { "Squiggly: Restart" }, Titles(Controller.Menu(file)));
        var cellMenu = Controller.CellMenu(file, new Point(4, 4));
        Assert.Equal(new[] { "Squiggly: Reveal", "Squiggly: Flag" }, Titles(cellMenu));
        Assert.Equal(new[] { "squiggly.mines.reveal", "squiggly.mines.flag" }, cellMenu.Select(c => c.Key));

        var edit = Controller.Apply(file, cellMenu[0], 1);
        Assert.Equal("r4,4", edit.Keys);
        Assert.Equal(MinesGame.Instance.Canvas(GameFile.Replay(MinesGame.Instance, MinesGame.Instance.NewGame(77), "r4,4")), edit.CanvasRows);

        var next = Game(MinesGame.Instance, "r4,4");
        var open = ((MinesState)next.State!).Open;
        var covered = Enumerable.Range(0, 128).Select(i => new Point(i % 16, i / 16)).First(p => !open[p]);
        var flag = Controller.CellMenu(next, covered)[1];
        Assert.Equal("r4,4 f" + covered, Controller.Apply(next, flag, 1).Keys);
    }

    [Fact]
    public void Snake_cells_offer_nothing()
    {
        Assert.Empty(Controller.CellMenu(Game(SnakeGame.Instance), new Point(10, 5)));
    }
}
