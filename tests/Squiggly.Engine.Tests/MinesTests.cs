using System.Linq;

namespace Squiggly.Engine.Tests;

public class MinesTests
{
    private static readonly MinesGame Game = MinesGame.Instance;

    [Fact]
    public void First_reveal_is_always_safe()
    {
        for (uint seed = 0; seed < 300; seed++)
        {
            var cell = new Point((int)(seed % 16), (int)(seed % 8));
            var state = (MinesState)Game.Step(Game.NewGame(seed), MinesGame.Reveal(cell));
            Assert.NotNull(state.Mines);
            Assert.False(state.Mines![cell]);
            Assert.Equal(16, state.Mines.Count());
            Assert.False(state.Lost);
            Assert.True(state.Open[cell]);
        }
    }

    [Fact]
    public void Reveal_floods_through_empty_cells()
    {
        var mines = Layout((15, 7), (14, 7), (15, 6), (0, 7), (1, 7), (2, 7), (3, 7), (4, 7), (5, 7), (6, 7), (7, 7), (8, 7), (9, 7), (10, 7), (11, 7), (12, 7));
        var state = (MinesState)Game.Step(new MinesState { Seed = 1, Status = GameStatus.Playing, Mines = mines }, MinesGame.Reveal(new Point(0, 0)));
        Assert.True(state.Open[7, 0]);
        Assert.True(state.Open[15, 0]);
        Assert.True(state.Open[0, 6]);
        Assert.False(state.Open[0, 7]);
        Assert.Equal(state.Open.Count(), state.Score);
    }

    [Fact]
    public void Revealing_a_mine_loses()
    {
        var mines = Layout(Enumerable.Range(0, 16).Select(x => (x, 7)).ToArray());
        var state = (MinesState)Game.Step(new MinesState { Seed = 1, Status = GameStatus.Playing, Mines = mines }, MinesGame.Reveal(new Point(3, 7)));
        Assert.Equal(GameStatus.Over, state.Status);
        Assert.True(state.Lost);
        Assert.Contains(new Cell(3, 7, PaletteColor.Primary, "mine"), Game.Render(state).Cells);
        Assert.Contains("BOOM", Game.GameOver(state));
    }

    [Fact]
    public void Clearing_every_safe_cell_wins()
    {
        var mines = Layout(Enumerable.Range(0, 16).Select(x => (x, 7)).ToArray());
        var state = (MinesState)Game.Step(new MinesState { Seed = 1, Status = GameStatus.Playing, Mines = mines }, MinesGame.Reveal(new Point(0, 0)));
        Assert.Equal(GameStatus.Over, state.Status);
        Assert.False(state.Lost);
        Assert.Equal(16 * 8 - 16, state.Score);
        Assert.Contains("YOU WIN", Game.GameOver(state));
    }

    [Fact]
    public void Flags_toggle_and_block_reveal()
    {
        var cell = new Point(2, 2);
        var start = Game.NewGame(3);
        Assert.Equal(new[] { "reveal", "flag" }, Game.CellActions(start, cell).Select(a => a.Id));

        var flagged = (MinesState)Game.Step(start, MinesGame.Flag(cell));
        Assert.True(flagged.Flags[cell]);
        Assert.Equal(new[] { "unflag" }, Game.CellActions(flagged, cell).Select(a => a.Id));
        Assert.Same(flagged, Game.Step(flagged, MinesGame.Reveal(cell)));

        var unflagged = (MinesState)Game.Step(flagged, MinesGame.Unflag(cell));
        Assert.False(unflagged.Flags[cell]);
        Assert.Equal(2, unflagged.Tick);
    }

    [Fact]
    public void Canvas_shows_counts_for_revealed_cells()
    {
        var mines = Layout(Enumerable.Range(0, 16).Select(x => (x, 7)).ToArray());
        var state = (MinesState)Game.Step(new MinesState { Seed = 1, Status = GameStatus.Playing, Mines = mines }, MinesGame.Reveal(new Point(0, 0)));
        var canvas = Game.Canvas(state);
        Assert.Equal(new string(' ', 32), canvas[0]);
        Assert.Equal(" 2" + string.Concat(Enumerable.Repeat(" 3", 14)) + " 2", canvas[6]);
        Assert.Equal(new string('.', 32), canvas[7]);
    }

    [Fact]
    public void Covered_cells_render_as_input_targets()
    {
        var frame = Game.Render(Game.NewGame(1));
        Assert.Equal(128, frame.Cells.Count);
        Assert.All(frame.Cells, cell => Assert.Equal(PaletteColor.Covered, cell.Color));
    }

    [Fact]
    public void Replay_is_deterministic()
    {
        var moves = new[] { (3, 3), (10, 2), (0, 7), (15, 0), (7, 5), (12, 6) }
            .Select(c => MinesGame.Reveal(new Point(c.Item1, c.Item2)));
        var final = TestPlay.Replay(Game, 8812, moves);
        Assert.Equal(GoldenMines, Header.Serialize(Game, final));
    }

    private static BitGrid Layout(params (int X, int Y)[] cells)
    {
        var grid = BitGrid.Empty(16, 8);
        foreach (var (x, y) in cells)
        {
            grid = grid.With(new Point(x, y), true);
        }

        return grid;
    }

    private const string GoldenMines =
        "// squiggly v1 game=mines seed=8812 tick=2 status=over score=1 mines=606.4020.4cc.20.100.9800.0.0 open=0.0.400.8.0.0.0.0 flags=0.0.0.0.0.0.0.0";
}
