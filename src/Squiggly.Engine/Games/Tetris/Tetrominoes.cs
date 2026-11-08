using System.Collections.Generic;

namespace Squiggly.Engine;

/// <summary>
/// The seven tetrominoes and their four rotations. Each piece lives in a square box (4 for I,
/// 2 for O, 3 for the rest) and rotates clockwise within it, as in SRS; the wall kicks are simpler
/// than SRS (see <see cref="TetrisGame"/>).
/// </summary>
public static class Tetrominoes
{
    /// <summary>The piece letters, in the order used to build each seven-bag before shuffling.</summary>
    public const string Letters = "IOTSZJL";

    private static readonly Point[][][] Rotations = Build();

    public static bool IsPiece(char letter) => Letters.IndexOf(letter) >= 0;

    /// <summary>The four cells of <paramref name="piece"/> at rotation <paramref name="rot"/>, relative to the box's top-left corner.</summary>
    public static IReadOnlyList<Point> Cells(char piece, int rot) => Rotations[Letters.IndexOf(piece)][rot & 3];

    /// <summary>Spawn position: centred horizontally, with the piece's top row on the board's top row.</summary>
    public static Point Spawn(char piece) => piece switch
    {
        'I' => new Point(3, -1),
        'O' => new Point(4, 0),
        _ => new Point(3, 0),
    };

    private static Point[][][] Build()
    {
        var shapes = new[]
        {
            (Size: 4, Cells: new[] { new Point(0, 1), new Point(1, 1), new Point(2, 1), new Point(3, 1) }), // I
            (Size: 2, Cells: new[] { new Point(0, 0), new Point(1, 0), new Point(0, 1), new Point(1, 1) }), // O
            (Size: 3, Cells: new[] { new Point(1, 0), new Point(0, 1), new Point(1, 1), new Point(2, 1) }), // T
            (Size: 3, Cells: new[] { new Point(1, 0), new Point(2, 0), new Point(0, 1), new Point(1, 1) }), // S
            (Size: 3, Cells: new[] { new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(2, 1) }), // Z
            (Size: 3, Cells: new[] { new Point(0, 0), new Point(0, 1), new Point(1, 1), new Point(2, 1) }), // J
            (Size: 3, Cells: new[] { new Point(2, 0), new Point(0, 1), new Point(1, 1), new Point(2, 1) }), // L
        };

        var result = new Point[shapes.Length][][];
        for (var p = 0; p < shapes.Length; p++)
        {
            result[p] = new Point[4][];
            var cells = shapes[p].Cells;
            for (var r = 0; r < 4; r++)
            {
                result[p][r] = cells;
                var size = shapes[p].Size;
                var rotated = new Point[cells.Length];
                for (var i = 0; i < cells.Length; i++)
                {
                    rotated[i] = new Point(size - 1 - cells[i].Y, cells[i].X);
                }

                cells = rotated;
            }
        }

        return result;
    }
}
