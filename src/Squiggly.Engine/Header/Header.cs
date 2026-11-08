using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Squiggly.Engine;

/// <summary>Appends <c>key=value</c> pairs to a header in the order they are written.</summary>
public sealed class HeaderWriter
{
    private readonly StringBuilder _builder;

    internal HeaderWriter(StringBuilder builder)
    {
        _builder = builder;
    }

    public void Add(string key, string value) => _builder.Append(' ').Append(key).Append('=').Append(value);

    public void Add(string key, int value) => Add(key, value.ToString(CultureInfo.InvariantCulture));

    public void Add(string key, Point value) => Add(key, value.ToString());
}

/// <summary>The outcome of parsing a header line.</summary>
public enum HeaderKind
{
    /// <summary>A bare <c>// squiggly</c> header: no game yet.</summary>
    Ready,

    /// <summary>A game in progress or finished.</summary>
    Game,

    /// <summary>The header could not be parsed.</summary>
    Error,
}

/// <summary>A parsed header: ready, a game with its state, or an error naming the bad key.</summary>
public sealed class HeaderResult
{
    private HeaderResult(HeaderKind kind, IGame? game, GameState? state, string? errorKey, string? error)
    {
        Kind = kind;
        Game = game;
        State = state;
        ErrorKey = errorKey;
        Error = error;
    }

    public HeaderKind Kind { get; }

    public IGame? Game { get; }

    public GameState? State { get; }

    public string? ErrorKey { get; }

    public string? Error { get; }

    public static HeaderResult Ready { get; } = new(HeaderKind.Ready, null, null, null, null);

    public static HeaderResult ForGame(IGame game, GameState state) => new(HeaderKind.Game, game, state, null, null);

    public static HeaderResult Failed(string key, string message) => new(HeaderKind.Error, null, null, key, message);
}

/// <summary>
/// The one-line state header: <c>// squiggly v1</c> followed by space-separated <c>key=value</c> pairs.
/// Serialization is canonical (fixed key order, single spaces), so the same state always produces a
/// byte-identical header, and <c>Parse(Serialize(s))</c> returns an equal state.
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

        var pairs = new List<KeyValuePair<string, string>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i < tokens.Length; i++)
        {
            var token = tokens[i];
            var equals = token.IndexOf('=');
            if (equals <= 0)
            {
                return HeaderResult.Failed(token, $"expected key=value but found '{token}'");
            }

            var key = token.Substring(0, equals);
            if (!seen.Add(key))
            {
                return HeaderResult.Failed(key, $"duplicate key '{key}'");
            }

            pairs.Add(new KeyValuePair<string, string>(key, token.Substring(equals + 1)));
        }

        try
        {
            var gameId = pairs[0].Key == "game" ? pairs[0].Value : null;
            if (gameId is null)
            {
                foreach (var pair in pairs)
                {
                    if (pair.Key == "game")
                    {
                        gameId = pair.Value;
                    }
                }
            }

            if (gameId is null)
            {
                return HeaderResult.Failed("game", "missing key 'game'");
            }

            var game = Games.Find(gameId);
            if (game is null)
            {
                return HeaderResult.Failed("game", $"unknown game '{gameId}'");
            }

            var reader = new HeaderReader(pairs);
            reader.Get("game");
            var state = game.Read(reader);
            reader.EnsureAllUsed();
            return HeaderResult.ForGame(game, state);
        }
        catch (HeaderException ex)
        {
            return HeaderResult.Failed(ex.Key, ex.Message);
        }
    }

    public static string Serialize(IGame game, GameState state)
    {
        var builder = new StringBuilder(Prefix).Append(' ').Append(Version);
        var writer = new HeaderWriter(builder);
        writer.Add("game", game.Id);
        writer.Add("seed", state.Seed.ToString(CultureInfo.InvariantCulture));
        writer.Add("tick", state.Tick);
        writer.Add("status", state.Status switch
        {
            GameStatus.Playing => "playing",
            GameStatus.Paused => "paused",
            _ => "over",
        });
        writer.Add("score", state.Score);
        game.Write(state, writer);
        return builder.ToString();
    }
}
