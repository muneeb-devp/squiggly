using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Squiggly.Engine;

// Plays a scripted Snake game through the same GameFile code the analyzer runs, and writes one frame
// per step: the file's lines, every diagnostic mark, the caret and any open lightbulb menu.
// eng/demo/render_gif.py turns the frames into assets/demo.gif.

const uint Seed = 2026;
const int Moves = 70;

var output = args.Length > 0 ? args[0] : "frames.json";
var frames = new List<object>();

void AddFrame(string header, IReadOnlyList<string> body, (int Line, int Column) caret, int milliseconds, string[]? menu = null)
{
    var file = GameFile.Read(header, body.ToArray());
    var lines = new List<string> { header };
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

// 1. A fresh file: just the header, then the lightbulb menu.
var ready = new[] { "namespace Arcade;" };
AddFrame("// squiggly", ready, (0, 11), 1400);
AddFrame("// squiggly", ready, (0, 11), 1600, Controller.Menu(GameFile.Read("// squiggly", ready)).Select(c => c.Title).ToArray());

// 2. New game: Snake writes the header, the canvas and an empty keys line.
var game = SnakeGame.Instance;
var start = (SnakeState)game.NewGame(Seed);
var header = Header.Serialize(game, start);
var canvas = Canvas.Lines(game, start, "").ToList();
var keysLine = canvas.Count - 1;
List<string> Body(string keys)
{
    var body = canvas.ToList();
    body[keysLine] = Canvas.KeysLine("", keys);
    body.Add("namespace Arcade;");
    return body;
}

AddFrame(header, Body(""), (keysLine + 1, Canvas.KeysPrefix.Length), 1200);

// 3. Type keys: a greedy player chases the food, then runs into the wall to show the game-over screen.
var keys = "";
var state = start;
for (var i = 0; i < Moves && state.Status == GameStatus.Playing; i++)
{
    keys += Chase(state);
    state = (SnakeState)GameFile.Replay(game, start, keys);
    var line = Canvas.KeysLine("", keys);
    AddFrame(header, Body(keys), (keysLine + 1, line.Length), 140);
}

while (state.Status == GameStatus.Playing)
{
    keys += Letter(state.Dir);
    state = (SnakeState)GameFile.Replay(game, start, keys);
    AddFrame(header, Body(keys), (keysLine + 1, Canvas.KeysLine("", keys).Length), state.Status == GameStatus.Over ? 3000 : 140);
}

File.WriteAllText(output, JsonSerializer.Serialize(frames));
Console.WriteLine($"{frames.Count} frames, final score {state.Score}, written to {output}");

static char Letter(Direction direction) => direction switch
{
    Direction.Up => 'w',
    Direction.Left => 'a',
    Direction.Down => 's',
    _ => 'd',
};

// Head for the food along the axis with the larger gap, avoiding moves that end the game.
static char Chase(SnakeState state)
{
    var game = SnakeGame.Instance;
    var food = state.Food ?? state.Head;
    var options = new List<Direction>();
    var dx = food.X - state.Head.X;
    var dy = food.Y - state.Head.Y;
    if (Math.Abs(dx) >= Math.Abs(dy))
    {
        if (dx != 0) options.Add(dx < 0 ? Direction.Left : Direction.Right);
        if (dy != 0) options.Add(dy < 0 ? Direction.Up : Direction.Down);
    }
    else
    {
        if (dy != 0) options.Add(dy < 0 ? Direction.Up : Direction.Down);
        if (dx != 0) options.Add(dx < 0 ? Direction.Left : Direction.Right);
    }

    options.Add(state.Dir);
    options.AddRange(new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left });
    foreach (var direction in options)
    {
        if (game.Step(state, game.KeyAction(Letter(direction))!).Status == GameStatus.Playing)
        {
            return Letter(direction);
        }
    }

    return Letter(state.Dir);
}
