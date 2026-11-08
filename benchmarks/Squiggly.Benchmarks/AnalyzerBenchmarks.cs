using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Squiggly.Analyzers;
using Squiggly.Engine;

BenchmarkSwitcher.FromAssembly(typeof(AnalyzerBenchmarks).Assembly).Run(args);

/// <summary>
/// The two paths that matter: the early exit that runs on every ordinary file in every project,
/// and a full Snake frame. Numbers go in the README as a trust signal.
/// </summary>
[MemoryDiagnoser]
public class AnalyzerBenchmarks
{
    private SourceText _ordinary = null!;
    private SourceText _snake = null!;
    private Compilation _ordinaryCompilation = null!;
    private Compilation _snakeCompilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        var builder = new StringBuilder("namespace Big;\n\npublic static class Generated\n{\n");
        for (var i = 0; i < 5000; i++)
        {
            builder.Append("    public static int Method").Append(i).Append("(int x) => x * ").Append(i).Append(";\n");
        }

        _ordinary = SourceText.From(builder.Append("}\n").ToString());
        _snake = SourceText.From(LongSnakeFile());
        _ordinaryCompilation = Compile(_ordinary);
        _snakeCompilation = Compile(_snake);
    }

    [Benchmark(Description = "Early exit: locate header in a 5,000-line ordinary file")]
    public SquigglyDocument? EarlyExit() => SquigglyDocument.Locate(_ordinary);

    [Benchmark(Description = "Full frame: read and render a long Snake")]
    public int SnakeFrame() => SquigglyDocument.Locate(_snake)!.Read().Marks.Count;

    [Benchmark(Description = "Analyzer via Roslyn: 5,000-line ordinary file")]
    public Task<ImmutableArray<Diagnostic>> AnalyzerOrdinary() => Run(_ordinaryCompilation);

    [Benchmark(Description = "Analyzer via Roslyn: long Snake")]
    public Task<ImmutableArray<Diagnostic>> AnalyzerSnake() => Run(_snakeCompilation);

    private static Task<ImmutableArray<Diagnostic>> Run(Compilation compilation) =>
        compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new SquigglyAnalyzer())).GetAnalyzerDiagnosticsAsync();

    private static Compilation Compile(SourceText text) =>
        CSharpCompilation.Create("Bench", new[] { CSharpSyntaxTree.ParseText(text) });

    /// <summary>A snake winding across the board, about 100 cells long.</summary>
    private static string LongSnakeFile()
    {
        var tail = string.Concat(Enumerable.Repeat("LLLLLLLLLLLLLLLLLLLD" + "RRRRRRRRRRRRRRRRRRRD", 2)) + "LLLLLLLLLLLLLLLLLLL";
        var header = $"// squiggly v1 game=snake seed=1 tick=500 status=playing score=96 dir=R head=19,0 tail={tail} food=0,9";
        var game = SnakeGame.Instance;
        var state = Header.Parse(header).State!;
        return header + "\n" + string.Join("\n", Canvas.Lines(game, state, "")) + "\nnamespace Arcade;\n";
    }
}
