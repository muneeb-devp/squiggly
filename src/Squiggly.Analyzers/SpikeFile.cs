using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis.Text;

namespace Squiggly.Analyzers;

/// <summary>
/// Phase 0 spike: locates the header, canvas rows and controls line in a game file.
/// Throwaway code; the real header parser lives in the engine from Phase 2.
/// </summary>
public static class SpikeFile
{
    public const string HeaderPrefix = "// squiggly";
    public const string CanvasPrefix = "//|";
    public const string ControlsLine = "// controls";

    public static TextLine? FindHeader(SourceText text)
    {
        var count = System.Math.Min(5, text.Lines.Count);
        for (var i = 0; i < count; i++)
        {
            var line = text.Lines[i];
            if (line.ToString().Trim().StartsWith(HeaderPrefix, System.StringComparison.Ordinal))
            {
                return line;
            }
        }

        return null;
    }

    public static List<TextLine> CanvasRows(SourceText text, TextLine header)
    {
        var rows = new List<TextLine>();
        for (var i = header.LineNumber + 1; i < text.Lines.Count; i++)
        {
            var line = text.Lines[i];
            if (!line.ToString().StartsWith(CanvasPrefix, System.StringComparison.Ordinal))
            {
                break;
            }

            rows.Add(line);
        }

        return rows;
    }

    public static int ReadTick(string header)
    {
        foreach (var token in header.Split(' '))
        {
            if (token.StartsWith("tick=", System.StringComparison.Ordinal)
                && int.TryParse(token.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out var tick))
            {
                return tick;
            }
        }

        return 0;
    }

    public static string WithTick(string header, int tick)
    {
        var tokens = new List<string>(header.Split(' '));
        var value = "tick=" + tick.ToString(CultureInfo.InvariantCulture);
        var index = tokens.FindIndex(t => t.StartsWith("tick=", System.StringComparison.Ordinal));
        if (index >= 0)
        {
            tokens[index] = value;
        }
        else
        {
            tokens.Add(value);
        }

        return string.Join(" ", tokens);
    }
}
