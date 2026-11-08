using System.Collections.Generic;

namespace Squiggly.Engine;

/// <summary>What a menu command does.</summary>
public enum CommandKind
{
    NewGame,
    Action,
    Pause,
    Resume,
    Restart,
    PlayAgain,
    SwitchGame,
    ResetGame,
    RepairCanvas,
}

/// <summary>
/// One lightbulb menu entry. <see cref="Key"/> is the stable code-action equivalence key;
/// <see cref="Title"/> is shown to the player, prefixed with "Squiggly: " so entries group together.
/// </summary>
public sealed record Command(CommandKind Kind, string Key, string Title, IGame? Game = null, GameAction? Action = null);

/// <summary>
/// The result of a command. <see cref="Header"/> replaces the header text (without indent).
/// <see cref="CanvasRows"/>, when not null, is the complete canvas the file should end up with;
/// callers rewrite only the rows that differ.
/// </summary>
public sealed record FileEdit(string Header, IReadOnlyList<string>? CanvasRows);

/// <summary>
/// The controls. The menu depends only on the file's state, and applying a command is a pure function
/// of the file, the command and a seed chosen by the caller (used only when a new game starts).
/// </summary>
public static class Controller
{
    private const string Prefix = "Squiggly: ";

    /// <summary>The commands offered on the header line, in menu order.</summary>
    public static IReadOnlyList<Command> Menu(GameFile file)
    {
        var commands = new List<Command>();
        var header = file.Header;
        switch (header.Kind)
        {
            case HeaderKind.Error:
                commands.Add(new Command(CommandKind.ResetGame, "squiggly.reset", Prefix + "Reset game"));
                return commands;
            case HeaderKind.Ready:
                foreach (var game in Games.All)
                {
                    commands.Add(new Command(CommandKind.NewGame, "squiggly.new." + game.Id, Prefix + "New game: " + game.Name, game));
                }

                return commands;
        }

        var current = header.Game!;
        if (!file.CanvasValid)
        {
            commands.Add(new Command(CommandKind.RepairCanvas, "squiggly.repair", Prefix + "Repair canvas", current));
            return commands;
        }

        switch (header.State!.Status)
        {
            case GameStatus.Playing:
                foreach (var action in current.Actions)
                {
                    commands.Add(ActionCommand(current, action));
                }

                commands.Add(new Command(CommandKind.Pause, "squiggly.pause", Prefix + "Pause", current));
                commands.Add(new Command(CommandKind.Restart, "squiggly.restart", Prefix + "Restart", current));
                break;
            case GameStatus.Paused:
                commands.Add(new Command(CommandKind.Resume, "squiggly.resume", Prefix + "Resume", current));
                commands.Add(new Command(CommandKind.Restart, "squiggly.restart", Prefix + "Restart", current));
                break;
            default:
                commands.Add(new Command(CommandKind.PlayAgain, "squiggly.again", Prefix + "Play again", current));
                commands.Add(new Command(CommandKind.SwitchGame, "squiggly.switch", Prefix + "Switch game", current));
                break;
        }

        return commands;
    }

    /// <summary>The commands offered on one board cell (Minesweeper only), in menu order.</summary>
    public static IReadOnlyList<Command> CellMenu(GameFile file, Point cell)
    {
        if (file.Header.Kind != HeaderKind.Game || !file.CanvasValid)
        {
            return System.Array.Empty<Command>();
        }

        var game = file.Header.Game!;
        var commands = new List<Command>();
        foreach (var action in game.CellActions(file.Header.State!, cell))
        {
            commands.Add(ActionCommand(game, action));
        }

        return commands;
    }

    /// <summary>Applies <paramref name="command"/>. <paramref name="seed"/> is used only when a new game starts.</summary>
    public static FileEdit Apply(GameFile file, Command command, uint seed)
    {
        var header = file.Header;
        switch (command.Kind)
        {
            case CommandKind.NewGame:
            case CommandKind.Restart:
            case CommandKind.PlayAgain:
                var game = command.Game!;
                var fresh = game.NewGame(seed);
                return new FileEdit(Header.Serialize(game, fresh), game.Canvas(fresh));
            case CommandKind.SwitchGame:
            case CommandKind.ResetGame:
                return new FileEdit(Header.ReadyLine, null);
            case CommandKind.RepairCanvas:
                return new FileEdit(file.HeaderLine.Trim(), header.Game!.Canvas(header.State!));
            case CommandKind.Pause:
                return Rewrite(file, header.State! with { Status = GameStatus.Paused });
            case CommandKind.Resume:
                return Rewrite(file, header.State! with { Status = GameStatus.Playing });
            default:
                var current = header.Game!;
                var next = current.Step(header.State!, command.Action!);
                return new FileEdit(Header.Serialize(current, next), current.DynamicCanvas ? current.Canvas(next) : null);
        }
    }

    private static Command ActionCommand(IGame game, GameAction action) =>
        new(CommandKind.Action, "squiggly." + game.Id + "." + action.Id, Prefix + action.Title, game, action);

    private static FileEdit Rewrite(GameFile file, GameState state) =>
        new(Header.Serialize(file.Header.Game!, state), null);
}
