using System.Collections.Generic;
using System.Linq;

namespace Squiggly.Engine.Tests;

public class ControllerTests
{
    private static GameFile File(string header, IReadOnlyList<string>? canvas = null) =>
        GameFile.Read(header, (canvas ?? new string[0]).Concat(new[] { "namespace Arcade;" }).ToArray());

    private static GameFile Playing(IGame game, GameStatus status = GameStatus.Playing)
    {
        var state = game.NewGame(77) with { Status = status };
        return File(Header.Serialize(game, state), Canvas.Lines(game, state, ""));
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
    public void Playing_snake_offers_moves_then_pause_and_restart()
    {
        Assert.Equal(
            new[] { "Squiggly: Move up", "Squiggly: Move down", "Squiggly: Move left", "Squiggly: Move right", "Squiggly: Wait", "Squiggly: Pause", "Squiggly: Restart" },
            Titles(Controller.Menu(Playing(SnakeGame.Instance))));
    }

    [Fact]
    public void Playing_tetris_offers_its_actions()
    {
        Assert.Equal(
            new[] { "Squiggly: Move left", "Squiggly: Move right", "Squiggly: Rotate", "Squiggly: Soft drop", "Squiggly: Hard drop", "Squiggly: Pause", "Squiggly: Restart" },
            Titles(Controller.Menu(Playing(TetrisGame.Instance))));
    }

    [Fact]
    public void Paused_offers_resume_and_restart()
    {
        Assert.Equal(new[] { "Squiggly: Resume", "Squiggly: Restart" }, Titles(Controller.Menu(Playing(SnakeGame.Instance, GameStatus.Paused))));
    }

    [Fact]
    public void Over_offers_play_again_and_switch_game()
    {
        Assert.Equal(new[] { "Squiggly: Play again", "Squiggly: Switch game" }, Titles(Controller.Menu(Playing(SnakeGame.Instance, GameStatus.Over))));
    }

    [Fact]
    public void Bad_header_offers_reset_only()
    {
        var menu = Controller.Menu(File("// squiggly v1 game=pong"));
        Assert.Equal(new[] { "Squiggly: Reset game" }, Titles(menu));
        Assert.Equal(new FileEdit("// squiggly", null), Controller.Apply(File("// squiggly v1 game=pong"), menu[0], 1));
    }

    [Fact]
    public void Damaged_canvas_offers_repair_which_rewrites_it()
    {
        var game = SnakeGame.Instance;
        var state = game.NewGame(3);
        var file = File(Header.Serialize(game, state), new[] { "//|....|" });
        Assert.False(file.CanvasValid);
        var menu = Controller.Menu(file);
        Assert.Equal(new[] { "Squiggly: Repair canvas" }, Titles(menu));

        var edit = Controller.Apply(file, menu[0], 99);
        Assert.Equal(Header.Serialize(game, state), edit.Header);
        Assert.Equal(game.Canvas(state), edit.CanvasRows);
    }

    [Fact]
    public void Equivalence_keys_are_stable_and_unique()
    {
        var keys = Controller.Menu(Playing(SnakeGame.Instance)).Select(c => c.Key).ToArray();
        Assert.Equal(new[] { "squiggly.snake.up", "squiggly.snake.down", "squiggly.snake.left", "squiggly.snake.right", "squiggly.snake.wait", "squiggly.pause", "squiggly.restart" }, keys);
    }

    [Fact]
    public void New_game_writes_header_and_canvas_from_the_given_seed()
    {
        var menu = Controller.Menu(File("// squiggly"));
        var edit = Controller.Apply(File("// squiggly"), menu[0], 4242);
        Assert.Equal(Header.Serialize(SnakeGame.Instance, SnakeGame.Instance.NewGame(4242)), edit.Header);
        Assert.Equal(10, edit.CanvasRows!.Count);
        Assert.All(edit.CanvasRows, row => Assert.Equal(new string('.', 40), row));
    }

    [Fact]
    public void Actions_step_the_game_and_leave_static_canvases_alone()
    {
        var file = Playing(SnakeGame.Instance);
        var up = Controller.Menu(file)[0];
        var edit = Controller.Apply(file, up, 1);
        var expected = SnakeGame.Instance.Step(file.Header.State!, SnakeGame.Up);
        Assert.Equal(Header.Serialize(SnakeGame.Instance, expected), edit.Header);
        Assert.Null(edit.CanvasRows);
    }

    [Fact]
    public void Pause_and_resume_do_not_advance_the_tick()
    {
        var file = Playing(SnakeGame.Instance);
        var pause = Controller.Menu(file).Single(c => c.Kind == CommandKind.Pause);
        var paused = Header.Parse(Controller.Apply(file, pause, 1).Header).State!;
        Assert.Equal(GameStatus.Paused, paused.Status);
        Assert.Equal(file.Header.State!.Tick, paused.Tick);
    }

    [Fact]
    public void Switch_game_returns_to_ready()
    {
        var file = Playing(TetrisGame.Instance, GameStatus.Over);
        var edit = Controller.Apply(file, Controller.Menu(file)[1], 1);
        Assert.Equal("// squiggly", edit.Header);
    }

    [Fact]
    public void Minesweeper_cells_offer_reveal_and_flag_and_rewrite_the_canvas()
    {
        var file = Playing(MinesGame.Instance);
        Assert.Equal(new[] { "Squiggly: Pause", "Squiggly: Restart" }, Titles(Controller.Menu(file)));
        var cellMenu = Controller.CellMenu(file, new Point(4, 4));
        Assert.Equal(new[] { "Squiggly: Reveal", "Squiggly: Flag" }, Titles(cellMenu));
        Assert.Equal(new[] { "squiggly.mines.reveal", "squiggly.mines.flag" }, cellMenu.Select(c => c.Key));

        var edit = Controller.Apply(file, cellMenu[0], 1);
        Assert.NotNull(edit.CanvasRows);
        Assert.NotEqual(MinesGame.Instance.Canvas(file.Header.State!), edit.CanvasRows);
    }

    [Fact]
    public void Typed_keys_add_save_progress_which_folds_them_into_the_header()
    {
        var game = SnakeGame.Instance;
        var state = game.NewGame(77);
        var lines = Canvas.Lines(game, state, "").Take(10).Append("// keys: ddw").ToArray();
        var file = File(Header.Serialize(game, state), lines);
        var menu = Controller.Menu(file);
        Assert.Equal("Squiggly: Save progress (move typed keys into the header)", menu.Last().Title);

        var edit = Controller.Apply(file, menu.Last(), 1);
        var expected = GameFile.Replay(game, state, "ddw");
        Assert.Equal(new FileEdit(Header.Serialize(game, expected), null, ""), edit);
    }

    [Fact]
    public void Menu_moves_act_on_the_state_after_typed_keys()
    {
        var game = SnakeGame.Instance;
        var state = game.NewGame(77);
        var lines = Canvas.Lines(game, state, "").Take(10).Append("// keys: dd").ToArray();
        var file = File(Header.Serialize(game, state), lines);
        var edit = Controller.Apply(file, Controller.Menu(file)[0], 1);
        Assert.Equal(Header.Serialize(game, game.Step(GameFile.Replay(game, state, "dd"), SnakeGame.Up)), edit.Header);
        Assert.Equal("", edit.Keys);
    }

    [Fact]
    public void Snake_cells_offer_nothing()
    {
        Assert.Empty(Controller.CellMenu(Playing(SnakeGame.Instance), new Point(10, 5)));
    }
}
