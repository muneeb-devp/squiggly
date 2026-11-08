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
    public void Header_holds_only_the_game_and_its_seed()
    {
        Assert.Equal("// squiggly v1 game=snake seed=8812", Header.Serialize(SnakeGame.Instance, 8812));
    }

    [Fact]
    public void Parses_game_and_seed_in_any_order()
    {
        var result = Header.Parse("// squiggly v1 seed=4294967295 game=tetris");
        Assert.Equal(HeaderKind.Game, result.Kind);
        Assert.Same(TetrisGame.Instance, result.Game);
        Assert.Equal(4294967295u, result.Seed);
    }

    [Fact]
    public void Serialize_round_trips_for_every_game()
    {
        foreach (var game in Games.All)
        {
            foreach (var seed in new uint[] { 0, 1, 8812, uint.MaxValue })
            {
                var parsed = Header.Parse(Header.Serialize(game, seed));
                Assert.Same(game, parsed.Game);
                Assert.Equal(seed, parsed.Seed);
            }
        }
    }

    [Theory]
    [InlineData("// squiggly v2 game=snake seed=1", "version")]
    [InlineData("// squiggly v1 game", "game")]
    [InlineData("// squiggly v1 seed=1", "game")]
    [InlineData("// squiggly v1 game=pong seed=1", "game")]
    [InlineData("// squiggly v1 game=snake game=snake seed=1", "game")]
    [InlineData("// squiggly v1 game=snake", "seed")]
    [InlineData("// squiggly v1 game=snake seed=-1", "seed")]
    [InlineData("// squiggly v1 game=snake seed=007", "seed")]
    [InlineData("// squiggly v1 game=snake seed=4294967296", "seed")]
    [InlineData("// squiggly v1 game=snake seed=1 dir=R", "dir")]
    public void Bad_headers_name_the_bad_key(string line, string key)
    {
        var result = Header.Parse(line);
        Assert.Equal(HeaderKind.Error, result.Kind);
        Assert.Equal(key, result.ErrorKey);
        Assert.Contains(key, result.Error);
    }

    [Fact]
    public void Describe_writes_a_canonical_snapshot()
    {
        Assert.Equal(
            "game=snake seed=8812 tick=0 status=playing score=0 dir=R head=10,5 tail=LL food=0,2",
            Header.Describe(SnakeGame.Instance, SnakeGame.Instance.NewGame(8812)));
    }

    [Fact]
    public void Recorded_keys_replay_to_the_same_state_for_every_game()
    {
        // The keys line is the save game: whatever moves are made, writing them down with AppendKey and
        // replaying them with ParseKeys must reproduce the exact same state.
        foreach (var game in Games.All)
        {
            for (uint seed = 1; seed <= 25; seed++)
            {
                var random = new Prng(seed);
                var state = game.NewGame(seed);
                var keys = "";
                for (var move = 0; move < 300 && state.Status == GameStatus.Playing; move++)
                {
                    var action = TestPlay.PickAction(game, state, ref random);
                    keys = game.AppendKey(keys, state, action);
                    state = game.Step(state, action);
                    Assert.Equal(state, GameFile.Replay(game, game.NewGame(seed), keys));
                }
            }
        }
    }
}
