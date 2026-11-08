using System.Collections.Generic;

namespace Squiggly.Engine.Tests;

/// <summary>Drives games with pseudo-random input to reach many different states.</summary>
internal static class TestPlay
{
    /// <summary>Plays up to <paramref name="moves"/> random actions and yields every state on the way, including the first.</summary>
    public static IEnumerable<GameState> RandomStates(IGame game, uint seed, int moves)
    {
        var random = new Prng(seed * 7919UL + 17);
        var state = game.NewGame(seed);
        yield return state;
        for (var i = 0; i < moves && state.Status == GameStatus.Playing; i++)
        {
            state = game.Step(state, PickAction(game, state, ref random));
            yield return state;
        }
    }

    public static GameAction PickAction(IGame game, GameState state, ref Prng random)
    {
        if (game.Actions.Count > 0)
        {
            return game.Actions[random.NextBelow(game.Actions.Count)];
        }

        // Cell-input games: pick a random cell that still offers actions.
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var cell = new Point(random.NextBelow(game.Width), random.NextBelow(game.Height));
            var actions = game.CellActions(state, cell);
            if (actions.Count > 0)
            {
                return actions[random.NextBelow(actions.Count)];
            }
        }

        return new GameAction("none", "None");
    }

    /// <summary>Applies actions in order and returns the final state.</summary>
    public static GameState Replay(IGame game, uint seed, IEnumerable<GameAction> actions)
    {
        var state = game.NewGame(seed);
        foreach (var action in actions)
        {
            state = game.Step(state, action);
        }

        return state;
    }
}
