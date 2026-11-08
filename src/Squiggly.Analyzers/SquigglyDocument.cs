using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using Squiggly.Engine;

namespace Squiggly.Analyzers;

/// <summary>
/// A source file that contains a Squiggly header: the bridge between Roslyn's <see cref="SourceText"/>
/// and the engine's line-based <see cref="GameFile"/>. Shared by the analyzer and the code fixes.
/// </summary>
public sealed class SquigglyDocument
{
    private SquigglyDocument(SourceText text, int headerLine)
    {
        Text = text;
        HeaderLineNumber = headerLine;
    }

    public SourceText Text { get; }

    public int HeaderLineNumber { get; }

    public TextLine HeaderLine => Text.Lines[HeaderLineNumber];

    /// <summary>
    /// Finds the header in the first <see cref="Header.SearchLines"/> lines, or returns null. This runs on
    /// every file in every project, so it scans characters directly and allocates nothing on the miss path.
    /// </summary>
    public static SquigglyDocument? Locate(SourceText text)
    {
        var length = text.Length;
        var position = 0;
        for (var line = 0; line < Header.SearchLines && position < length; line++)
        {
            var start = position;
            while (start < length && (text[start] == ' ' || text[start] == '\t'))
            {
                start++;
            }

            if (StartsWithPrefix(text, start))
            {
                return new SquigglyDocument(text, text.Lines.GetLineFromPosition(start).LineNumber);
            }

            while (position < length && text[position] != '\n' && text[position] != '\r')
            {
                position++;
            }

            if (position < length && text[position] == '\r')
            {
                position++;
            }

            if (position < length && text[position] == '\n')
            {
                position++;
            }
        }

        return null;
    }

    /// <summary>Reads the header and enough following lines to validate the largest canvas and its keys line.</summary>
    public GameFile Read()
    {
        var lines = Text.Lines;
        var following = new List<string>();
        var last = System.Math.Min(lines.Count - 1, HeaderLineNumber + Games.MaxHeight + 2);
        for (var i = HeaderLineNumber + 1; i <= last; i++)
        {
            following.Add(lines[i].ToString());
        }

        return GameFile.Read(HeaderLine.ToString(), following);
    }

    /// <summary>The text span a <see cref="Mark"/> covers.</summary>
    public TextSpan SpanOf(Mark mark)
    {
        var line = Text.Lines[HeaderLineNumber + 1 + mark.Row];
        return new TextSpan(line.Start + mark.Column, mark.Length);
    }

    /// <summary>The board cell under <paramref name="position"/>, for cell-targeted input; null when it is not on the canvas.</summary>
    public Point? CellAt(int position)
    {
        var line = Text.Lines.GetLineFromPosition(position);
        var row = line.LineNumber - HeaderLineNumber - 1;
        if (row < 0)
        {
            return null;
        }

        return GameFile.CellAt(line.ToString(), row, position - line.Start);
    }

    /// <summary>
    /// Turns an engine edit into text changes: the header text (keeping its indent), plus only the lines
    /// that differ in the block under it (canvas rows, then the keys line). A block of a different size,
    /// such as a canvas for another game, replaces the existing block as a whole. Line endings follow the
    /// file's own.
    /// </summary>
    public IReadOnlyList<TextChange> Changes(FileEdit edit)
    {
        var changes = new List<TextChange>();
        var header = HeaderLine;
        var headerText = header.ToString();
        var indent = headerText.Substring(0, headerText.Length - headerText.TrimStart().Length);
        if (headerText.Trim() != edit.Header)
        {
            changes.Add(new TextChange(TextSpan.FromBounds(header.Start + indent.Length, header.End), edit.Header));
        }

        if (edit.CanvasRows is null && edit.Keys is null)
        {
            return changes;
        }

        // The existing block: consecutive canvas rows, then the keys line if there is one.
        var lines = Text.Lines;
        var first = HeaderLineNumber + 1;
        var rows = 0;
        while (first + rows < lines.Count && Canvas.IsRow(lines[first + rows].ToString()))
        {
            rows++;
        }

        var hasKeys = first + rows < lines.Count && Canvas.IsKeysLine(lines[first + rows].ToString());
        var existing = rows + (hasKeys ? 1 : 0);

        var wanted = new List<string>();
        if (edit.CanvasRows is { } canvas)
        {
            foreach (var row in canvas)
            {
                wanted.Add(Canvas.Row(indent, row));
            }
        }
        else
        {
            for (var i = 0; i < rows; i++)
            {
                wanted.Add(lines[first + i].ToString());
            }
        }

        if (edit.Keys is { } keys)
        {
            wanted.Add(Canvas.KeysLine(indent, keys));
        }
        else if (hasKeys)
        {
            wanted.Add(lines[first + rows].ToString());
        }

        if (existing == wanted.Count)
        {
            for (var i = 0; i < existing; i++)
            {
                var line = lines[first + i];
                if (line.ToString() != wanted[i])
                {
                    changes.Add(new TextChange(line.Span, wanted[i]));
                }
            }

            return changes;
        }

        var newline = NewLine();
        var block = string.Join(newline, wanted);
        if (existing == 0)
        {
            changes.Add(header.EndIncludingLineBreak > header.End
                ? new TextChange(new TextSpan(header.EndIncludingLineBreak, 0), block + newline)
                : new TextChange(new TextSpan(header.End, 0), newline + block));
        }
        else
        {
            changes.Add(new TextChange(TextSpan.FromBounds(lines[first].Start, lines[first + existing - 1].End), block));
        }

        return changes;
    }

    /// <summary>The header's own line break, else the first one in the file, else "\n".</summary>
    private string NewLine()
    {
        foreach (var line in Text.Lines)
        {
            if (line.EndIncludingLineBreak > line.End && line.LineNumber >= HeaderLineNumber)
            {
                return Text.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));
            }
        }

        foreach (var line in Text.Lines)
        {
            if (line.EndIncludingLineBreak > line.End)
            {
                return Text.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));
            }
        }

        return "\n";
    }

    private static bool StartsWithPrefix(SourceText text, int start)
    {
        var prefix = Header.Prefix;
        if (start + prefix.Length > text.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (text[start + i] != prefix[i])
            {
                return false;
            }
        }

        var next = start + prefix.Length;
        return next == text.Length || char.IsWhiteSpace(text[next]);
    }
}
