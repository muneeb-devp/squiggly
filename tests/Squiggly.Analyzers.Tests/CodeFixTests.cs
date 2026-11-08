using System.Linq;
using System.Threading.Tasks;
using Squiggly.Engine;
using Descriptors = Squiggly.Analyzers.Descriptors;

namespace Squiggly.Analyzers.Tests;

public class CodeFixTests
{
    private const string Footer = "namespace Arcade;\n";

    private static string SnakeFile(string header, string keys = "") =>
        header + "\n" + string.Concat(Enumerable.Repeat("//|" + new string('.', 40) + "|\n", 10)) + Canvas.KeysLine("", keys) + "\n" + Footer;

    private static string MinesFile(uint seed, string keys = "", string[]? canvas = null) =>
        Header.Serialize(MinesGame.Instance, seed) + "\n"
        + string.Concat((canvas ?? Enumerable.Repeat(new string('.', 32), 8).ToArray()).Select(row => "//|" + row + "|\n"))
        + Canvas.KeysLine("", keys) + "\n" + Footer;

    [Fact]
    public async Task Ready_menu_offers_every_game()
    {
        var actions = await new FixHarness().ActionsAsync(FixHarness.Document("// squiggly\n" + Footer), Descriptors.Hud.Id);
        Assert.Equal(
            new[] { "Squiggly: New game: Snake", "Squiggly: New game: Tetris", "Squiggly: New game: Minesweeper" },
            actions.Select(a => a.Title));
        Assert.Equal(new[] { "squiggly.new.snake", "squiggly.new.tetris", "squiggly.new.mines" }, actions.Select(a => a.EquivalenceKey));
    }

    [Fact]
    public async Task New_game_writes_header_canvas_and_keys_line_and_nothing_else()
    {
        var document = FixHarness.Document("// squiggly\n" + Footer);
        var result = await new FixHarness(4242).ChooseAsync(document, Descriptors.Hud.Id, "Squiggly: New game: Snake");
        Assert.Equal(SnakeFile("// squiggly v1 game=snake seed=4242"), await FixHarness.TextAsync(result));
    }

    [Fact]
    public async Task Playing_menu_lists_moves_then_restart()
    {
        var actions = await new FixHarness().ActionsAsync(FixHarness.Document(SnakeFile("// squiggly v1 game=snake seed=7")), Descriptors.Hud.Id);
        Assert.Equal(
            new[] { "Squiggly: Move up", "Squiggly: Move down", "Squiggly: Move left", "Squiggly: Move right", "Squiggly: Wait", "Squiggly: Restart" },
            actions.Select(a => a.Title));
    }

    [Fact]
    public async Task A_menu_move_is_appended_to_the_keys_line_and_nothing_else_changes()
    {
        var source = "using System;\n" + SnakeFile("// squiggly v1 game=snake seed=7", "dd");
        var result = await new FixHarness().ChooseAsync(FixHarness.Document(source), Descriptors.Hud.Id, "Squiggly: Move up");
        Assert.Equal(source.Replace("// keys: dd", "// keys: ddw"), await FixHarness.TextAsync(result));
    }

    [Fact]
    public async Task Fixes_preserve_crlf_line_endings_and_indentation()
    {
        var source = "  // squiggly\r\nnamespace Arcade;\r\n";
        var harness = new FixHarness(99);
        var started = await harness.ChooseAsync(FixHarness.Document(source), Descriptors.Hud.Id, "Squiggly: New game: Tetris");
        var text = await FixHarness.TextAsync(started);

        Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
        var lines = text.Split("\r\n");
        Assert.Equal("  // squiggly v1 game=tetris seed=99", lines[0]);
        Assert.Equal(TetrisGame.Instance.Height + 4, lines.Length);
        Assert.All(lines.Skip(1).Take(TetrisGame.Instance.Height), line => Assert.Equal("  //|" + new string('.', 20) + "|", line));
        Assert.Equal("  // keys:", lines[TetrisGame.Instance.Height + 1]);

        var dropped = await harness.ChooseAsync(started, Descriptors.Hud.Id, "Squiggly: Hard drop");
        Assert.Equal(text.Replace("  // keys:", "  // keys: x"), await FixHarness.TextAsync(dropped));
    }

