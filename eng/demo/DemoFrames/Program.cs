using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Squiggly.Engine;

// Plays a scripted game through the same GameFile code the analyzer runs, and writes one frame per
// step: the file's lines, every diagnostic mark, the caret and any open lightbulb menu.
// eng/demo/render_gif.py turns the frames into a GIF.
//
//   dotnet run -- snake frames.json
//   dotnet run -- tetris frames.json

var gameId = args.Length > 0 ? args[0] : "snake";
var output = args.Length > 1 ? args[1] : "frames.json";
var game = Games.Find(gameId) ?? throw new ArgumentException($"unknown game '{gameId}'");
var seed = gameId == "snake" ? 2026u : 7u;

var frames = new List<object>();
var header = Header.Serialize(game, seed);
var canvas = Canvas.Lines(game, game.NewGame(seed), "").ToList();
var keysLine = canvas.Count - 1;

void AddFrame(string headerLine, IReadOnlyList<string> body, (int Line, int Column) caret, int milliseconds, string[]? menu = null)
{
    var file = GameFile.Read(headerLine, body.ToArray());
    var lines = new List<string> { headerLine };
    lines.AddRange(body);
    frames.Add(new
    {
        lines,
        marks = file.Marks.Select(m => new { kind = m.Kind.ToString(), line = m.Row + 1, column = m.Column, length = m.Length, message = m.Message }),
        caret = new { line = caret.Line, column = caret.Column },
        menu,
        ms = milliseconds,
    });
}

List<string> Body(string keys)
{
    var body = canvas.ToList();
    body[keysLine] = Canvas.KeysLine("", keys);
    body.Add("namespace Arcade;");
    return body;
}

void AddKeysFrame(string keys, int milliseconds) =>
    AddFrame(header, Body(keys), (keysLine + 1, Canvas.KeysLine("", keys).Length), milliseconds);

// 1. A fresh file: just the header, then the lightbulb menu with this game highlighted.
var ready = new[] { "namespace Arcade;" };
var menu = Controller.Menu(GameFile.Read("// squiggly", ready)).Select(c => c.Title).ToList();
var chosen = menu.FindIndex(title => title.EndsWith(game.Name, StringComparison.Ordinal));
menu.Insert(0, menu[chosen]);
menu.RemoveAt(chosen + 1);
AddFrame("// squiggly", ready, (0, 11), 1400);
AddFrame("// squiggly", ready, (0, 11), 1600, menu.ToArray());

// 2. The new game: header, canvas and an empty keys line, caret at the end of the keys line.
AddKeysFrame("", 1200);

// 3. Type the moves.
var keys = game is TetrisGame ? PlayTetris(seed, AddKeysFrame) : PlaySnake(seed, AddKeysFrame);
var final = GameFile.Replay(game, game.NewGame(seed), keys);

File.WriteAllText(output, JsonSerializer.Serialize(frames));
Console.WriteLine($"{frames.Count} frames, {keys.Length} keys, final score {final.Score}, {final.Status}, written to {output}");

// A greedy Snake player: head for the food, avoiding moves that end the game; then run into the wall.
static string PlaySnake(uint seed, Action<string, int> frame)
{
    var game = SnakeGame.Instance;
    var start = game.NewGame(seed);
    var keys = "";
    var state = (SnakeState)start;
    for (var i = 0; i < 70 && state.Status == GameStatus.Playing; i++)
    {
        keys = game.AppendKey(keys, state, Chase(state));
        state = (SnakeState)GameFile.Replay(game, start, keys);
        frame(keys, 140);
    }

    while (state.Status == GameStatus.Playing)
    {
        keys = game.AppendKey(keys, state, SnakeGame.Wait);
        state = (SnakeState)GameFile.Replay(game, start, keys);
        frame(keys, state.Status == GameStatus.Over ? 3000 : 140);
    }

    return keys;
}

static GameAction Chase(SnakeState state)
{
    var game = SnakeGame.Instance;
    var food = state.Food ?? state.Head;
    var dx = food.X - state.Head.X;
    var dy = food.Y - state.Head.Y;
    var horizontal = dx < 0 ? SnakeGame.Left : SnakeGame.Right;
    var vertical = dy < 0 ? SnakeGame.Up : SnakeGame.Down;
    var options = new List<GameAction>();
    if (Math.Abs(dx) >= Math.Abs(dy))
    {
        if (dx != 0) options.Add(horizontal);
        if (dy != 0) options.Add(vertical);
    }
    else
    {
        if (dy != 0) options.Add(vertical);
        if (dx != 0) options.Add(horizontal);
    }

    options.AddRange(new[] { SnakeGame.Wait, SnakeGame.Up, SnakeGame.Right, SnakeGame.Down, SnakeGame.Left });
    return options.FirstOrDefault(a => game.Step(state, a).Status == GameStatus.Playing) ?? SnakeGame.Wait;
}

// A simple Tetris player: for each piece, try every rotation and column, and pick the placement that
// clears the most lines and leaves the flattest board with the fewest holes. Stops before the keys line
// gets too long to show.
static string PlayTetris(uint seed, Action<string, int> frame)
{
    var game = TetrisGame.Instance;
    var start = game.NewGame(seed);
    var keys = "";
    var state = (TetrisState)start;
    while (keys.Length < 66 && state.Status == GameStatus.Playing)
    {
        string? best = null;
        var bestScore = double.MinValue;
        for (var rot = 0; rot < 4; rot++)
        {
            for (var dx = -5; dx <= 5; dx++)
            {
                var plan = new string('w', rot) + new string(dx < 0 ? 'a' : 'd', Math.Abs(dx)) + "x";
                var after = (TetrisState)GameFile.Replay(game, state, plan);
                var score = Evaluate(after) - plan.Length * 0.01;
                if (score > bestScore)
                {
                    (best, bestScore) = (plan, score);
                }
            }
        }

        foreach (var key in best!)
        {
            keys += key;
            frame(keys, key == 'x' ? 380 : 170);
        }

        state = (TetrisState)GameFile.Replay(game, start, keys);
    }

    frame(keys, 3000);
    return keys;
}

static double Evaluate(TetrisState state)
{
    if (state.Status == GameStatus.Over)
    {
        return -1e9;
    }

    var heights = new int[TetrisGame.BoardWidth];
    var holes = 0;
    for (var x = 0; x < TetrisGame.BoardWidth; x++)
    {
        var seenBlock = false;
        for (var y = 0; y < TetrisGame.BoardHeight; y++)
        {
            if (state.Board[x, y])
            {
                if (!seenBlock)
                {
                    heights[x] = TetrisGame.BoardHeight - y;
                }

                seenBlock = true;
            }
            else if (seenBlock)
            {
                holes++;
            }
        }
    }

    var bumpiness = 0;
    for (var x = 1; x < heights.Length; x++)
    {
        bumpiness += Math.Abs(heights[x] - heights[x - 1]);
    }

    return state.Lines * 8.0 - heights.Sum() * 0.5 - holes * 3.5 - bumpiness * 0.35;
}
