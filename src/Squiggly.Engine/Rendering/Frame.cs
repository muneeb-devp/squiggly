using System.Collections.Generic;

namespace Squiggly.Engine;

/// <summary>
/// How a lit cell is drawn. Each colour maps to one diagnostic ID, and the IDE's theme decides what
/// that diagnostic looks like (see the palette table in the README).
/// </summary>
public enum PaletteColor
{
    /// <summary>SQ0001, error: snake head, active piece, mines on loss.</summary>
    Primary,

    /// <summary>SQ0002, warning: snake body, locked blocks.</summary>
    Secondary,

    /// <summary>SQ0003, info: food, Tetris ghost piece.</summary>
    Accent,

    /// <summary>SQ0020, info: a covered Minesweeper cell that accepts input.</summary>
    Covered,

    /// <summary>SQ0021, warning: a flagged Minesweeper cell that accepts input.</summary>
    Flag,
}

/// <summary>One lit cell of a frame. <see cref="Label"/> is the short hover text, such as "food".</summary>
public readonly record struct Cell(int X, int Y, PaletteColor Color, string Label);

/// <summary>A rendered frame: the lit cells of a board, in row-major order, at most one per position.</summary>
public sealed class Frame
{
    public Frame(int width, int height, IReadOnlyList<Cell> cells)
    {
        Width = width;
        Height = height;
        Cells = cells;
    }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<Cell> Cells { get; }
}

/// <summary>Collects lit cells; a later <see cref="Set"/> on the same position replaces the earlier one.</summary>
public sealed class FrameBuilder
{
    private readonly int _width;
    private readonly int _height;
    private readonly Cell?[] _cells;

    public FrameBuilder(int width, int height)
    {
        _width = width;
        _height = height;
        _cells = new Cell?[width * height];
    }

    public void Set(Point point, PaletteColor color, string label)
    {
        if (point.IsInside(_width, _height))
        {
            _cells[point.Y * _width + point.X] = new Cell(point.X, point.Y, color, label);
        }
    }

    public Frame Build()
    {
        var cells = new List<Cell>();
        foreach (var cell in _cells)
        {
            if (cell is { } lit)
            {
                cells.Add(lit);
            }
        }

        return new Frame(_width, _height, cells);
    }
}
