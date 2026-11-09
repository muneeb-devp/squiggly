using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Squiggly.Analyzers;
using Squiggly.Engine;

namespace Squiggly.CodeFixes;

/// <summary>
/// The game controls. On the header line it offers the menu for the current status (new game, moves,
/// restart, play again, switch game, repair, reset); on a Minesweeper cell it offers that cell's actions. Each entry is
/// computed up front, with the new-game seed drawn once per request, so the IDE's preview and the
/// applied change are always identical.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ControllerCodeFixProvider)), Shared]
public sealed class ControllerCodeFixProvider : CodeFixProvider
{
    private readonly Func<uint> _newSeed;

    public ControllerCodeFixProvider()
        : this(RandomSeed)
    {
    }

    /// <summary>Lets tests supply fixed seeds.</summary>
    internal ControllerCodeFixProvider(Func<uint> newSeed)
    {
        _newSeed = newSeed;
    }

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = Descriptors.HeaderIds.AddRange(Descriptors.CellInputIds);

    /// <summary>Fix All on a game makes no sense.</summary>
    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var text = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        var document = SquigglyDocument.Locate(text);
        if (document is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var file = document.Read();
        IReadOnlyList<Command> commands;
        if (Descriptors.CellInputIds.Contains(diagnostic.Id))
        {
            commands = document.CellAt(diagnostic.Location.SourceSpan.Start) is { } cell
                ? Controller.CellMenu(file, cell)
                : Array.Empty<Command>();
        }
        else
        {
            commands = Controller.Menu(file);
        }

        if (commands.Count == 0)
        {
            return;
        }

        var seed = _newSeed();
        foreach (var command in commands)
        {
            var changed = text.WithChanges(document.Changes(Controller.Apply(file, command, seed)));
            var result = context.Document.WithText(changed);
            context.RegisterCodeFix(
                CodeAction.Create(command.Title, _ => Task.FromResult(result), command.Key),
                diagnostic);
        }
    }

    private static uint RandomSeed() => BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0);
}
