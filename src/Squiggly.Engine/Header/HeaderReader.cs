using System;
using System.Collections.Generic;
using System.Globalization;

namespace Squiggly.Engine;

/// <summary>Raised while parsing a header. Never escapes <see cref="Header.Parse"/>.</summary>
public sealed class HeaderException : Exception
{
    public HeaderException(string key, string message)
        : base(message)
    {
        Key = key;
    }

    /// <summary>The key (or token) that could not be parsed.</summary>
    public string Key { get; }
}

/// <summary>
/// Typed access to a header's <c>key=value</c> pairs. Common keys are read up front; each game
/// reads its own keys, and any key nobody read is reported as unknown.
/// </summary>
public sealed class HeaderReader
{
    private readonly List<KeyValuePair<string, string>> _pairs;
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    internal HeaderReader(List<KeyValuePair<string, string>> pairs)
    {
        _pairs = pairs;
        Seed = UInt("seed");
        Tick = Int("tick", 0, int.MaxValue);
        Status = Get("status") switch
        {
            "playing" => GameStatus.Playing,
            "paused" => GameStatus.Paused,
            "over" => GameStatus.Over,
            var other => throw Bad("status", other),
        };
        Score = Int("score", 0, int.MaxValue);
    }

    public uint Seed { get; }

    public int Tick { get; }

    public GameStatus Status { get; }

    public int Score { get; }

    public string Get(string key)
    {
        foreach (var pair in _pairs)
        {
            if (pair.Key == key)
            {
                _used.Add(key);
                return pair.Value;
            }
        }

        throw new HeaderException(key, $"missing key '{key}'");
    }

    public int Int(string key, int min, int max)
    {
        var text = Get(key);
        if (!TryParseInt(text, out var value) || value < min || value > max)
        {
            throw Bad(key, text);
        }

        return value;
    }

    public uint UInt(string key)
    {
        var text = Get(key);
        if (text.Length == 0 || (text.Length > 1 && text[0] == '0')
            || !uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            throw Bad(key, text);
        }

        return value;
    }

    /// <summary>Reads <c>x,y</c>; each coordinate must lie within its inclusive range.</summary>
    public Point Point(string key, int minX, int maxX, int minY, int maxY)
    {
        var text = Get(key);
        var parts = text.Split(',');
        if (parts.Length != 2 || !TryParseInt(parts[0], out var x) || !TryParseInt(parts[1], out var y)
            || x < minX || x > maxX || y < minY || y > maxY)
        {
            throw Bad(key, text);
        }

        return new Point(x, y);
    }

    public BitGrid Grid(string key, int width, int height)
    {
        var text = Get(key);
        return BitGrid.TryParse(text, width, height, out var grid) ? grid : throw Bad(key, text);
    }

    public static HeaderException Bad(string key, string value) =>
        new(key, $"bad value '{value}' for key '{key}'");

    internal void EnsureAllUsed()
    {
        foreach (var pair in _pairs)
        {
            if (!_used.Contains(pair.Key))
            {
                throw new HeaderException(pair.Key, $"unknown key '{pair.Key}'");
            }
        }
    }

    /// <summary>Canonical integers only: optional '-', no leading zeros, no '+', no spaces.</summary>
    private static bool TryParseInt(string text, out int value)
    {
        value = 0;
        var digits = text.StartsWith("-", StringComparison.Ordinal) ? text.Substring(1) : text;
        if (digits.Length == 0 || (digits.Length > 1 && digits[0] == '0') || text == "-0")
        {
            return false;
        }

        foreach (var c in digits)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }
        }

        return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }
}
