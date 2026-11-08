using System.Collections.Generic;
using System.Globalization;

namespace Squiggly.Engine;

/// <summary>
/// Snake state. <see cref="Tail"/> is a run of direction letters walking from the head towards the
/// tail: head <c>10,5</c> with tail <c>LL</c> occupies 10,5, 9,5 and 8,5. <see cref="Food"/> is null
/// once the board is full.
/// </summary>
public sealed record SnakeState : GameState
{
    public Direction Dir { get; init; }

    public Point Head { get; init; }

    public string Tail { get; init; } = "";

    public Point? Food { get; init; }

    /// <summary>True when the game ended because the snake filled the board.</summary>
    public bool Won => Status == GameStatus.Over && Food is null;

    /// <summary>The snake's cells, head first.</summary>
    public IReadOnlyList<Point> Body()
    {
        var cells = new List<Point>(Tail.Length + 1) { Head };
        var current = Head;
        foreach (var letter in Tail)
        {
            current = current.Step(Directions.FromLetter(letter));
            cells.Add(current);
        }

        return cells;
    }
}

/// <summary>
/// Snake on a 20 x 10 board. Each action advances one tick; a direction turns first, Wait does not,
/// and turning back into the neck counts as Wait. Walls and the body end the game.
/// </summary>
public sealed class SnakeGame : IGame
{
    public static SnakeGame Instance { get; } = new();

    public static readonly GameAction Up = new("up", "Move up");
    public static readonly GameAction Down = new("down", "Move down");
    public static readonly GameAction Left = new("left", "Move left");
    public static readonly GameAction Right = new("right", "Move right");
    public static readonly GameAction Wait = new("wait", "Wait");

    private static readonly GameAction[] ActionList = { Up, Down, Left, Right, Wait };

    private SnakeGame()
    {
    }

    public string Id => "snake";

    public string Name => "Snake";

    public int Width => 20;

    public int Height => 10;

    public bool DynamicCanvas => false;

    public IReadOnlyList<GameAction> Actions => ActionList;

    public GameState NewGame(uint seed)
    {
        var state = new SnakeState
        {
            Seed = seed,
            Status = GameStatus.Playing,
            Dir = Direction.Right,
            Head = new Point(Width / 2, Height / 2),
            Tail = "LL",
        };
        return state with { Food = SpawnFood(state, state.Body()) };
    }

    public GameState Step(GameState state, GameAction action)
    {
        var snake = (SnakeState)state;
        if (snake.Status != GameStatus.Playing)
        {
            return snake;
        }

        var dir = action.Id switch
        {
            "up" => Direction.Up,
            "down" => Direction.Down,
            "left" => Direction.Left,
            "right" => Direction.Right,
            _ => snake.Dir,
        };

        // Turning straight back into the neck is ignored and treated as Wait.
        if (snake.Tail.Length > 0 && Directions.ToLetter(dir) == snake.Tail[0])
        {
            dir = snake.Dir;
        }

        var next = snake.Head.Step(dir);
        var moved = snake with { Dir = dir, Tick = snake.Tick + 1 };
        if (!next.IsInside(Width, Height))
        {
            return moved with { Status = GameStatus.Over };
        }

        var body = snake.Body();
        var eating = snake.Food == next;
        var kept = eating ? body.Count : body.Count - 1;
        for (var i = 0; i < kept; i++)
        {
            if (body[i] == next)
            {
                return moved with { Status = GameStatus.Over };
            }
        }

        // The new snake is the new head plus the first `kept` old cells, so it has `kept` tail letters.
        var tail = kept == 0 ? "" : Directions.ToLetter(Directions.Opposite(dir)) + snake.Tail.Substring(0, kept - 1);
        moved = moved with { Head = next, Tail = tail };
        if (!eating)
        {
            return moved;
        }

        moved = moved with { Score = snake.Score + 1 };
        var food = SpawnFood(moved, moved.Body());
        return food is null
            ? moved with { Food = null, Status = GameStatus.Over }
            : moved with { Food = food };
    }

    public IReadOnlyList<GameAction> CellActions(GameState state, Point cell) => System.Array.Empty<GameAction>();

    public Frame Render(GameState state)
    {
        var snake = (SnakeState)state;
        var frame = new FrameBuilder(Width, Height);
        if (snake.Food is { } food)
        {
            frame.Set(food, PaletteColor.Accent, "food");
        }

        var body = snake.Body();
        for (var i = body.Count - 1; i >= 1; i--)
        {
            frame.Set(body[i], PaletteColor.Secondary, "snake");
        }

        frame.Set(snake.Head, PaletteColor.Primary, "snake head");
        return frame.Build();
    }

    public string Hud(GameState state)
    {
        var snake = (SnakeState)state;
        var paused = snake.Status == GameStatus.Paused ? " (paused)" : "";
        return string.Format(
            CultureInfo.InvariantCulture,
            "Snake{0}, score {1}, length {2}, tick {3}",
            paused,
            snake.Score,
            snake.Tail.Length + 1,
            snake.Tick);
    }

    public string GameOver(GameState state)
    {
        var snake = (SnakeState)state;
        return snake.Won
            ? $"YOU WIN, the board is full, score {snake.Score.ToString(CultureInfo.InvariantCulture)}. Caret here, Ctrl+. to play again"
            : $"GAME OVER, score {snake.Score.ToString(CultureInfo.InvariantCulture)}. Caret here, Ctrl+. to restart";
    }

    public IReadOnlyList<string> Canvas(GameState state) => Games.BlankCanvas(Width, Height);

    public GameState Read(HeaderReader reader)
    {
        var dirText = reader.Get("dir");
        if (dirText.Length != 1 || !Directions.TryParse(dirText[0], out var dir))
        {
            throw HeaderReader.Bad("dir", dirText);
        }

        var head = reader.Point("head", 0, Width - 1, 0, Height - 1);

        var tail = reader.Get("tail");
        var walk = head;
        foreach (var letter in tail)
        {
            if (!Directions.TryParse(letter, out var step) || !(walk = walk.Step(step)).IsInside(Width, Height))
            {
                throw HeaderReader.Bad("tail", tail);
            }
        }

        var foodText = reader.Get("food");
        Point? food = foodText == "-" ? null : reader.Point("food", 0, Width - 1, 0, Height - 1);

        return new SnakeState
        {
            Seed = reader.Seed,
            Tick = reader.Tick,
            Status = reader.Status,
            Score = reader.Score,
            Dir = dir,
            Head = head,
            Tail = tail,
            Food = food,
        };
    }

    public void Write(GameState state, HeaderWriter writer)
    {
        var snake = (SnakeState)state;
        writer.Add("dir", Directions.ToLetter(snake.Dir).ToString());
        writer.Add("head", snake.Head);
        writer.Add("tail", snake.Tail);
        writer.Add("food", snake.Food is { } food ? food.ToString() : "-");
    }

    /// <summary>Picks a random empty cell from seed and tick, or null when the board is full.</summary>
    private Point? SpawnFood(SnakeState state, IReadOnlyList<Point> body)
    {
        var occupied = new HashSet<Point>(body);
        var empty = new List<Point>();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var cell = new Point(x, y);
                if (!occupied.Contains(cell))
                {
                    empty.Add(cell);
                }
            }
        }

        if (empty.Count == 0)
        {
            return null;
        }

        var random = Prng.For(state.Seed, state.Tick, stream: 1);
        return empty[random.NextBelow(empty.Count)];
    }
}
