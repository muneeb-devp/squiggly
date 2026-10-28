using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using Squiggly.Analyzers;

namespace Squiggly.CodeFixes;

/// <summary>
/// Phase 0 spike: advances tick in the header, replacing only the header line's text span.
/// Registered for the hidden header controller, the visible controls-line fallback, and the
/// Info-severity HUD (to test whether the HUD alone can anchor the menu in Rider).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SpikeCodeFixProvider)), Shared]
public sealed class SpikeCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(SpikeAnalyzer.Controller.Id, SpikeAnalyzer.ControllerVisible.Id, SpikeAnalyzer.Hud.Id);

    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        if (SpikeFile.FindHeader(text) is not { } header)
        {
            return;
        }

        var current = header.ToString();
        var tick = SpikeFile.ReadTick(current);

        foreach (var step in new[] { 1, 5 })
        {
            var updated = SpikeFile.WithTick(current, tick + step);
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Squiggly: Tick +{step}",
                    _ => Task.FromResult(context.Document.WithText(text.WithChanges(new TextChange(header.Span, updated)))),
                    equivalenceKey: $"squiggly.spike.tick{step}"),
                context.Diagnostics);
        }
    }
}
