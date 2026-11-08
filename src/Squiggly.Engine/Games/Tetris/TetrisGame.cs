using System.Collections.Generic;
using System.Globalization;

namespace Squiggly.Engine;

/// <summary>
/// Tetris state. <see cref="Pos"/> is the top-left of the piece's rotation box and may be negative
/// (the I piece spawns at y = -1). <see cref="Drawn"/> counts pieces taken from the randomizer,
/// including the current and next piece, so the seven-bag sequence can be continued from the seed.
/// </summary>
public sealed record TetrisState : GameState
{
    public char Piece { get; init; }

    public int Rot { get; init; }

    public Point Pos { get; init; }

    public char Next { get; init; }

    public int Drawn { get; init; }

    public int Lines { get; init; }

    public BitGrid Board { get; init; } = BitGrid.Empty(TetrisGame.BoardWidth, TetrisGame.BoardHeight);
}

/// <summary>
/// Turn-based Tetris on a 10 x 20 board. Every action except Hard drop is followed by one row of
/// gravity; a piece that cannot fall locks at once. Soft drop therefore moves two rows.
/// Rotation is clockwise with a simple kick table (try x offsets 0, -1, +1, -2), not full SRS.
/// Pieces come from a seven-bag randomizer: bag k is a shuffle of all seven pieces seeded from the
/// game seed and k, so the piece order is reproducible.
/// </summary>
public sealed class TetrisGame : IGame
{
    public const int BoardWidth = 10;
    public const int BoardHeight = 20;

    public static TetrisGame Instance { get; } = new();

    public static readonly GameAction Left = new("left", "Move left");
    public static readonly GameAction Right = new("right", "Move right");
    public static readonly GameAction Rotate = new("rotate", "Rotate");
    public static readonly GameAction SoftDrop = new("softdrop", "Soft drop");
    public static readonly GameAction HardDrop = new("harddrop", "Hard drop");

    private static readonly GameAction[] ActionList = { Left, Right, Rotate, SoftDrop, HardDrop };
    private static readonly int[] Kicks = { 0, -1, 1, -2 };
    private static readonly int[] LineScores = { 0, 100, 300, 500, 800 };

    private TetrisGame()
    {
    }

    public string Id => "tetris";

    public string Name => "Tetris";

    public int Width => BoardWidth;

    public int Height => BoardHeight;

    public bool DynamicCanvas => false;

    public bool KeyControls => true;

    public IReadOnlyList<GameAction> Actions => ActionList;

    /// <summary>The <paramref name="index"/>th piece (0-based) of the seven-bag sequence for <paramref name="seed"/>.</summary>
    public static char PieceAt(uint seed, int index)
    {
        var bag = Tetrominoes.Letters.ToCharArray();
        var random = Prng.For(seed, index / 7, stream: 2);
        for (var i = bag.Length - 1; i > 0; i--)
        {
            var j = random.NextBelow(i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }

        return bag[index % 7];
    }

    public GameState NewGame(uint seed)
    {
        var first = PieceAt(seed, 0);
        return new TetrisState
        {
            Seed = seed,
            Status = GameStatus.Playing,
            Piece = first,
            Pos = Tetrominoes.Spawn(first),
            Next = PieceAt(seed, 1),
            Drawn = 2,
        };
    }

    public GameState Step(GameState state, GameAction action)
    {
        var tetris = (TetrisState)state;
        if (tetris.Status != GameStatus.Playing)
        {
            return tetris;
        }

        var next = tetris with { Tick = tetris.Tick + 1 };
        switch (action.Id)
        {
            case "left":
                return Gravity(TryMove(next, -1));
            case "right":
                return Gravity(TryMove(next, 1));
            case "rotate":
                return Gravity(TryRotate(next));
            case "softdrop":
                return Fits(next, next.Rot, Below(next.Pos)) ? Gravity(next with { Pos = Below(next.Pos) }) : Lock(next);
            case "harddrop":
                return Lock(next with { Pos = DropPosition(next) });
            default:
                return next;
        }
    }

    public IReadOnlyList<GameAction> CellActions(GameState state, Point cell) => System.Array.Empty<GameAction>();

    /// <summary>a and d move, w rotates, s soft drops, x hard drops.</summary>
    public GameAction? KeyAction(char key) => char.ToLowerInvariant(key) switch
    {
        'a' => Left,
        'd' => Right,
        'w' => Rotate,
        's' => SoftDrop,
        'x' => HardDrop,
        _ => null,
    };

    public Frame Render(GameState state)
    {
        var tetris = (TetrisState)state;
        var frame = new FrameBuilder(Width, Height);
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (tetris.Board[x, y])
                {
                    frame.Set(new Point(x, y), PaletteColor.Secondary, "block");
                }
            }
        }

        if (tetris.Status != GameStatus.Over)
        {
            var ghost = DropPosition(tetris);
            if (ghost != tetris.Pos)
            {
                foreach (var cell in Tetrominoes.Cells(tetris.Piece, tetris.Rot))
                {
                    frame.Set(Offset(ghost, cell), PaletteColor.Accent, "ghost");
                }
            }
        }

        foreach (var cell in Tetrominoes.Cells(tetris.Piece, tetris.Rot))
        {
            frame.Set(Offset(tetris.Pos, cell), PaletteColor.Primary, "piece");
        }

