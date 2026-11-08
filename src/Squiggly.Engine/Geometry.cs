using System;
using System.Globalization;

namespace Squiggly.Engine;

/// <summary>A cell coordinate on a game board. X grows to the right, Y grows downwards.</summary>
public readonly record struct Point(int X, int Y)
{
    /// <summary>Returns this point moved one cell in <paramref name="direction"/>.</summary>
    public Point Step(Direction direction) => direction switch
    {
        Direction.Up => new Point(X, Y - 1),
        Direction.Down => new Point(X, Y + 1),
        Direction.Left => new Point(X - 1, Y),
        _ => new Point(X + 1, Y),
    };

    /// <summary>True when the point lies on a board of the given size.</summary>
    public bool IsInside(int width, int height) => X >= 0 && Y >= 0 && X < width && Y < height;

    /// <summary>Header form: <c>x,y</c>.</summary>
    public override string ToString() =>
        X.ToString(CultureInfo.InvariantCulture) + "," + Y.ToString(CultureInfo.InvariantCulture);
}

/// <summary>One of the four board directions.</summary>
public enum Direction
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>Conversions between <see cref="Direction"/> values and their one-letter header form.</summary>
public static class Directions
{
    public static char ToLetter(Direction direction) => direction switch
    {
        Direction.Up => 'U',
        Direction.Down => 'D',
        Direction.Left => 'L',
        _ => 'R',
    };

    public static bool TryParse(char letter, out Direction direction)
    {
        switch (letter)
        {
            case 'U': direction = Direction.Up; return true;
            case 'D': direction = Direction.Down; return true;
            case 'L': direction = Direction.Left; return true;
            case 'R': direction = Direction.Right; return true;
            default: direction = default; return false;
        }
    }

    public static Direction Opposite(Direction direction) => direction switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        _ => Direction.Left,
    };

    internal static Direction FromLetter(char letter) =>
        TryParse(letter, out var direction) ? direction : throw new ArgumentOutOfRangeException(nameof(letter));
}
