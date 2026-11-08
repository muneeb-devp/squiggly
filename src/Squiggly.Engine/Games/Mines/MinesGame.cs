using System.Collections.Generic;
using System.Globalization;

namespace Squiggly.Engine;

/// <summary>
/// Minesweeper state. <see cref="Mines"/> is null until the first reveal, because mines are placed
/// then (never on the revealed cell). <see cref="Open"/> holds revealed cells and <see cref="Flags"/>
/// flagged ones. The score is the number of revealed safe cells.
/// </summary>
public sealed record MinesState : GameState
{
    public BitGrid? Mines { get; init; }

    public BitGrid Open { get; init; } = BitGrid.Empty(MinesGame.BoardWidth, MinesGame.BoardHeight);

    public BitGrid Flags { get; init; } = BitGrid.Empty(MinesGame.BoardWidth, MinesGame.BoardHeight);

    /// <summary>True when the game ended on a revealed mine.</summary>
    public bool Lost => Status == GameStatus.Over && Mines is { } mines && HitMine(mines);

    private bool HitMine(BitGrid mines)
    {
        for (var y = 0; y < Open.Height; y++)
        {
            for (var x = 0; x < Open.Width; x++)
            {
                if (Open[x, y] && mines[x, y])
                {
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>
/// Minesweeper on a 16 x 8 board with 16 mines. Input targets a cell: every covered cell carries its
/// own diagnostic, and the code fix reads that diagnostic's location. Revealed numbers cannot be
/// drawn with squiggles, so this is the one game whose actions also rewrite canvas characters.
/// </summary>
public sealed class MinesGame : IGame
{
    public const int BoardWidth = 16;
    public const int BoardHeight = 8;
    public const int MineCount = 16;

    public static MinesGame Instance { get; } = new();

    private MinesGame()
    {
    }

    public string Id => "mines";

    public string Name => "Minesweeper";

    public int Width => BoardWidth;

    public int Height => BoardHeight;

    public bool DynamicCanvas => true;

    public IReadOnlyList<GameAction> Actions => System.Array.Empty<GameAction>();

    public static GameAction Reveal(Point cell) => new("reveal", "Reveal", cell);

    public static GameAction Flag(Point cell) => new("flag", "Flag", cell);

    public static GameAction Unflag(Point cell) => new("unflag", "Unflag", cell);

    public GameState NewGame(uint seed) => new MinesState { Seed = seed, Status = GameStatus.Playing };

    public GameState Step(GameState state, GameAction action)
    {
        var mines = (MinesState)state;
        if (mines.Status != GameStatus.Playing || action.Target is not { } cell || !cell.IsInside(Width, Height)
            || mines.Open[cell])
        {
            return mines;
        }

        switch (action.Id)
        {
            case "flag" when !mines.Flags[cell]:
            case "unflag" when mines.Flags[cell]:
                return mines with { Tick = mines.Tick + 1, Flags = mines.Flags.With(cell, !mines.Flags[cell]) };
            case "reveal" when !mines.Flags[cell]:
                return RevealCell(mines, cell);
            default:
                return mines;
        }
    }

    public IReadOnlyList<GameAction> CellActions(GameState state, Point cell)
    {
        var mines = (MinesState)state;
        if (mines.Status != GameStatus.Playing || !cell.IsInside(Width, Height) || mines.Open[cell])
        {
            return System.Array.Empty<GameAction>();
        }

        return mines.Flags[cell] ? new[] { Unflag(cell) } : new[] { Reveal(cell), Flag(cell) };
    }

    public Frame Render(GameState state)
    {
        var mines = (MinesState)state;
        var frame = new FrameBuilder(Width, Height);
        var over = mines.Status == GameStatus.Over;
        var lost = mines.Lost;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var cell = new Point(x, y);
                var mine = mines.Mines is { } layout && layout[cell];
                if (over && mine)
                {
                    frame.Set(cell, lost ? PaletteColor.Primary : PaletteColor.Flag, lost ? "mine" : "mine (cleared)");
                }
                else if (mines.Flags[cell])
                {
                    frame.Set(cell, PaletteColor.Flag, "flag");
                }
                else if (!over && !mines.Open[cell])
                {
                    frame.Set(cell, PaletteColor.Covered, "covered");
                }
            }
        }

        return frame.Build();
    }

    public string Hud(GameState state)
    {
        var mines = (MinesState)state;
        var paused = mines.Status == GameStatus.Paused ? " (paused)" : "";
        return string.Format(
            CultureInfo.InvariantCulture,
            "Minesweeper{0}, {1} mines, {2} flagged, {3} of {4} cleared, tick {5}. Caret on a dotted cell, Ctrl+. to reveal or flag",
            paused,
            MineCount,
            mines.Flags.Count(),
            mines.Score,
            Width * Height - MineCount,
            mines.Tick);
    }

    public string GameOver(GameState state)
    {
        var mines = (MinesState)state;
        return mines.Lost
            ? $"BOOM, you hit a mine with {mines.Score.ToString(CultureInfo.InvariantCulture)} cells cleared. Caret here, Ctrl+. to restart"
            : $"YOU WIN, all {MineCount.ToString(CultureInfo.InvariantCulture)} mines found in {mines.Tick.ToString(CultureInfo.InvariantCulture)} moves. Caret here, Ctrl+. to play again";
    }

    /// <summary>Revealed cells show their neighbour count (blank for zero, ** for a mine); covered cells keep the fill.</summary>
    public IReadOnlyList<string> Canvas(GameState state)
    {
        var mines = (MinesState)state;
        var rows = new string[Height];
        var chars = new char[Width * 2];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                string text;
                if (!mines.Open[x, y] || mines.Mines is not { } layout)
                {
                    text = new string(Engine.Canvas.Fill, 2);
                }
                else if (layout[x, y])
                {
                    text = "**";
                }
                else
                {
                    var count = Neighbours(layout, new Point(x, y));
                    text = count == 0 ? "  " : " " + count.ToString(CultureInfo.InvariantCulture);
                }

                chars[x * 2] = text[0];
                chars[x * 2 + 1] = text[1];
            }

            rows[y] = new string(chars);
        }

        return rows;
    }

    public GameState Read(HeaderReader reader)
    {
        BitGrid? layout = null;
        var minesText = reader.Get("mines");
        if (minesText != "-")
        {
            layout = reader.Grid("mines", Width, Height);
            if (layout.Count() != MineCount)
            {
                throw HeaderReader.Bad("mines", minesText);
            }
        }

        return new MinesState
        {
            Seed = reader.Seed,
            Tick = reader.Tick,
            Status = reader.Status,
            Score = reader.Score,
            Mines = layout,
            Open = reader.Grid("open", Width, Height),
            Flags = reader.Grid("flags", Width, Height),
        };
    }

    public void Write(GameState state, HeaderWriter writer)
    {
        var mines = (MinesState)state;
        writer.Add("mines", mines.Mines?.Serialize() ?? "-");
        writer.Add("open", mines.Open.Serialize());
        writer.Add("flags", mines.Flags.Serialize());
    }

    public static int Neighbours(BitGrid layout, Point cell)
    {
        var count = 0;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if ((dx != 0 || dy != 0) && layout[cell.X + dx, cell.Y + dy])
                {
                    count++;
                }
            }
        }

