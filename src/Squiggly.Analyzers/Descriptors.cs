using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Squiggly.Engine;

namespace Squiggly.Analyzers;

/// <summary>
/// Every diagnostic Squiggly reports. Severity is fixed per descriptor, so each palette colour has its
/// own ID; players can still override severities in .editorconfig.
/// </summary>
public static class Descriptors
{
    public const string Category = "Squiggly";
    public const string HelpLink = "https://github.com/muneeb-devp/squiggly#readme";

    public static readonly DiagnosticDescriptor Primary = Create("SQ0001", "Pixel (primary colour)", DiagnosticSeverity.Error);
    public static readonly DiagnosticDescriptor Secondary = Create("SQ0002", "Pixel (secondary colour)", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor Accent = Create("SQ0003", "Pixel (accent colour)", DiagnosticSeverity.Info);
    public static readonly DiagnosticDescriptor Hud = Create("SQ0010", "Squiggly HUD", DiagnosticSeverity.Info);
    public static readonly DiagnosticDescriptor GameOver = Create("SQ0011", "Squiggly game over", DiagnosticSeverity.Error);
    public static readonly DiagnosticDescriptor Covered = Create("SQ0020", "Minesweeper covered cell", DiagnosticSeverity.Info);
    public static readonly DiagnosticDescriptor Flag = Create("SQ0021", "Minesweeper flag", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor HeaderError = Create("SQ9000", "Squiggly header could not be parsed", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor CanvasError = Create("SQ9001", "Squiggly canvas missing or damaged", DiagnosticSeverity.Warning);

    public static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
        Primary, Secondary, Accent, Hud, GameOver, Covered, Flag, HeaderError, CanvasError);

    /// <summary>The diagnostics on the header line. Each one doubles as the controller for the lightbulb menu.</summary>
    public static ImmutableArray<string> HeaderIds { get; } = ImmutableArray.Create(Hud.Id, GameOver.Id, HeaderError.Id, CanvasError.Id);

    /// <summary>The cell diagnostics that accept cell-targeted input (Minesweeper).</summary>
    public static ImmutableArray<string> CellInputIds { get; } = ImmutableArray.Create(Covered.Id, Flag.Id);

    public static DiagnosticDescriptor For(MarkKind kind) => kind switch
    {
        MarkKind.Primary => Primary,
        MarkKind.Secondary => Secondary,
        MarkKind.Accent => Accent,
        MarkKind.Covered => Covered,
        MarkKind.Flag => Flag,
        MarkKind.Hud => Hud,
        MarkKind.GameOver => GameOver,
        MarkKind.HeaderError => HeaderError,
        _ => CanvasError,
    };

    // Messages are produced by the engine, so every descriptor uses a single "{0}" placeholder.
    private static DiagnosticDescriptor Create(string id, string title, DiagnosticSeverity severity) =>
        new(id, title, "{0}", Category, severity, isEnabledByDefault: true, helpLinkUri: HelpLink);
}
