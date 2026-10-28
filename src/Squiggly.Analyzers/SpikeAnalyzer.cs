using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Squiggly.Analyzers;

/// <summary>
/// Phase 0 spike: lights a fixed test pattern on the canvas so each IDE's rendering can be checked.
/// Every canvas row r draws pattern r % 6, so a file with six '·' rows followed by six '.' rows
/// shows the same pattern under both fill characters.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SpikeAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "Squiggly";
    private const string HelpLink = "https://github.com/muneeb-devp/squiggly";

    public static readonly DiagnosticDescriptor Primary = new(
        "SQ0001", "Pixel (primary)", "primary", Category, DiagnosticSeverity.Error, true, helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor Secondary = new(
        "SQ0002", "Pixel (secondary)", "secondary", Category, DiagnosticSeverity.Warning, true, helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor Accent = new(
        "SQ0003", "Pixel (accent)", "accent", Category, DiagnosticSeverity.Info, true, helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor GhostHidden = new(
        "SQ0004", "Pixel (ghost, hidden + unnecessary)", "ghost", Category, DiagnosticSeverity.Hidden, true,
        helpLinkUri: HelpLink, customTags: WellKnownDiagnosticTags.Unnecessary);

    public static readonly DiagnosticDescriptor GhostInfo = new(
        "SQ0005", "Pixel (ghost, info + unnecessary)", "ghost", Category, DiagnosticSeverity.Info, true,
        helpLinkUri: HelpLink, customTags: WellKnownDiagnosticTags.Unnecessary);

    public static readonly DiagnosticDescriptor Hud = new(
        "SQ0010", "HUD", "Spike, tick {0}", Category, DiagnosticSeverity.Info, true, helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor Controller = new(
        "SQ0100", "Controller", "Squiggly controller", Category, DiagnosticSeverity.Hidden, true, helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor ControllerVisible = new(
        "SQ0101", "Controller (visible fallback)", "Squiggly controls: put the caret here and open the lightbulb", Category, DiagnosticSeverity.Info, true,
        helpLinkUri: HelpLink);

    public static readonly DiagnosticDescriptor Crash = new(
        "SQ9000", "Analyzer error", "Squiggly spike failed: {0}", Category, DiagnosticSeverity.Warning, true, helpLinkUri: HelpLink);

    public const int Columns = 20;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        Primary, Secondary, Accent, GhostHidden, GhostInfo, Hud, Controller, ControllerVisible, Crash);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxTreeAction(Analyze);
    }

    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var tree = context.Tree;
        var text = tree.GetText(context.CancellationToken);
        if (SpikeFile.FindHeader(text) is not { } header)
        {
            return;
        }

        try
        {
            var tick = SpikeFile.ReadTick(header.ToString());
            context.ReportDiagnostic(Diagnostic.Create(Hud, Location.Create(tree, header.Span), tick));
            context.ReportDiagnostic(Diagnostic.Create(Controller, Location.Create(tree, header.Span)));

            var rows = SpikeFile.CanvasRows(text, header);
            for (var r = 0; r < rows.Count; r++)
            {
                DrawRow(context, tree, rows[r], r % 6, tick);
            }

            var after = header.LineNumber + 1 + rows.Count;
            if (after < text.Lines.Count && text.Lines[after].ToString().Trim() == SpikeFile.ControlsLine)
            {
                var controls = text.Lines[after];
                context.ReportDiagnostic(Diagnostic.Create(ControllerVisible, Location.Create(tree, controls.Span)));
            }
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(Crash, Location.Create(tree, header.Span), ex.Message));
        }
    }

    private static void DrawRow(SyntaxTreeAnalysisContext context, SyntaxTree tree, TextLine row, int pattern, int tick)
    {
        void Cell(DiagnosticDescriptor descriptor, int x)
        {
            var start = row.Start + SpikeFile.CanvasPrefix.Length + x * 2;
            if (start + 2 <= row.End)
            {
                context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.Create(tree, new TextSpan(start, 2))));
            }
        }

        switch (pattern)
        {
            case 0: // moving 4-cell bar: continuity of adjacent cells, and redraw after a fix
                var x0 = tick % (Columns - 3);
                for (var x = x0; x < x0 + 4; x++) Cell(Primary, x);
                break;
            case 1: // continuous secondary run
                for (var x = 2; x < 8; x++) Cell(Secondary, x);
                break;
            case 2: // isolated single accent cells
                Cell(Accent, 2);
                Cell(Accent, 5);
                Cell(Accent, 9);
                break;
            case 3: // ghost via Hidden + Unnecessary
                for (var x = 2; x < 8; x++) Cell(GhostHidden, x);
                break;
            case 4: // ghost via Info + Unnecessary
                for (var x = 2; x < 8; x++) Cell(GhostInfo, x);
                break;
            case 5: // alternating primary/secondary: can neighbouring colours be told apart?
                for (var x = 2; x < 10; x++) Cell(x % 2 == 0 ? Primary : Secondary, x);
                break;
        }
    }
}
