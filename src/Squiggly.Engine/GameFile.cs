using System;
using System.Collections.Generic;
using System.Globalization;

namespace Squiggly.Engine;

/// <summary>What a <see cref="Mark"/> represents; each kind maps to one diagnostic ID.</summary>
public enum MarkKind
{
    Primary,
    Secondary,
    Accent,
    Covered,
    Flag,
    Hud,
    GameOver,
    HeaderError,
    CanvasError,
}

/// <summary>
/// One diagnostic to report. <see cref="Row"/> is -1 for the header line, otherwise a canvas row index;
/// <see cref="Column"/> and <see cref="Length"/> are character offsets within that line.
/// </summary>
public readonly record struct Mark(MarkKind Kind, int Row, int Column, int Length, string Message);

/// <summary>
/// A game file seen through its header and the lines directly below it. This is the whole analyzer
/// as a pure function: the Roslyn layer only finds the lines and turns <see cref="Marks"/> into diagnostics.
/// </summary>
public sealed class GameFile
{
    private GameFile(string headerLine, HeaderResult header, int canvasRows, bool canvasValid, string keys, GameState? state, IReadOnlyList<Mark> marks)
    {
        HeaderLine = headerLine;
        Header = header;
        CanvasRows = canvasRows;
        CanvasValid = canvasValid;
        Keys = keys;
        State = state;
        Marks = marks;
    }

    public string HeaderLine { get; }

    public HeaderResult Header { get; }

    /// <summary>How many consecutive <c>//|</c> lines follow the header.</summary>
    public int CanvasRows { get; }

    /// <summary>
    /// True when the canvas has the game's exact row count and width, followed by a <c>// keys:</c> line
    /// for games with key controls. Always false when there is no game.
    /// </summary>
    public bool CanvasValid { get; }

    /// <summary>The keys typed on the line directly under the canvas rows; empty when there are none.</summary>
    public string Keys { get; }

    /// <summary>
    /// The current game state: the header's state with every typed key replayed on top of it. Null when
    /// there is no game. Code fixes act on this state and fold the keys back into the header.
    /// </summary>
    public GameState? State { get; }

    public IReadOnlyList<Mark> Marks { get; }

    /// <summary>The header line's leading whitespace, reused for new canvas rows.</summary>
    public string Indent => HeaderLine.Substring(0, HeaderLine.Length - HeaderLine.TrimStart().Length);

    /// <summary>
    /// Reads a game file. <paramref name="following"/> holds the lines after the header; callers pass at
    /// least <see cref="Games.MaxHeight"/> + 2 of them (or all that exist) so an overlong canvas and the
    /// keys line are both seen.
    /// </summary>
    public static GameFile Read(string headerLine, IReadOnlyList<string> following)
    {
        var header = Engine.Header.Parse(headerLine);
        var canvasRows = 0;
        while (canvasRows < following.Count && Canvas.IsRow(following[canvasRows]))
        {
            canvasRows++;
        }

        var hasKeysLine = canvasRows < following.Count && Canvas.IsKeysLine(following[canvasRows]);
        var keys = hasKeysLine ? Canvas.ReadKeys(following[canvasRows]) : "";

        var marks = new List<Mark>();
        var indent = HeaderExtent(headerLine, out var headerLength);
        void OnHeader(MarkKind kind, string message) => marks.Add(new Mark(kind, -1, indent, headerLength, message));

        switch (header.Kind)
        {
            case HeaderKind.Error:
                OnHeader(MarkKind.HeaderError, $"Squiggly header could not be parsed: {header.Error}. Caret here, Ctrl+. to reset the game");
                return new GameFile(headerLine, header, canvasRows, false, keys, null, marks);
            case HeaderKind.Ready:
                OnHeader(MarkKind.Hud, "Squiggly is ready. Caret here, Ctrl+. (Alt+Enter in Rider) to start a game");
                return new GameFile(headerLine, header, canvasRows, false, keys, null, marks);
        }

        var game = header.Game!;
        var valid = canvasRows == game.Height && (hasKeysLine || !game.KeyControls);
        for (var i = 0; valid && i < game.Height; i++)
        {
            valid = Canvas.IsValidRow(following[i], game.Width);
        }

        if (!valid)
        {
            var problem = canvasRows == 0 ? "missing" : "damaged";
            OnHeader(MarkKind.CanvasError, string.Format(
                CultureInfo.InvariantCulture,
                "Squiggly canvas is {0}: {1} needs {2} rows of //| plus {3} characters plus |{4}. Caret here, Ctrl+. to repair it",
                problem,
                game.Name,
                game.Height,
                game.Width * 2,
                game.KeyControls ? ", then a // keys: line" : ""));
            return new GameFile(headerLine, header, canvasRows, false, keys, header.State, marks);
        }

        var state = Replay(game, header.State!, keys);
        OnHeader(state.Status == GameStatus.Over ? MarkKind.GameOver : MarkKind.Hud,
            state.Status == GameStatus.Over ? game.GameOver(state) : game.Hud(state));

        foreach (var cell in game.Render(state).Cells)
        {
            var row = following[cell.Y];
            var rowIndent = row.Length - row.TrimStart().Length;
            marks.Add(new Mark(KindOf(cell.Color), cell.Y, Canvas.CellColumn(rowIndent, cell.X), 2, cell.Label));
        }

        return new GameFile(headerLine, header, canvasRows, true, keys, state, marks);
    }

    /// <summary>
    /// Applies each typed key in order. Keys that mean nothing (including spaces) are skipped, and keys
    /// typed after the game ends or while it is paused change nothing, because Step ignores them.
    /// </summary>
    public static GameState Replay(IGame game, GameState state, string keys)
    {
        if (!game.KeyControls)
        {
            return state;
        }

        foreach (var key in keys)
        {
            if (game.KeyAction(key) is { } action)
            {
                state = game.Step(state, action);
            }
        }

        return state;
    }

    /// <summary>Maps a pixel back to its board cell, for cell-targeted input.</summary>
    public static Point CellAt(string canvasLine, int row, int column)
    {
        var indent = canvasLine.Length - canvasLine.TrimStart().Length;
        return new Point((column - indent - Canvas.RowPrefix.Length) / 2, row);
    }

    private static MarkKind KindOf(PaletteColor color) => color switch
    {
        PaletteColor.Primary => MarkKind.Primary,
        PaletteColor.Secondary => MarkKind.Secondary,
        PaletteColor.Accent => MarkKind.Accent,
        PaletteColor.Covered => MarkKind.Covered,
        PaletteColor.Flag => MarkKind.Flag,
        _ => throw new ArgumentOutOfRangeException(nameof(color)),
    };

    /// <summary>Returns the header's indent and, through <paramref name="length"/>, its length without surrounding whitespace.</summary>
    private static int HeaderExtent(string line, out int length)
    {
        var indent = line.Length - line.TrimStart().Length;
        length = line.Trim().Length;
        return indent;
    }
}
