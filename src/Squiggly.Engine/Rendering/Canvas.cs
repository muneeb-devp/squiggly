using System.Collections.Generic;

namespace Squiggly.Engine;

/// <summary>
/// The text canvas under the header: one <c>//|...|</c> line per board row, two characters per cell.
/// It sits inside line comments, so a game file always compiles as ordinary C#.
/// </summary>
public static class Canvas
{
    public const string RowPrefix = "//|";
    public const char RowSuffix = '|';

    /// <summary>
    /// The fill character for an empty cell. ASCII '.', chosen over U+00B7 in the Phase 0 spike
    /// because squiggles read better under it.
    /// </summary>
    public const char Fill = '.';

    /// <summary>Character offset of cell <paramref name="x"/> within a canvas row with the given indent.</summary>
    public static int CellColumn(int indent, int x) => indent + RowPrefix.Length + x * 2;

    /// <summary>Wraps row contents in <c>//|</c> and <c>|</c>, with the given indent.</summary>
    public static string Row(string indent, string contents) => indent + RowPrefix + contents + RowSuffix;

    public static bool IsRow(string line) => line.TrimStart().StartsWith(RowPrefix, System.StringComparison.Ordinal);

    /// <summary>True when <paramref name="line"/> is a canvas row whose contents are exactly <paramref name="width"/> cells wide.</summary>
    public static bool IsValidRow(string line, int width)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith(RowPrefix, System.StringComparison.Ordinal)
            && trimmed.Length == RowPrefix.Length + width * 2 + 1
            && trimmed[trimmed.Length - 1] == RowSuffix;
    }

    /// <summary>The full canvas lines for a game state.</summary>
    public static IReadOnlyList<string> Lines(IGame game, GameState state, string indent)
    {
        var contents = game.Canvas(state);
        var lines = new string[contents.Count];
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = Row(indent, contents[i]);
        }

        return lines;
    }
}