        return count;
    }

    private MinesState RevealCell(MinesState state, Point cell)
    {
        var layout = state.Mines ?? PlaceMines(state.Seed, state.Tick, cell);
        var next = state with { Tick = state.Tick + 1, Mines = layout };
        if (layout[cell])
        {
            return next with { Open = next.Open.With(cell, true), Status = GameStatus.Over };
        }

        // Flood fill from the cell through zero-count cells, skipping flags.
        var open = next.Open;
        var pending = new Stack<Point>();
        pending.Push(cell);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!current.IsInside(Width, Height) || open[current] || next.Flags[current] || layout[current])
            {
                continue;
            }

            open = open.With(current, true);
            if (Neighbours(layout, current) == 0)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        pending.Push(new Point(current.X + dx, current.Y + dy));
                    }
                }
            }
        }

        var cleared = open.Count();
        return next with
        {
            Open = open,
            Score = cleared,
            Status = cleared == Width * Height - MineCount ? GameStatus.Over : GameStatus.Playing,
        };
    }

    /// <summary>Places the mines on the first reveal, never on <paramref name="safe"/>, so the first click is always safe.</summary>
    private BitGrid PlaceMines(uint seed, int tick, Point safe)
    {
        var candidates = new List<Point>();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (x != safe.X || y != safe.Y)
                {
                    candidates.Add(new Point(x, y));
                }
            }
        }

        var random = Prng.For(seed, tick, stream: 3);
        var layout = BitGrid.Empty(Width, Height);
        for (var i = 0; i < MineCount; i++)
        {
            var j = i + random.NextBelow(candidates.Count - i);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            layout = layout.With(candidates[i], true);
        }

        return layout;
    }
}
