using System.Linq;
using System.Threading.Tasks;
using Squiggly.Engine;
using Descriptors = Squiggly.Analyzers.Descriptors;

namespace Squiggly.Analyzers.Tests;

public class CodeFixTests
{
    private const string Footer = "namespace Arcade;\n";

    private static string SnakeFile(string header) =>
        header + "\n" + string.Concat(Enumerable.Repeat("//|" + new string('.', 40) + "|\n", 10)) + Footer;

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
    public async Task New_game_writes_header_and_canvas_and_nothing_else()
    {
        var document = FixHarness.Document("// squiggly\n" + Footer);
        var result = await new FixHarness(4242).ChooseAsync(document, Descriptors.Hud.Id, "Squiggly: New game: Snake");
        var expectedHeader = Header.Serialize(SnakeGame.Instance, SnakeGame.Instance.NewGame(4242));
        Assert.Equal(SnakeFile(expectedHeader), await FixHarness.TextAsync(result));
    }

    [Fact]
    public async Task Playing_menu_lists_moves_then_pause_and_restart()
    {
        var header = Header.Serialize(SnakeGame.Instance, SnakeGame.Instance.NewGame(7));
        var actions = await new FixHarness().ActionsAsync(FixHarness.Document(SnakeFile(header)), Descriptors.Hud.Id);
        Assert.Equal(
            new[] { "Squiggly: Move up", "Squiggly: Move down", "Squiggly: Move left", "Squiggly: Move right", "Squiggly: Wait", "Squiggly: Pause", "Squiggly: Restart" },
            actions.Select(a => a.Title));
    }

