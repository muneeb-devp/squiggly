using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Squiggly.CodeFixes;

namespace Squiggly.Analyzers.Tests;

/// <summary>
/// Runs the real analyzer and code fix provider against one document, the way an IDE does:
/// collect diagnostics, ask for code actions on one of them, apply the chosen action.
/// </summary>
internal sealed class FixHarness
{
    private readonly uint[] _seeds;
    private int _nextSeed;

    public FixHarness(params uint[] seeds)
    {
        _seeds = seeds.Length == 0 ? new uint[] { 1 } : seeds;
    }

    public static async Task<ImmutableArray<Diagnostic>> DiagnosticsAsync(Document document)
    {
        var compilation = (await document.Project.GetCompilationAsync())!;
        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new SquigglyAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        return diagnostics.OrderBy(d => d.Location.SourceSpan.Start).ToImmutableArray();
    }

    public static Document Document(string source)
    {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Arcade", LanguageNames.CSharp);
        return project.AddDocument("Game.cs", SourceText.From(source));
    }

    /// <summary>The code actions offered on the first diagnostic with <paramref name="id"/> (at <paramref name="position"/>, if given).</summary>
    public async Task<IReadOnlyList<CodeAction>> ActionsAsync(Document document, string id, int? position = null)
    {
        var diagnostic = (await DiagnosticsAsync(document))
            .First(d => d.Id == id && (position is null || d.Location.SourceSpan.Contains(position.Value)));
        var actions = new List<CodeAction>();
        var provider = new ControllerCodeFixProvider(() => _seeds[_nextSeed++ % _seeds.Length]);
        Assert.Contains(id, provider.FixableDiagnosticIds);
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }

    public static async Task<Document> ApplyAsync(Document document, CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        return solution.GetDocument(document.Id)!;
    }

    /// <summary>Applies the action titled <paramref name="title"/> from the menu on diagnostic <paramref name="id"/>.</summary>
    public async Task<Document> ChooseAsync(Document document, string id, string title, int? position = null)
    {
        var actions = await ActionsAsync(document, id, position);
        var action = actions.SingleOrDefault(a => a.Title == title)
            ?? throw new Xunit.Sdk.XunitException($"No '{title}' in menu: {string.Join(", ", actions.Select(a => a.Title))}");
        return await ApplyAsync(document, action);
    }

    public static async Task<string> TextAsync(Document document) => (await document.GetTextAsync()).ToString();
}