        return frame.Build();
    }

    public string Hud(GameState state)
    {
        var tetris = (TetrisState)state;
        var paused = tetris.Status == GameStatus.Paused ? " (paused)" : "";
        return string.Format(
            CultureInfo.InvariantCulture,
            "Tetris{0}, score {1}, lines {2}, next {3}, tick {4}. On the // keys: line type a d to move, w to rotate, s to drop, x to hard drop",
            paused,
            tetris.Score,
            tetris.Lines,
            tetris.Next,
            tetris.Tick);
    }

    public string GameOver(GameState state)
    {
        var tetris = (TetrisState)state;
        return string.Format(
            CultureInfo.InvariantCulture,
            "GAME OVER, score {0}, lines {1}. Caret here, Ctrl+. to restart",
            tetris.Score,
            tetris.Lines);
    }

    public IReadOnlyList<string> Canvas(GameState state) => Games.BlankCanvas(Width, Height);

    public GameState Read(HeaderReader reader)
    {
        var piece = ReadPiece(reader, "piece");
        var rot = reader.Int("rot", 0, 3);
        var pos = reader.Point("pos", -3, Width, -3, Height);
        var next = ReadPiece(reader, "next");
        var drawn = reader.Int("drawn", 2, int.MaxValue);
        var lines = reader.Int("lines", 0, int.MaxValue);
        var board = reader.Grid("board", Width, Height);
        return new TetrisState
        {
            Seed = reader.Seed,
            Tick = reader.Tick,
            Status = reader.Status,
            Score = reader.Score,
            Piece = piece,
            Rot = rot,
            Pos = pos,
            Next = next,
            Drawn = drawn,
            Lines = lines,
            Board = board,
        };
    }

    public void Write(GameState state, HeaderWriter writer)
    {
        var tetris = (TetrisState)state;
        writer.Add("piece", tetris.Piece.ToString());
        writer.Add("rot", tetris.Rot);
        writer.Add("pos", tetris.Pos);
        writer.Add("next", tetris.Next.ToString());
        writer.Add("drawn", tetris.Drawn);
        writer.Add("lines", tetris.Lines);
        writer.Add("board", tetris.Board.Serialize());
    }

    /// <summary>Where the current piece would land if dropped straight down.</summary>
    public Point DropPosition(TetrisState state)
    {
        var pos = state.Pos;
        if (!Fits(state, state.Rot, pos))
        {
            return pos;
        }

        while (Fits(state, state.Rot, Below(pos)))
        {
            pos = Below(pos);
        }

        return pos;
    }

    private static char ReadPiece(HeaderReader reader, string key)
    {
        var text = reader.Get(key);
        return text.Length == 1 && Tetrominoes.IsPiece(text[0]) ? text[0] : throw HeaderReader.Bad(key, text);
    }

    private static Point Below(Point pos) => new(pos.X, pos.Y + 1);

    private static Point Offset(Point pos, Point cell) => new(pos.X + cell.X, pos.Y + cell.Y);

    private bool Fits(TetrisState state, int rot, Point pos)
    {
        foreach (var cell in Tetrominoes.Cells(state.Piece, rot))
        {
            var p = Offset(pos, cell);
            if (!p.IsInside(Width, Height) || state.Board[p])
            {
                return false;
            }
        }

        return true;
    }

    private TetrisState TryMove(TetrisState state, int dx)
    {
        var pos = new Point(state.Pos.X + dx, state.Pos.Y);
        return Fits(state, state.Rot, pos) ? state with { Pos = pos } : state;
    }

    private TetrisState TryRotate(TetrisState state)
    {
        var rot = (state.Rot + 1) & 3;
        foreach (var kick in Kicks)
        {
            var pos = new Point(state.Pos.X + kick, state.Pos.Y);
            if (Fits(state, rot, pos))
            {
                return state with { Rot = rot, Pos = pos };
            }
        }

        return state;
    }

    private TetrisState Gravity(TetrisState state)
    {
        var below = Below(state.Pos);
        return Fits(state, state.Rot, below) ? state with { Pos = below } : Lock(state);
    }

    /// <summary>Locks the piece, clears full rows, and spawns the next piece; a spawn collision ends the game.</summary>
    private TetrisState Lock(TetrisState state)
    {
        var board = state.Board;
        foreach (var cell in Tetrominoes.Cells(state.Piece, state.Rot))
        {
            var p = Offset(state.Pos, cell);
            if (p.IsInside(Width, Height))
            {
                board = board.With(p, true);
            }
        }

        board = board.ClearFullRows(out var cleared);
        var piece = state.Next;
        var spawned = state with
        {
            Board = board,
            // A hand-edited board can hold full rows already, so more than four can clear at once.
            Score = state.Score + LineScores[System.Math.Min(cleared, 4)],
            Lines = state.Lines + cleared,
            Piece = piece,
            Rot = 0,
            Pos = Tetrominoes.Spawn(piece),
            Next = PieceAt(state.Seed, state.Drawn),
            Drawn = state.Drawn + 1,
        };

        return Fits(spawned, 0, spawned.Pos) ? spawned : spawned with { Status = GameStatus.Over };
    }
}