    [Fact]
    public async Task A_move_changes_only_the_header_line_to_the_predicted_state()
    {
        var state = SnakeGame.Instance.NewGame(7);
        var source = "using System;\n" + SnakeFile(Header.Serialize(SnakeGame.Instance, state));
        var result = await new FixHarness().ChooseAsync(FixHarness.Document(source), Descriptors.Hud.Id, "Squiggly: Move up");

        var expected = Header.Serialize(SnakeGame.Instance, SnakeGame.Instance.Step(state, SnakeGame.Up));
        var before = source.Split('\n');
        var after = (await FixHarness.TextAsync(result)).Split('\n');
        Assert.Equal(before.Length, after.Length);
        Assert.Equal(expected, after[1]);
        Assert.Equal(before.Where((_, i) => i != 1), after.Where((_, i) => i != 1));
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
        Assert.Equal(TetrisGame.Instance.Height + 3, lines.Length);
        Assert.All(lines.Skip(1).Take(TetrisGame.Instance.Height), line => Assert.Equal("  //|" + new string('.', 20) + "|", line));

        var moved = await harness.ChooseAsync(started, Descriptors.Hud.Id, "Squiggly: Hard drop");
        var movedLines = (await FixHarness.TextAsync(moved)).Split("\r\n");
        Assert.StartsWith("  // squiggly v1 game=tetris", movedLines[0]);
        Assert.Equal(lines.Skip(1), movedLines.Skip(1));
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
    public async Task Pause_then_resume()
    {
        var harness = new FixHarness();
        var document = FixHarness.Document(SnakeFile(Header.Serialize(SnakeGame.Instance, SnakeGame.Instance.NewGame(7))));
        var paused = await harness.ChooseAsync(document, Descriptors.Hud.Id, "Squiggly: Pause");
        Assert.Equal(new[] { "Squiggly: Resume", "Squiggly: Restart" }, (await harness.ActionsAsync(paused, Descriptors.Hud.Id)).Select(a => a.Title));
        var resumed = await harness.ChooseAsync(paused, Descriptors.Hud.Id, "Squiggly: Resume");
        Assert.Equal(await FixHarness.TextAsync(document), await FixHarness.TextAsync(resumed));
    }

    [Fact]
    public async Task Game_over_offers_play_again_and_switch_game()
    {
        var harness = new FixHarness(5);
        var header = "// squiggly v1 game=snake seed=1 tick=9 status=over score=2 dir=R head=19,5 tail=LL food=1,1";
        var document = FixHarness.Document(SnakeFile(header));
        var actions = await harness.ActionsAsync(document, Descriptors.GameOver.Id);
        Assert.Equal(new[] { "Squiggly: Play again", "Squiggly: Switch game" }, actions.Select(a => a.Title));

        var again = await harness.ChooseAsync(document, Descriptors.GameOver.Id, "Squiggly: Play again");
        Assert.Equal(SnakeFile(Header.Serialize(SnakeGame.Instance, SnakeGame.Instance.NewGame(5))), await FixHarness.TextAsync(again));
    }

    [Fact]
    public async Task Switching_from_tetris_to_snake_resizes_the_canvas()
    {
        // Each menu request draws one seed: 11 for Tetris, 12 for the game-over menu, 13 for Snake.
        var harness = new FixHarness(11, 12, 13);
        var tetris = await harness.ChooseAsync(FixHarness.Document("// squiggly\n" + Footer), Descriptors.Hud.Id, "Squiggly: New game: Tetris");
        var text = await FixHarness.TextAsync(tetris);
        var over = FixHarness.Document(text.Replace("status=playing", "status=over"));

        var ready = await harness.ChooseAsync(over, Descriptors.GameOver.Id, "Squiggly: Switch game");
        Assert.StartsWith("// squiggly\n//|", await FixHarness.TextAsync(ready));

        var snake = await harness.ChooseAsync(ready, Descriptors.Hud.Id, "Squiggly: New game: Snake");
        Assert.Equal(SnakeFile(Header.Serialize(SnakeGame.Instance, SnakeGame.Instance.NewGame(13))), await FixHarness.TextAsync(snake));
    }

    [Fact]
    public async Task Corrupt_header_offers_reset()
    {
        var harness = new FixHarness();
        var document = FixHarness.Document(SnakeFile("// squiggly v1 game=snake seed=banana"));
        var reset = await harness.ChooseAsync(document, Descriptors.HeaderError.Id, "Squiggly: Reset game");
        Assert.Equal(SnakeFile("// squiggly"), await FixHarness.TextAsync(reset));
    }

    [Fact]
    public async Task Damaged_canvas_offers_repair()
    {
        var header = Header.Serialize(SnakeGame.Instance, SnakeGame.Instance.NewGame(3));
        var damaged = SnakeFile(header).Replace("//|" + new string('.', 40) + "|\n//|", "//|.... oops\n//|");
        var harness = new FixHarness();
        var repaired = await harness.ChooseAsync(FixHarness.Document(damaged), Descriptors.CanvasError.Id, "Squiggly: Repair canvas");
        Assert.Equal(SnakeFile(header), await FixHarness.TextAsync(repaired));
    }

    [Fact]
    public async Task Missing_canvas_is_repaired_below_the_header()
    {
        var header = Header.Serialize(SnakeGame.Instance, SnakeGame.Instance.NewGame(3));
        var repaired = await new FixHarness().ChooseAsync(FixHarness.Document(header + "\n" + Footer), Descriptors.CanvasError.Id, "Squiggly: Repair canvas");
        Assert.Equal(SnakeFile(header), await FixHarness.TextAsync(repaired));
    }

    [Fact]
    public async Task Minesweeper_cell_fix_reveals_that_cell_and_edits_only_touched_rows()
    {
        var game = MinesGame.Instance;
        var state = game.NewGame(8812);
        var source = Header.Serialize(game, state) + "\n" + string.Concat(Enumerable.Repeat("//|" + new string('.', 32) + "|\n", 8)) + Footer;
        var document = FixHarness.Document(source);

        // Cell (5, 3): line 5 (0-based 4), column 3 + 5 * 2.
        var position = source.Split('\n').Take(4).Sum(line => line.Length + 1) + 3 + 10;
        var harness = new FixHarness();
        var actions = await harness.ActionsAsync(document, Descriptors.Covered.Id, position);
        Assert.Equal(new[] { "Squiggly: Reveal", "Squiggly: Flag" }, actions.Select(a => a.Title));

        var revealed = await FixHarness.ApplyAsync(document, actions[0]);
        var expected = game.Step(state, MinesGame.Reveal(new Point(5, 3)));
        var lines = (await FixHarness.TextAsync(revealed)).Split('\n');
        Assert.Equal(Header.Serialize(game, expected), lines[0]);
        Assert.Equal(game.Canvas(expected).Select(row => "//|" + row + "|"), lines.Skip(1).Take(8));
    }

    [Fact]
    public async Task Minesweeper_flag_cell_offers_unflag()
    {
        var game = MinesGame.Instance;
        var state = (MinesState)game.Step(game.NewGame(1), MinesGame.Flag(new Point(0, 0)));
        var source = Header.Serialize(game, state) + "\n" + string.Concat(Enumerable.Repeat("//|" + new string('.', 32) + "|\n", 8)) + Footer;
        var document = FixHarness.Document(source);
        var position = source.IndexOf('\n') + 1 + 3;
        var actions = await new FixHarness().ActionsAsync(document, Descriptors.Flag.Id, position);
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
                return;
            }

            Assert.Single(diagnostics, d => d.Id == Descriptors.Primary.Id);
            document = await harness.ChooseAsync(document, Descriptors.Hud.Id, "Squiggly: Move up");
        }

        Assert.Fail("Moving up forever should hit the wall");
    }
}
