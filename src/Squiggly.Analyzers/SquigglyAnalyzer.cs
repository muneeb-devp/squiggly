using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Squiggly.Analyzers;

/// <summary>
/// Renders a Squiggly game file as diagnostics: one per lit cell, plus one on the header line (HUD,
/// game over or an error) that doubles as the controller. Files without a header get nothing.
/// No semantic model, no state, no I/O: the diagnostics are a pure function of the document text.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SquigglyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Descriptors.All;

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxTreeAction(Analyze);
    }

    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var tree = context.Tree;
        var document = SquigglyDocument.Locate(tree.GetText(context.CancellationToken));
        if (document is null)
        {
            return;
        }

        try
        {
            foreach (var mark in document.Read().Marks)
            {
                var location = Location.Create(tree, document.SpanOf(mark));
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.For(mark.Kind), location, mark.Message));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A thrown exception makes IDEs disable the analyzer, so report it on the header instead.
            var location = Location.Create(tree, document.HeaderLine.Span);
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.HeaderError, location, "Squiggly internal error: " + ex.Message));
        }
    }
}
