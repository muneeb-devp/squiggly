using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Squiggly.Engine;

/// <summary>Appends <c>key=value</c> pairs to a state snapshot in the order they are written.</summary>
public sealed class HeaderWriter
{
    private readonly StringBuilder _builder;

    internal HeaderWriter(StringBuilder builder)
    {
        _builder = builder;
    }

    public void Add(string key, string value)
    {
        if (_builder.Length > 0)
        {
            _builder.Append(' ');
        }

        _builder.Append(key).Append('=').Append(value);
    }

    public void Add(string key, int value) => Add(key, value.ToString(CultureInfo.InvariantCulture));

    public void Add(string key, Point value) => Add(key, value.ToString());
}

/// <summary>The outcome of parsing a header line.</summary>
public enum HeaderKind
{
    /// <summary>A bare <c>// squiggly</c> header: no game yet.</summary>
    Ready,

    /// <summary>A game: which one, and its seed.</summary>
    Game,

    /// <summary>The header could not be parsed.</summary>
    Error,
}

/// <summary>A parsed header: ready, a game with its seed, or an error naming the bad key.</summary>
public sealed class HeaderResult
{
    private HeaderResult(HeaderKind kind, IGame? game, uint seed, string? errorKey, string? error)
    {
        Kind = kind;
        Game = game;
        Seed = seed;
        ErrorKey = errorKey;
        Error = error;
    }

    public HeaderKind Kind { get; }

    public IGame? Game { get; }

    public uint Seed { get; }

    public string? ErrorKey { get; }

    public string? Error { get; }

    public static HeaderResult Ready { get; } = new(HeaderKind.Ready, null, 0, null, null);

    public static HeaderResult ForGame(IGame game, uint seed) => new(HeaderKind.Game, game, seed, null, null);

    public static HeaderResult Failed(string key, string message) => new(HeaderKind.Error, null, 0, key, message);
}

/// <summary>
/// The one-line header: <c>// squiggly v1 game=snake seed=2026</c>. It names the game and the seed it
/// started from; everything else (positions, score, the board) follows from replaying the moves on the
/// <c>// keys:</c> line, so the header never goes stale and never needs rewriting during play.
/// </summary>
public static class Header
{
    public const string Prefix = "// squiggly";
    public const string Version = "v1";

    /// <summary>How many lines from the top of a file are searched for a header.</summary>
    public const int SearchLines = 5;

    /// <summary>The bare header that means "no game yet".</summary>
    public const string ReadyLine = Prefix;

    /// <summary>
    /// True when <paramref name="line"/>, trimmed, starts with <c>// squiggly</c> followed by the end of
    /// the line or whitespace. Cheap: no allocation.
    /// </summary>
    public static bool IsHeader(string line)
    {
        var start = 0;
        while (start < line.Length && char.IsWhiteSpace(line[start]))
        {
            start++;
        }

        if (string.CompareOrdinal(line, start, Prefix, 0, Prefix.Length) != 0)
        {
            return false;
        }

        var next = start + Prefix.Length;
        return next == line.Length || char.IsWhiteSpace(line[next]);
    }

    public static HeaderResult Parse(string line)
    {
        var body = line.Trim();
        var tokens = body.Substring(Math.Min(Prefix.Length, body.Length))
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)
        {
            return HeaderResult.Ready;
        }

        if (tokens[0] != Version)
        {
            return HeaderResult.Failed("version", $"unsupported version '{tokens[0]}', expected '{Version}'");
        }

        if (tokens.Length == 1)
        {
            return HeaderResult.Ready;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < tokens.Length; i++)
        {
            var token = tokens[i];
            var equals = token.IndexOf('=');
            if (equals <= 0)
            {
                return HeaderResult.Failed(token, $"expected key=value but found '{token}'");
            }

            var key = token.Substring(0, equals);
            if (key != "game" && key != "seed")
            {
                return HeaderResult.Failed(key, $"unknown key '{key}'");
            }

            if (values.ContainsKey(key))
            {
                return HeaderResult.Failed(key, $"duplicate key '{key}'");
            }

            values[key] = token.Substring(equals + 1);
        }

        if (!values.TryGetValue("game", out var gameId))
        {
            return HeaderResult.Failed("game", "missing key 'game'");
        }

        var game = Games.Find(gameId);
        if (game is null)
        {
            return HeaderResult.Failed("game", $"unknown game '{gameId}'");
        }

        if (!values.TryGetValue("seed", out var seedText))
        {
            return HeaderResult.Failed("seed", "missing key 'seed'");
        }

        if (seedText.Length == 0 || (seedText.Length > 1 && seedText[0] == '0')
            || !uint.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
        {
            return HeaderResult.Failed("seed", $"bad value '{seedText}' for key 'seed'");
        }

        return HeaderResult.ForGame(game, seed);
    }

    public static string Serialize(IGame game, uint seed) =>
        Prefix + " " + Version + " game=" + game.Id + " seed=" + seed.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A canonical one-line snapshot of a full game state, for golden tests and debugging. It is never
    /// written into game files.
    /// </summary>
    public static string Describe(IGame game, GameState state)
    {
        var builder = new StringBuilder();
        var writer = new HeaderWriter(builder);
        writer.Add("game", game.Id);
        writer.Add("seed", state.Seed.ToString(CultureInfo.InvariantCulture));
        writer.Add("tick", state.Tick);
        writer.Add("status", state.Status == GameStatus.Playing ? "playing" : "over");
        writer.Add("score", state.Score);
        game.Write(state, writer);
        return builder.ToString();
    }
}
