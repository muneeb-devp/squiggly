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
        System.Console.WriteLine($"Long Snake game: {MoveCount} moves");
        _ordinaryCompilation = Compile(_ordinary);
        _snakeCompilation = Compile(_snake);
    }

    [Benchmark(Description = "Early exit: locate header in a 5,000-line ordinary file")]
    public SquigglyDocument? EarlyExit() => SquigglyDocument.Locate(_ordinary);

    [Benchmark(Description = "Full frame: replay a long Snake game and render it")]
    public int SnakeFrame() => SquigglyDocument.Locate(_snake)!.Read().Marks.Count;

    [Benchmark(Description = "Analyzer via Roslyn: 5,000-line ordinary file")]
    public Task<ImmutableArray<Diagnostic>> AnalyzerOrdinary() => Run(_ordinaryCompilation);

    [Benchmark(Description = "Analyzer via Roslyn: long Snake game")]
    public Task<ImmutableArray<Diagnostic>> AnalyzerSnake() => Run(_snakeCompilation);

    private static Task<ImmutableArray<Diagnostic>> Run(Compilation compilation) =>
        compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new SquigglyAnalyzer())).GetAnalyzerDiagnosticsAsync();

    private static Compilation Compile(SourceText text) =>
        CSharpCompilation.Create("Bench", new[] { CSharpSyntaxTree.ParseText(text) });

    /// <summary>A Snake file with a long move history: a greedy player chases food until it dies or makes 2,000 moves.</summary>
    private static string LongSnakeFile()
    {
        var game = SnakeGame.Instance;
        var state = (SnakeState)game.NewGame(2026);
        var keys = "";
        while (keys.Length < 2000 && state.Status == GameStatus.Playing)
        {
            var food = state.Food ?? state.Head;
            var options = new[]
            {
                food.X < state.Head.X ? SnakeGame.Left : SnakeGame.Right,
                food.Y < state.Head.Y ? SnakeGame.Up : SnakeGame.Down,
                SnakeGame.Up, SnakeGame.Right, SnakeGame.Down, SnakeGame.Left,
            };
            var move = options.FirstOrDefault(a => game.Step(state, a).Status == GameStatus.Playing) ?? SnakeGame.Wait;
            keys = game.AppendKey(keys, state, move);
            state = (SnakeState)game.Step(state, move);
        }

        MoveCount = keys.Length;
        var lines = Canvas.Lines(game, game.NewGame(2026), "").ToArray();
        lines[lines.Length - 1] = Canvas.KeysLine("", keys);
        return Header.Serialize(game, 2026) + "\n" + string.Join("\n", lines) + "\nnamespace Arcade;\n";
    }

    /// <summary>How many moves the long Snake replays; printed in the setup so the README can quote it.</summary>
    public static int MoveCount { get; private set; }
}
