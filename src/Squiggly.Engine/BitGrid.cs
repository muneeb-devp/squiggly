using System;
using System.Globalization;
using System.Text;

namespace Squiggly.Engine;

/// <summary>
/// An immutable grid of booleans with value equality, used for Tetris boards and Minesweeper layers.
/// Header form: one lowercase hex bitmask per row, top row first, joined by '.'; bit x is column x.
/// </summary>
public sealed class BitGrid : IEquatable<BitGrid>
{
    private readonly int[] _rows;

    private BitGrid(int width, int[] rows)
    {
        Width = width;
        _rows = rows;
    }

    public int Width { get; }

    public int Height => _rows.Length;

    public static BitGrid Empty(int width, int height) => new(width, new int[height]);

    public bool this[Point point] => point.IsInside(Width, Height) && (_rows[point.Y] & (1 << point.X)) != 0;

    public bool this[int x, int y] => this[new Point(x, y)];

    public BitGrid With(Point point, bool value)
    {
        var rows = (int[])_rows.Clone();
        rows[point.Y] = value ? rows[point.Y] | (1 << point.X) : rows[point.Y] & ~(1 << point.X);
        return new BitGrid(Width, rows);
    }

    public int Count()
    {
        var count = 0;
        foreach (var row in _rows)
        {
            for (var bits = row; bits != 0; bits &= bits - 1)
            {
                count++;
            }
        }

        return count;
    }

    public bool IsRowFull(int y) => _rows[y] == (1 << Width) - 1;

    /// <summary>Removes every full row, shifting the rows above it down. Returns how many were removed.</summary>
    public BitGrid ClearFullRows(out int cleared)
    {
        var kept = new int[Height];
        var target = Height - 1;
        for (var y = Height - 1; y >= 0; y--)
        {
            if (!IsRowFull(y))
            {
                kept[target--] = _rows[y];
            }
        }

        cleared = target + 1;
        return new BitGrid(Width, kept);
    }

    public string Serialize()
    {
        var builder = new StringBuilder();
        for (var y = 0; y < Height; y++)
        {
            if (y > 0)
            {
                builder.Append('.');
            }

            builder.Append(_rows[y].ToString("x", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static bool TryParse(string text, int width, int height, out BitGrid grid)
    {
        grid = Empty(width, height);
        var parts = text.Split('.');
        if (parts.Length != height)
        {
            return false;
        }

        var rows = new int[height];
        for (var y = 0; y < height; y++)
        {
            var part = parts[y];
            if (part.Length == 0 || part.Length > 4 || (part.Length > 1 && part[0] == '0')
                || !IsLowerHex(part)
                || !int.TryParse(part, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var row)
                || row >= 1 << width)
            {
                return false;
            }

            rows[y] = row;
        }

        grid = new BitGrid(width, rows);
        return true;
    }

    private static bool IsLowerHex(string part)
    {
        foreach (var c in part)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
            {
                return false;
            }
        }

        return true;
    }

    public bool Equals(BitGrid? other)
    {
        if (other is null || other.Width != Width || other.Height != Height)
        {
            return false;
        }

        for (var y = 0; y < Height; y++)
        {
            if (_rows[y] != other._rows[y])
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as BitGrid);

    public override int GetHashCode()
    {
        var hash = Width;
        foreach (var row in _rows)
        {
            hash = unchecked(hash * 31 + row);
        }

        return hash;
    }

    public override string ToString() => Serialize();
}
