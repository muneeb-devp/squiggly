using System.Collections.Generic;
using System.Linq;

namespace Squiggly.Engine.Tests;

public class SnakeTests
{
    private static readonly SnakeGame Game = SnakeGame.Instance;

    private static SnakeState Snake(string head, string tail, string dir = "R", string food = "0,0") => new()
    {
        Seed = 1,
        Status = GameStatus.Playing,
        Dir = Directions.TryParse(dir[0], out var d) ? d : throw new System.ArgumentException(dir),
        Head = Parse(head),
        Tail = tail,
        Food = food == "-" ? null : Parse(food),
    };

    private static Point Parse(string point)
    {
        var parts = point.Split(',');
        return new Point(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    [Fact]
    public void New_game_is_length_three_in_the_middle_heading_right()
    {
        var state = (SnakeState)Game.NewGame(1234);
        Assert.Equal(GameStatus.Playing, state.Status);
        Assert.Equal(new Point(10, 5), state.Head);
        Assert.Equal("LL", state.Tail);
        Assert.Equal(Direction.Right, state.Dir);
        Assert.NotNull(state.Food);
        Assert.DoesNotContain(state.Food!.Value, state.Body());
    }

    [Fact]
    public void Wait_moves_forward_one_cell_and_one_tick()
    {
        var state = (SnakeState)Game.Step(Snake("5,5", "LL"), SnakeGame.Wait);
        Assert.Equal(new Point(6, 5), state.Head);
        Assert.Equal("LL", state.Tail);
        Assert.Equal(1, state.Tick);
    }

    [Fact]
    public void Direction_turns_then_moves()
    {
        var state = (SnakeState)Game.Step(Snake("5,5", "LL"), SnakeGame.Up);
        Assert.Equal(new Point(5, 4), state.Head);
        Assert.Equal("DL", state.Tail);
        Assert.Equal(Direction.Up, state.Dir);
    }

    [Fact]
    public void Turning_back_into_the_neck_is_treated_as_wait()
    {
        var state = (SnakeState)Game.Step(Snake("5,5", "LL"), SnakeGame.Left);
        Assert.Equal(new Point(6, 5), state.Head);
        Assert.Equal(Direction.Right, state.Dir);
        Assert.Equal(GameStatus.Playing, state.Status);
    }

    [Fact]
    public void Hitting_a_wall_ends_the_game()
    {
        var state = (SnakeState)Game.Step(Snake("19,5", "LL"), SnakeGame.Wait);
        Assert.Equal(GameStatus.Over, state.Status);
        Assert.False(state.Won);
        Assert.Equal(new Point(19, 5), state.Head);
    }

    [Fact]
    public void Hitting_the_body_ends_the_game()
    {
        // A 5-long snake curled so that moving up bites its own body at 5,4.
        var state = (SnakeState)Game.Step(Snake("5,5", "LURR", dir: "R"), SnakeGame.Up);
        Assert.Equal(GameStatus.Over, state.Status);
    }

    [Fact]
    public void Moving_into_the_cell_the_tail_is_leaving_is_allowed()
    {
        // A 4-long square loop: the head moves onto the tail's current cell as the tail moves away.
        var state = (SnakeState)Game.Step(Snake("5,5", "LUR", dir: "U"), SnakeGame.Up);
        Assert.Equal(GameStatus.Playing, state.Status);
        Assert.Equal(new Point(5, 4), state.Head);
    }

    [Fact]
    public void Eating_grows_scores_and_respawns_food()
    {
        var state = (SnakeState)Game.Step(Snake("5,5", "LL", food: "6,5"), SnakeGame.Wait);
        Assert.Equal(1, state.Score);
        Assert.Equal("LLL", state.Tail);
        Assert.NotNull(state.Food);
        Assert.DoesNotContain(state.Food!.Value, state.Body());
    }

    [Fact]
    public void Food_never_spawns_on_the_snake()
    {
        for (uint seed = 1; seed <= 200; seed++)
        {
            foreach (var state in TestPlay.RandomStates(Game, seed, 400).Cast<SnakeState>())
            {
                if (state.Food is { } food)
                {
                    Assert.DoesNotContain(food, state.Body());
                }
            }
        }
    }

    [Fact]
    public void Filling_the_board_wins()
    {
        // Serpentine path through all 200 cells; the snake covers the first 199, food is the last.
        var path = new List<Point>();
        for (var y = 0; y < 10; y++)
        {
            for (var i = 0; i < 20; i++)
            {
                path.Add(new Point(y % 2 == 0 ? i : 19 - i, y));
            }
        }

        var tail = "";
        for (var i = 198; i > 0; i--)
        {
            var (from, to) = (path[i], path[i - 1]);
            tail += to.X < from.X ? 'L' : to.X > from.X ? 'R' : to.Y < from.Y ? 'U' : 'D';
        }

        var state = (SnakeState)Game.Step(Snake(path[198].ToString(), tail, dir: "L", food: path[199].ToString()), SnakeGame.Wait);
        Assert.Equal(GameStatus.Over, state.Status);
        Assert.True(state.Won);
        Assert.Null(state.Food);
        Assert.Contains("YOU WIN", Game.GameOver(state));
    }

    [Fact]
    public void Step_ignores_finished_games()
    {
        var over = Snake("5,5", "LL") with { Status = GameStatus.Over };
        Assert.Same(over, Game.Step(over, SnakeGame.Up));
    }

    [Fact]
    public void Keys_map_to_moves_and_menu_moves_map_back_to_keys()
    {
        Assert.Equal(new[] { SnakeGame.Up, SnakeGame.Left, SnakeGame.Down, SnakeGame.Right, SnakeGame.Up }, Game.ParseKeys("wasd W?"));
        var heading = Snake("5,5", "LL", dir: "U");
        Assert.Equal("ddw", Game.AppendKey("dd", heading, SnakeGame.Wait));
        Assert.Equal("dda", Game.AppendKey("dd", heading, SnakeGame.Left));
    }

    [Fact]
    public void Renders_head_body_and_food_in_their_colours()
    {
        var frame = Game.Render(Snake("5,5", "LL", food: "9,9"));
        Assert.Contains(new Cell(5, 5, PaletteColor.Primary, "snake head"), frame.Cells);
        Assert.Contains(new Cell(4, 5, PaletteColor.Secondary, "snake"), frame.Cells);
        Assert.Contains(new Cell(3, 5, PaletteColor.Secondary, "snake"), frame.Cells);
        Assert.Contains(new Cell(9, 9, PaletteColor.Accent, "food"), frame.Cells);
        Assert.Equal(4, frame.Cells.Count);
    }

    [Fact]
    public void Replay_is_deterministic()
    {
        // Golden replay: a fixed list of moves must always produce this exact header.
        var moves = "RRRDDDDLLLLUUWWWRRRRRRRDDDLLLLLLLLLUUUUWWWRRRRRRRRRRDDLLLL"
            .Select(c => c switch { 'U' => SnakeGame.Up, 'D' => SnakeGame.Down, 'L' => SnakeGame.Left, 'R' => SnakeGame.Right, _ => SnakeGame.Wait });
        var final = TestPlay.Replay(Game, 8812, moves);
        Assert.Equal(GoldenSnake, Header.Describe(Game, final));
    }

    [Fact]
    public void Food_chasing_replay_is_deterministic()
    {
        // Golden replay that eats, so food placement from seed and tick is pinned too.
        var state = (SnakeState)Game.NewGame(2026);
        for (var i = 0; i < 300 && state.Status == GameStatus.Playing; i++)
        {
            state = (SnakeState)Game.Step(state, Chase(state));
        }

        Assert.Equal(GoldenChase, Header.Describe(Game, state));
    }

    /// <summary>Greedy deterministic player: head towards the food, avoiding immediate death when it can.</summary>
    private static GameAction Chase(SnakeState state)
    {
        var food = state.Food ?? state.Head;
        var preferred = new List<GameAction>();
        if (food.X < state.Head.X) preferred.Add(SnakeGame.Left);
        if (food.X > state.Head.X) preferred.Add(SnakeGame.Right);
        if (food.Y < state.Head.Y) preferred.Add(SnakeGame.Up);
        if (food.Y > state.Head.Y) preferred.Add(SnakeGame.Down);
        preferred.AddRange(new[] { SnakeGame.Up, SnakeGame.Right, SnakeGame.Down, SnakeGame.Left });
        foreach (var action in preferred)
        {
            if (Game.Step(state, action).Status == GameStatus.Playing)
            {
                return action;
            }
        }

        return SnakeGame.Wait;
    }

    private const string GoldenSnake =
        "game=snake seed=8812 tick=58 status=playing score=0 dir=L head=13,2 tail=RR food=0,2";

    private const string GoldenChase =
        "game=snake seed=2026 tick=247 status=over score=18 dir=L head=13,4 tail=RRRRDLLLLLUURRRRRRDD food=4,5";
}
