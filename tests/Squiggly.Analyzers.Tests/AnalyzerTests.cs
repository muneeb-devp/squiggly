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

    [Fact]
    public async Task Snake_frame_lights_exactly_the_expected_cells()
    {
        await Verify("""
            {|SQ0010:// squiggly v1 game=snake seed=8812 tick=37 status=playing score=4 dir=R head=10,5 tail=LL food=14,7|}
            //|........................................|
            //|........................................|
            //|........................................|
            //|........................................|
            //|........................................|
            //|................{|SQ0002:..|}{|SQ0002:..|}{|SQ0001:..|}..................|
            //|........................................|
            //|............................{|SQ0003:..|}..........|
            //|........................................|
            //|........................................|
            namespace Arcade;
            """);
    }

    [Fact]
    public async Task Hud_message_describes_the_game()
    {
        var header = "// squiggly v1 game=snake seed=8812 tick=37 status=playing score=4 dir=R head=10,5 tail=LL food=14,7";
        await Verify(
            Board(header, 10, 20),
            new DiagnosticResult(Descriptors.Hud).WithSpan(1, 1, 1, header.Length + 1).WithArguments("Snake, score 4, length 3, tick 37"),
            Pixel(Descriptors.Secondary, 8, 5),
            Pixel(Descriptors.Secondary, 9, 5),
            Pixel(Descriptors.Primary, 10, 5),
            Pixel(Descriptors.Accent, 14, 7));
    }

    [Fact]
    public async Task Game_over_reports_on_the_header_and_keeps_the_frame()
    {
        var header = "// squiggly v1 game=snake seed=1 tick=9 status=over score=2 dir=R head=19,5 tail=LL food=1,1";
        await Verify(
            Board(header, 10, 20),
            Expect(Descriptors.GameOver).WithSpan(1, 1, 1, header.Length + 1),
            Pixel(Descriptors.Accent, 1, 1),
            Pixel(Descriptors.Secondary, 17, 5),
            Pixel(Descriptors.Secondary, 18, 5),
            Pixel(Descriptors.Primary, 19, 5));
    }

    [Theory]
    [InlineData("// squiggly v1 game=snake seed=1 tick=0 status=playing score=0 dir=R head=10,5 tail=LL")]
    [InlineData("// squiggly v1 game=chess")]
    [InlineData("// squiggly v2")]
    [InlineData("// squiggly v1 game=snake seed=1 tick=0 status=playing score=0 dir=R head=10,5 tail=LL food=1,1 x")]
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
        var header = "// squiggly v1 game=snake seed=1 tick=0 status=playing score=0 dir=R head=10,5 tail=LL food=1,1";
        await Verify(Board(header, rows, width), Expect(Descriptors.CanvasError).WithSpan(1, 1, 1, header.Length + 1));
    }

    [Theory]
    [InlineData("snake", "\n", "")]
    [InlineData("tetris", "\r\n", "")]
    [InlineData("mines", "\n", "    ")]
    [InlineData("snake", "\r\n", "\t")]
    public async Task New_games_render_every_cell_at_its_span(string id, string newline, string indent)
    {
        var game = Games.Find(id)!;
        var state = game.NewGame(31337);
        var header = Header.Serialize(game, state);
        var source = Board(indent + header, game.Height, game.Width, newline, indent);

        var expected = new List<DiagnosticResult>
        {
            Expect(Descriptors.Hud).WithSpan(1, indent.Length + 1, 1, indent.Length + header.Length + 1),
        };
        expected.AddRange(game.Render(state).Cells.Select(cell => Pixel(ForColor(cell.Color), cell.X, cell.Y, indent.Length)));
        await Verify(source, expected.ToArray());
    }

    [Fact]
    public async Task Minesweeper_marks_every_covered_cell_for_input()
    {
        var game = MinesGame.Instance;
        var state = game.NewGame(5);
        var header = Header.Serialize(game, state);
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

    private static string Board(string header, int rows, int width, string newline = "\n", string indent = "")
    {
        var lines = new List<string> { header };
        lines.AddRange(Enumerable.Repeat(indent + "//|" + new string('.', width * 2) + "|", rows));
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
