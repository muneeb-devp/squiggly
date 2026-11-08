using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Squiggly.Engine;
using Descriptors = Squiggly.Analyzers.Descriptors;

namespace Squiggly.Analyzers.Tests;

public class AnalyzerTests
{
    private static Task Verify(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<SquigglyAnalyzer, DefaultVerifier> { TestCode = source };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    public async Task Ordinary_files_get_no_diagnostics()
    {
        await Verify("""
            namespace Arcade;

            // This mentions squiggly, and "// squiggly" below sits on line 6, outside the header window.


            // squiggly v1 game=snake
            internal static class Squiggly
            {
                public const string Name = "// squiggly";
            }
            """);
    }

    [Fact]
    public async Task Bare_header_shows_the_ready_hud()
    {
        await Verify("""
            {|SQ0010:// squiggly|}
            namespace Arcade;
            """);
    }

    [Fact]
    public async Task Header_may_follow_using_directives()
    {
        await Verify("""
            using System;

            {|SQ0010:// squiggly v1|}
            namespace Arcade;
            """);
    }

    // Seed 8812 starts with the head at 10,5, the tail at 9,5 and 8,5, and the food at 0,2.
    private const string Snake8812 = "// squiggly v1 game=snake seed=8812";

    [Fact]
    public async Task Snake_frame_lights_exactly_the_expected_cells()
    {
        await Verify("""
            {|SQ0010:// squiggly v1 game=snake seed=8812|}
            //|........................................|
            //|........................................|
            //|{|SQ0003:..|}......................................|
            //|........................................|
            //|........................................|
            //|................{|SQ0002:..|}{|SQ0002:..|}{|SQ0001:..|}..................|
            //|........................................|
            //|........................................|
            //|........................................|
            //|........................................|
            // keys:
            namespace Arcade;
            """);
    }

    [Fact]
    public async Task Typing_keys_moves_the_snake_without_a_code_fix()
    {
        // Two moves right then one up: the head goes from 10,5 to 12,4.
        await Verify("""
            {|SQ0010:// squiggly v1 game=snake seed=8812|}
            //|........................................|
            //|........................................|
            //|{|SQ0003:..|}......................................|
            //|........................................|
            //|........................{|SQ0001:..|}..............|
            //|......................{|SQ0002:..|}{|SQ0002:..|}..............|
            //|........................................|
            //|........................................|
            //|........................................|
            //|........................................|
            // keys: ddw
            namespace Arcade;
            """);
    }

    [Fact]
    public async Task Missing_keys_line_reports_SQ9001()
    {
        await Verify(Board(Snake8812, 10, 20, keys: null), Expect(Descriptors.CanvasError).WithSpan(1, 1, 1, Snake8812.Length + 1));
    }

    [Fact]
    public async Task Hud_message_describes_the_game()
    {
        await Verify(
            Board(Snake8812, 10, 20, keys: "dd"),
            new DiagnosticResult(Descriptors.Hud).WithSpan(1, 1, 1, Snake8812.Length + 1).WithArguments("Snake, score 0, length 3, moves 2. Type w a s d at the end of the // keys: line to steer"),
            Pixel(Descriptors.Accent, 0, 2),
            Pixel(Descriptors.Secondary, 10, 5),
            Pixel(Descriptors.Secondary, 11, 5),
            Pixel(Descriptors.Primary, 12, 5));
    }

    [Fact]
    public async Task Game_over_reports_on_the_header_and_keeps_the_frame()
    {
        // Ten moves right: the tenth would leave the board, so the snake stops at 19,5.
        await Verify(
            Board(Snake8812, 10, 20, keys: "dddddddddd"),
            Expect(Descriptors.GameOver).WithSpan(1, 1, 1, Snake8812.Length + 1),
            Pixel(Descriptors.Accent, 0, 2),
            Pixel(Descriptors.Secondary, 17, 5),
            Pixel(Descriptors.Secondary, 18, 5),
            Pixel(Descriptors.Primary, 19, 5));
    }

    [Theory]
    [InlineData("// squiggly v1 game=snake")]
    [InlineData("// squiggly v1 game=chess seed=1")]
    [InlineData("// squiggly v2")]
    [InlineData("// squiggly v1 game=snake seed=1 x")]
    [InlineData("// squiggly v1 game=snake seed=1 dir=R head=10,5")]
    public async Task Corrupt_header_reports_exactly_one_SQ9000(string header)
    {
        await Verify(Board(header, 10, 20), Expect(Descriptors.HeaderError).WithSpan(1, 1, 1, header.Length + 1));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(3, 20)]
    [InlineData(10, 19)]
    [InlineData(12, 20)]
    public async Task Damaged_canvas_reports_exactly_one_SQ9001(int rows, int width)
    {
        await Verify(Board(Snake8812, rows, width), Expect(Descriptors.CanvasError).WithSpan(1, 1, 1, Snake8812.Length + 1));
    }

    [Theory]
    [InlineData("snake", "\n", "")]
    [InlineData("tetris", "\r\n", "")]
    [InlineData("mines", "\n", "    ")]
    [InlineData("snake", "\r\n", "\t")]
    public async Task New_games_render_every_cell_at_its_span(string id, string newline, string indent)
    {
        var game = Games.Find(id)!;
        var header = Header.Serialize(game, 31337);
        var source = Board(indent + header, game.Height, game.Width, newline, indent);

        var expected = new List<DiagnosticResult>
        {
            Expect(Descriptors.Hud).WithSpan(1, indent.Length + 1, 1, indent.Length + header.Length + 1),
        };
        expected.AddRange(game.Render(game.NewGame(31337)).Cells.Select(cell => Pixel(ForColor(cell.Color), cell.X, cell.Y, indent.Length)));
        await Verify(source, expected.ToArray());
    }

    [Fact]
    public async Task Minesweeper_marks_every_covered_cell_for_input()
    {
        var game = MinesGame.Instance;
        var header = Header.Serialize(game, 5);
        var expected = new List<DiagnosticResult> { Expect(Descriptors.Hud).WithSpan(1, 1, 1, header.Length + 1) };
        for (var y = 0; y < game.Height; y++)
        {
            for (var x = 0; x < game.Width; x++)
            {
                expected.Add(Pixel(Descriptors.Covered, x, y));
            }
        }

        await Verify(Board(header, game.Height, game.Width), expected.ToArray());
    }

    /// <summary>Matches ID, severity and span; messages are checked only where a test asks for them.</summary>
    private static DiagnosticResult Expect(DiagnosticDescriptor descriptor) => new(descriptor.Id, descriptor.DefaultSeverity);

    /// <summary>A game file; <paramref name="keys"/> is the keys line's content, or null for no keys line.</summary>
    private static string Board(string header, int rows, int width, string newline = "\n", string indent = "", string? keys = "")
    {
        var lines = new List<string> { header };
        lines.AddRange(Enumerable.Repeat(indent + "//|" + new string('.', width * 2) + "|", rows));
        if (keys is not null)
        {
            lines.Add(indent + Canvas.KeysLine("", keys));
        }

        lines.Add("namespace Arcade;");
        return string.Join(newline, lines) + newline;
    }

    /// <summary>The expected diagnostic for board cell (x, y): line 1 is the header, cells start at column 4.</summary>
    private static DiagnosticResult Pixel(DiagnosticDescriptor descriptor, int x, int y, int indent = 0) =>
        Expect(descriptor).WithSpan(y + 2, indent + 4 + x * 2, y + 2, indent + 6 + x * 2);

    private static DiagnosticDescriptor ForColor(PaletteColor color) => color switch
    {
        PaletteColor.Primary => Descriptors.Primary,
        PaletteColor.Secondary => Descriptors.Secondary,
        PaletteColor.Accent => Descriptors.Accent,
        PaletteColor.Covered => Descriptors.Covered,
        _ => Descriptors.Flag,
    };
}
