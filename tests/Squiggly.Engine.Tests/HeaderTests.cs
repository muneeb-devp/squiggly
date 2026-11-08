using System.Linq;

namespace Squiggly.Engine.Tests;

public class HeaderTests
{
    [Theory]
    [InlineData("// squiggly")]
    [InlineData("   // squiggly   ")]
    [InlineData("// squiggly v1")]
    [InlineData("\t// squiggly\tv1 ")]
    public void Bare_header_is_ready(string line)
    {
        Assert.Equal(HeaderKind.Ready, Header.Parse(line).Kind);
    }

    [Theory]
    [InlineData("// squiggly", true)]
    [InlineData("  // squiggly v1 game=snake", true)]
    [InlineData("// squigglyness", false)]
    [InlineData("/// squiggly", false)]
    [InlineData("var squiggly = 1; // squiggly", false)]
    [InlineData("", false)]
    public void Detects_header_lines(string line, bool expected)
    {
        Assert.Equal(expected, Header.IsHeader(line));
    }

    [Fact]
    public void Serializes_canonically_in_the_documented_order()
    {
        var state = new SnakeState
        {
            Seed = 8812,
            Tick = 37,
            Status = GameStatus.Playing,
            Score = 4,
            Dir = Direction.Right,
            Head = new Point(10, 5),
            Tail = "LL",
            Food = new Point(14, 7),
        };

        Assert.Equal(
            "// squiggly v1 game=snake seed=8812 tick=37 status=playing score=4 dir=R head=10,5 tail=LL food=14,7",
            Header.Serialize(SnakeGame.Instance, state));
    }

    [Fact]
    public void Accepts_keys_in_any_order()
    {
        var result = Header.Parse("// squiggly v1 food=14,7 tail=LL head=10,5 dir=R score=4 status=playing tick=37 seed=8812 game=snake");
        Assert.Equal(HeaderKind.Game, result.Kind);
        Assert.Equal(new Point(14, 7), ((SnakeState)result.State!).Food);
    }

    [Theory]
    [InlineData("// squiggly v2 game=snake", "version")]
    [InlineData("// squiggly v1 game", "game")]
    [InlineData("// squiggly v1 seed=1", "game")]
    [InlineData("// squiggly v1 game=pong", "game")]
    [InlineData("// squiggly v1 game=snake game=snake", "game")]
    [InlineData("// squiggly v1 game=snake seed=1 tick=0 status=playing score=0 dir=R head=10,5 tail=LL", "food")]
    [InlineData("// squiggly v1 game=snake seed=1 tick=0 status=playing score=0 dir=R head=10,5 tail=LL food=1,1 lives=3", "lives")]
    [InlineData("// squiggly v1 game=snake seed=-1 tick=0 status=playing score=0 dir=R head=10,5 tail=LL food=1,1", "seed")]
    [InlineData("// squiggly v1 game=snake seed=1 tick=07 status=playing score=0 dir=R head=10,5 tail=LL food=1,1", "tick")]
    [InlineData("// squiggly v1 game=snake seed=1 tick=0 status=dancing score=0 dir=R head=10,5 tail=LL food=1,1", "status")]
    [InlineData("// squiggly v1 game=snake seed=1 tick=0 status=playing score=0 dir=X head=10,5 tail=LL food=1,1", "dir")]
    [InlineData("// squiggly v1 game=snake seed=1 tick=0 status=playing score=0 dir=R head=20,5 tail=LL food=1,1", "head")]
    [InlineData("// squiggly v1 game=snake seed=1 tick=0 status=playing score=0 dir=R head=1,5 tail=LLL food=1,1", "tail")]
    [InlineData("// squiggly v1 game=tetris seed=1 tick=0 status=playing score=0 piece=Q rot=0 pos=3,0 next=T drawn=2 lines=0 board=0", "piece")]
    [InlineData("// squiggly v1 game=tetris seed=1 tick=0 status=playing score=0 piece=I rot=0 pos=3,0 next=T drawn=2 lines=0 board=0.0", "board")]
    [InlineData("// squiggly v1 game=mines seed=1 tick=0 status=playing score=0 mines=1.0.0.0.0.0.0.0 open=0.0.0.0.0.0.0.0 flags=0.0.0.0.0.0.0.0", "mines")]
    public void Bad_headers_name_the_bad_key(string line, string key)
    {
        var result = Header.Parse(line);
        Assert.Equal(HeaderKind.Error, result.Kind);
        Assert.Equal(key, result.ErrorKey);
        Assert.Contains(key, result.Error);
    }

    [Fact]
    public void Round_trips_every_reachable_state_byte_for_byte()
    {
        foreach (var game in Games.All)
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                foreach (var state in TestPlay.RandomStates(game, seed, 300))
                {
                    var text = Header.Serialize(game, state);
                    var parsed = Header.Parse(text);
                    Assert.True(parsed.Kind == HeaderKind.Game, $"{text}: {parsed.Error}");
                    Assert.Same(game, parsed.Game);
                    Assert.Equal(state, parsed.State);
                    Assert.Equal(text, Header.Serialize(game, parsed.State!));
                }
            }
        }
    }

    [Fact]
    public void Paused_and_over_states_round_trip()
    {
        foreach (var game in Games.All)
        {
            var playing = game.NewGame(5);
            foreach (var status in new[] { GameStatus.Paused, GameStatus.Over })
            {
                var state = playing with { Status = status };
                Assert.Equal(state, Header.Parse(Header.Serialize(game, state)).State);
            }
        }
    }

    [Fact]
    public void Headers_stay_short_enough_to_read()
    {
        var longest = Games.All
            .SelectMany(game => TestPlay.RandomStates(game, 3, 300).Select(state => Header.Serialize(game, state)))
            .Max(text => text.Length);
        Assert.InRange(longest, 1, 260);
    }
}
