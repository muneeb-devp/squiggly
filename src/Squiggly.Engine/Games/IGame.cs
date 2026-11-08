using System.Collections.Generic;

namespace Squiggly.Engine;

/// <summary>Whether a game is running or finished. The "ready" state is a bare header with no game.</summary>
public enum GameStatus
{
    Playing,
    Over,
}

/// <summary>
/// One player input. <see cref="Id"/> is stable and feeds the code action's equivalence key;
/// <see cref="Target"/> is set only for cell-targeted input (Minesweeper).
/// </summary>
public sealed record GameAction(string Id, string Title, Point? Target = null);

/// <summary>
/// State shared by every game. Each game derives a record with its own fields. States are never written
/// to files: they are rebuilt by replaying the moves on the <c>// keys:</c> line from the seed.
/// </summary>
public abstract record GameState
{
    /// <summary>PRNG seed, set when the game starts.</summary>
    public uint Seed { get; init; }

    /// <summary>Number of moves taken; also feeds the PRNG.</summary>
    public int Tick { get; init; }

    public GameStatus Status { get; init; }

    public int Score { get; init; }
}

/// <summary>
/// A turn-based game. Implementations are stateless singletons: <see cref="Step"/> and
/// <see cref="Render"/> are pure functions of their arguments.
/// </summary>
public interface IGame
{
    /// <summary>The <c>game=</c> header value, for example "snake".</summary>
    string Id { get; }

    /// <summary>Display name, for example "Snake".</summary>
    string Name { get; }

    /// <summary>Board width in cells. Each cell is two canvas characters wide.</summary>
    int Width { get; }

    /// <summary>Board height in cells, which is also the number of canvas rows.</summary>
    int Height { get; }

    /// <summary>True when actions also rewrite canvas characters (Minesweeper's revealed numbers).</summary>
    bool DynamicCanvas { get; }


    /// <summary>Actions offered on the header line while playing, in menu order.</summary>
    IReadOnlyList<GameAction> Actions { get; }

    GameState NewGame(uint seed);

    /// <summary>Applies one action plus one tick. Returns the state unchanged unless it is playing.</summary>
    GameState Step(GameState state, GameAction action);

    /// <summary>Actions offered on a single cell, in menu order. Empty for games without cell input.</summary>
    IReadOnlyList<GameAction> CellActions(GameState state, Point cell);

    /// <summary>
    /// The moves written on the <c>// keys:</c> line, in order. Anything that is not a move is skipped,
    /// so stray characters never break a game.
    /// </summary>
    IReadOnlyList<GameAction> ParseKeys(string keys);

    /// <summary>The keys line after recording <paramref name="action"/>, taken from <paramref name="state"/> (used by menu moves).</summary>
    string AppendKey(string keys, GameState state, GameAction action);

    Frame Render(GameState state);

    /// <summary>The header message while playing.</summary>
    string Hud(GameState state);

    /// <summary>The header message once the game is over, win or loss.</summary>
    string GameOver(GameState state);

    /// <summary>The canvas contents between the bars, one string of <c>Width * 2</c> characters per row.</summary>
    IReadOnlyList<string> Canvas(GameState state);

    /// <summary>Writes the game's own fields, in canonical order, for <see cref="Header.Describe"/>.</summary>
    void Write(GameState state, HeaderWriter writer);
}

/// <summary>The registry of games, in "New game" menu order.</summary>
public static class Games
{
    public static IReadOnlyList<IGame> All { get; } = new IGame[] { SnakeGame.Instance, TetrisGame.Instance, MinesGame.Instance };

    public static IGame? Find(string id)
    {
        foreach (var game in All)
        {
            if (game.Id == id)
            {
                return game;
            }
        }

        return null;
    }

    /// <summary>The largest board height, which bounds how many lines the analyzer reads after the header.</summary>
    public static int MaxHeight
    {
        get
        {
            var max = 0;
            foreach (var game in All)
            {
                max = game.Height > max ? game.Height : max;
            }

            return max;
        }
    }

    /// <summary>A canvas of <paramref name="height"/> rows filled with <see cref="Canvas.Fill"/>.</summary>
    public static IReadOnlyList<string> BlankCanvas(int width, int height)
    {
        var rows = new string[height];
        for (var y = 0; y < height; y++)
        {
            rows[y] = new string(Engine.Canvas.Fill, width * 2);
        }

        return rows;
    }
}
