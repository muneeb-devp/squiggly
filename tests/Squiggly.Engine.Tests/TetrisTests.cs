using System.Linq;

namespace Squiggly.Engine.Tests;

public class TetrisTests
{
    private static readonly TetrisGame Game = TetrisGame.Instance;

    private static TetrisState Tetris(char piece, int rot, Point pos, BitGrid? board = null) => new()
    {
        Seed = 9,
        Status = GameStatus.Playing,
        Piece = piece,
        Rot = rot,
        Pos = pos,
        Next = 'T',
        Drawn = 2,
        Board = board ?? BitGrid.Empty(10, 20),
    };

    private static BitGrid Board(params (int Row, string Cells)[] rows)
    {
        var board = BitGrid.Empty(10, 20);
        foreach (var (row, cells) in rows)
        {
            for (var x = 0; x < cells.Length; x++)
            {
                if (cells[x] == '#')
                {
                    board = board.With(new Point(x, row), true);
                }
            }
        }

        return board;
    }

    [Fact]
    public void Seven_bag_deals_each_piece_once_per_bag()
    {
        for (uint seed = 0; seed < 50; seed++)
        {
            for (var bag = 0; bag < 5; bag++)
            {
                var pieces = Enumerable.Range(bag * 7, 7).Select(i => TetrisGame.PieceAt(seed, i)).OrderBy(c => c);
                Assert.Equal("IJLOSTZ", string.Concat(pieces));
            }
        }
    }

    [Fact]
    public void Piece_order_depends_on_the_seed()
    {
        var a = string.Concat(Enumerable.Range(0, 14).Select(i => TetrisGame.PieceAt(1, i)));
        var b = string.Concat(Enumerable.Range(0, 14).Select(i => TetrisGame.PieceAt(2, i)));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Every_action_except_hard_drop_applies_gravity()
    {
        var start = Tetris('T', 0, new Point(3, 0));
        Assert.Equal(new Point(2, 1), ((TetrisState)Game.Step(start, TetrisGame.Left)).Pos);
        Assert.Equal(new Point(4, 1), ((TetrisState)Game.Step(start, TetrisGame.Right)).Pos);
        Assert.Equal(new Point(3, 1), ((TetrisState)Game.Step(start, TetrisGame.Rotate)).Pos);
        Assert.Equal(new Point(3, 2), ((TetrisState)Game.Step(start, TetrisGame.SoftDrop)).Pos);
    }

    [Fact]
    public void Hard_drop_locks_at_the_bottom_and_spawns_the_next_piece()
    {
        var state = (TetrisState)Game.Step(Tetris('O', 0, new Point(4, 0)), TetrisGame.HardDrop);
        Assert.True(state.Board[4, 19] && state.Board[5, 19] && state.Board[4, 18] && state.Board[5, 18]);
        Assert.Equal('T', state.Piece);
        Assert.Equal(Tetrominoes.Spawn('T'), state.Pos);
        Assert.Equal(TetrisGame.PieceAt(9, 2), state.Next);
        Assert.Equal(3, state.Drawn);
    }

    [Fact]
    public void Walls_block_movement()
    {
        var state = (TetrisState)Game.Step(Tetris('O', 0, new Point(0, 5)), TetrisGame.Left);
        Assert.Equal(new Point(0, 6), state.Pos);
    }

    [Fact]
    public void Rotation_kicks_off_the_right_wall()
    {
        // Vertical I in the rightmost column: rotating to horizontal only fits after the -1 kick.
        var state = (TetrisState)Game.Step(Tetris('I', 1, new Point(7, 5)), TetrisGame.Rotate);
        Assert.Equal(2, state.Rot);
        Assert.Equal(6, state.Pos.X);
    }

    [Fact]
    public void Rotation_that_cannot_fit_is_skipped()
    {
        var walls = Board(Enumerable.Range(0, 20).Select(y => (y, "####...###")).ToArray());
        var state = (TetrisState)Game.Step(Tetris('I', 1, new Point(3, 0)), TetrisGame.Rotate);
        Assert.Equal(2, state.Rot);
        var blocked = (TetrisState)Game.Step(Tetris('I', 1, new Point(3, 0), walls), TetrisGame.Rotate);
        Assert.Equal(1, blocked.Rot);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 300)]
    [InlineData(3, 500)]
    [InlineData(4, 800)]
    public void Line_clears_score_by_count(int lines, int score)
    {
        // Rows 19 upwards are full except column 0; a vertical I in column 0 fills up to four of them.
        var board = Board(Enumerable.Range(20 - lines, lines).Select(y => (y, ".#########")).ToArray());
        var state = (TetrisState)Game.Step(Tetris('I', 1, new Point(-2, 0), board), TetrisGame.HardDrop);
        Assert.Equal(score, state.Score);
        Assert.Equal(lines, state.Lines);
        Assert.Equal(4 - lines, state.Board.Count());
    }

    [Fact]
    public void Spawn_collision_ends_the_game()
    {
        var full = Board(Enumerable.Range(1, 19).Select(y => (y, "#########.")).ToArray());
        var state = (TetrisState)Game.Step(Tetris('O', 0, new Point(4, 0), full) with { Next = 'O' }, TetrisGame.HardDrop);
        Assert.Equal(GameStatus.Over, state.Status);
        Assert.Contains("GAME OVER", Game.GameOver(state));
    }

    [Fact]
    public void Renders_piece_ghost_and_blocks()
    {
        var frame = Game.Render(Tetris('O', 0, new Point(4, 0), Board((19, "#........."))));
        Assert.Contains(new Cell(4, 0, PaletteColor.Primary, "piece"), frame.Cells);
        Assert.Contains(new Cell(4, 19, PaletteColor.Accent, "ghost"), frame.Cells);
        Assert.Contains(new Cell(0, 19, PaletteColor.Secondary, "block"), frame.Cells);
        Assert.Equal(9, frame.Cells.Count);
    }

    [Fact]
    public void Random_play_always_ends_and_never_overlaps()
    {
        for (uint seed = 1; seed <= 30; seed++)
        {
            var last = TestPlay.RandomStates(Game, seed, 5000).Cast<TetrisState>().Last();
            Assert.Equal(GameStatus.Over, last.Status);
        }
    }

    [Fact]
    public void Replay_is_deterministic()
    {
        var map = new[] { TetrisGame.Left, TetrisGame.Right, TetrisGame.Rotate, TetrisGame.SoftDrop, TetrisGame.HardDrop };
        var moves = "LLRHRRRHURLHLLLLHRRRRRHSSSHUULH"
            .Select(c => map["LRUSH".IndexOf(c)]);
        var final = TestPlay.Replay(Game, 8812, moves);
        Assert.Equal(GoldenTetris, Header.Serialize(Game, final));
    }

    private const string GoldenTetris =
        "// squiggly v1 game=tetris seed=8812 tick=31 status=playing score=0 piece=Z rot=0 pos=3,0 next=L drawn=9 lines=0 board=0.0.0.0.0.0.0.0.0.0.0.0.1c.34.18.10.30.3d0.d9.19f";
}
