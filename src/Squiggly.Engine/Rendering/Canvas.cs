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

    /// <summary>The line under the canvas that holds typed keys, for games with <see cref="IGame.KeyControls"/>.</summary>
    public const string KeysPrefix = "// keys:";

    public static bool IsKeysLine(string line) => line.TrimStart().StartsWith(KeysPrefix, System.StringComparison.Ordinal);

    /// <summary>The typed keys on a keys line: everything after the prefix, without surrounding whitespace.</summary>
    public static string ReadKeys(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Substring(System.Math.Min(KeysPrefix.Length, trimmed.Length)).Trim();
    }

    /// <summary>A keys line with the given indent; empty keys give a bare prefix with no trailing space.</summary>
    public static string KeysLine(string indent, string keys) => indent + KeysPrefix + (keys.Length == 0 ? "" : " " + keys);

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

    /// <summary>The full block under the header for a game state: canvas rows, then an empty keys line if the game uses one.</summary>
    public static IReadOnlyList<string> Lines(IGame game, GameState state, string indent)
    {
        var lines = new List<string>();
        foreach (var contents in game.Canvas(state))
        {
            lines.Add(Row(indent, contents));
        }

        if (game.KeyControls)
        {
            lines.Add(KeysLine(indent, ""));
        }

        return lines;
    }
}