    [Fact]
    public async Task Preview_and_applied_change_are_identical()
    {
        var document = FixHarness.Document("// squiggly\n" + Footer);
        var actions = await new FixHarness(1, 2, 3).ActionsAsync(document, Descriptors.Hud.Id);
        var first = await FixHarness.TextAsync(await FixHarness.ApplyAsync(document, actions[0]));
        var second = await FixHarness.TextAsync(await FixHarness.ApplyAsync(document, actions[0]));
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Restart_starts_over_with_a_new_seed_and_no_moves()
    {
        var document = FixHarness.Document(SnakeFile("// squiggly v1 game=snake seed=7", "ddwwa"));
        var restarted = await new FixHarness(8).ChooseAsync(document, Descriptors.Hud.Id, "Squiggly: Restart");
        Assert.Equal(SnakeFile("// squiggly v1 game=snake seed=8"), await FixHarness.TextAsync(restarted));
    }

    [Fact]
    public async Task Game_over_offers_play_again_and_switch_game()
    {
        var harness = new FixHarness(5);
        var document = FixHarness.Document(SnakeFile("// squiggly v1 game=snake seed=1", "dddddddddd"));
        var actions = await harness.ActionsAsync(document, Descriptors.GameOver.Id);
        Assert.Equal(new[] { "Squiggly: Play again", "Squiggly: Switch game" }, actions.Select(a => a.Title));

        var again = await harness.ChooseAsync(document, Descriptors.GameOver.Id, "Squiggly: Play again");
        Assert.Equal(SnakeFile("// squiggly v1 game=snake seed=5"), await FixHarness.TextAsync(again));
    }

    [Fact]
    public async Task Switching_from_tetris_to_snake_resizes_the_canvas()
    {
        // Each menu request draws one seed: 11 for Tetris, 12 for the game-over menu, 13 for Snake.
        var harness = new FixHarness(11, 12, 13);
        var tetris = await harness.ChooseAsync(FixHarness.Document("// squiggly\n" + Footer), Descriptors.Hud.Id, "Squiggly: New game: Tetris");
        var over = FixHarness.Document((await FixHarness.TextAsync(tetris)).Replace("// keys:", "// keys: " + new string('x', 60)));

        var ready = await harness.ChooseAsync(over, Descriptors.GameOver.Id, "Squiggly: Switch game");
        Assert.StartsWith("// squiggly\n//|", await FixHarness.TextAsync(ready));

        var snake = await harness.ChooseAsync(ready, Descriptors.Hud.Id, "Squiggly: New game: Snake");
        Assert.Equal(SnakeFile("// squiggly v1 game=snake seed=13"), await FixHarness.TextAsync(snake));
    }

    [Fact]
    public async Task Corrupt_header_offers_reset()
    {
        var document = FixHarness.Document(SnakeFile("// squiggly v1 game=snake seed=banana"));
        var reset = await new FixHarness().ChooseAsync(document, Descriptors.HeaderError.Id, "Squiggly: Reset game");
        Assert.Equal(SnakeFile("// squiggly"), await FixHarness.TextAsync(reset));
    }

    [Fact]
    public async Task Damaged_canvas_is_repaired_and_keeps_the_moves()
    {
        var header = "// squiggly v1 game=snake seed=3";
        var damaged = SnakeFile(header, "ddw").Replace("//|" + new string('.', 40) + "|\n//|", "//|.... oops\n//|");
        var repaired = await new FixHarness().ChooseAsync(FixHarness.Document(damaged), Descriptors.CanvasError.Id, "Squiggly: Repair canvas");
        Assert.Equal(SnakeFile(header, "ddw"), await FixHarness.TextAsync(repaired));
    }

    [Fact]
    public async Task Missing_canvas_is_repaired_below_the_header()
    {
        var header = "// squiggly v1 game=snake seed=3";
        var repaired = await new FixHarness().ChooseAsync(FixHarness.Document(header + "\n" + Footer), Descriptors.CanvasError.Id, "Squiggly: Repair canvas");
        Assert.Equal(SnakeFile(header), await FixHarness.TextAsync(repaired));
    }

    [Fact]
    public async Task Minesweeper_cell_fix_records_the_move_and_rewrites_only_the_board()
    {
        var game = MinesGame.Instance;
        var source = MinesFile(8812);
        var document = FixHarness.Document(source);

        // Cell (5, 3): line 5 (0-based 4), column 3 + 5 * 2.
        var position = source.Split('\n').Take(4).Sum(line => line.Length + 1) + 3 + 10;
        var harness = new FixHarness();
        var actions = await harness.ActionsAsync(document, Descriptors.Covered.Id, position);
        Assert.Equal(new[] { "Squiggly: Reveal", "Squiggly: Flag" }, actions.Select(a => a.Title));

        var revealed = await FixHarness.ApplyAsync(document, actions[0]);
        var expected = GameFile.Replay(game, game.NewGame(8812), "r5,3");
        Assert.Equal(MinesFile(8812, "r5,3", game.Canvas(expected).ToArray()), await FixHarness.TextAsync(revealed));
    }

    [Fact]
    public async Task Minesweeper_flag_cell_offers_unflag()
    {
        var source = MinesFile(1, "f0,0");
        var position = source.IndexOf('\n') + 1 + 3;
        var actions = await new FixHarness().ActionsAsync(FixHarness.Document(source), Descriptors.Flag.Id, position);
        Assert.Equal(new[] { "Squiggly: Unflag" }, actions.Select(a => a.Title));
    }

    [Fact]
    public async Task Snake_can_be_played_to_game_over_through_code_fixes_alone()
    {
        var harness = new FixHarness(2026);
        var document = await harness.ChooseAsync(FixHarness.Document("// squiggly\n" + Footer), Descriptors.Hud.Id, "Squiggly: New game: Snake");
        for (var move = 0; move < 100; move++)
        {
            var diagnostics = await FixHarness.DiagnosticsAsync(document);
            if (diagnostics.Any(d => d.Id == Descriptors.GameOver.Id))
            {
                Assert.Contains(diagnostics, d => d.Id == Descriptors.Primary.Id);
                Assert.EndsWith("// keys: " + new string('w', move) + "\n" + Footer, await FixHarness.TextAsync(document));
                return;
            }

            Assert.Single(diagnostics, d => d.Id == Descriptors.Primary.Id);
            document = await harness.ChooseAsync(document, Descriptors.Hud.Id, "Squiggly: Move up");
        }

        Assert.Fail("Moving up forever should hit the wall");
    }
}
