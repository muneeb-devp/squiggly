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
    SaveProgress,
}

/// <summary>
/// One lightbulb menu entry. <see cref="Key"/> is the stable code-action equivalence key;
/// <see cref="Title"/> is shown to the player, prefixed with "Squiggly: " so entries group together.
/// </summary>
public sealed record Command(CommandKind Kind, string Key, string Title, IGame? Game = null, GameAction? Action = null);

/// <summary>
/// The result of a command. <see cref="Header"/> replaces the header text (without indent).
/// <see cref="CanvasRows"/>, when not null, is the complete canvas the file should end up with.
/// <see cref="Keys"/>, when not null, is the new content of the <c>// keys:</c> line (empty after the
/// keys are folded into the header). Callers rewrite only the lines that differ.
/// </summary>
public sealed record FileEdit(string Header, IReadOnlyList<string>? CanvasRows, string? Keys = null);

/// <summary>
/// The controls. The menu depends only on the file's state, and applying a command is a pure function
/// of the file, the command and a seed chosen by the caller (used only when a new game starts).
/// Commands act on <see cref="GameFile.State"/>, which includes any typed keys, and fold those keys into
/// the new header, so the keys line is cleared.
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

        switch (file.State!.Status)
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

        if (file.Keys.Length > 0)
        {
            commands.Add(new Command(CommandKind.SaveProgress, "squiggly.save", Prefix + "Save progress (move typed keys into the header)", current));
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
        foreach (var action in game.CellActions(file.State!, cell))
        {
            commands.Add(ActionCommand(game, action));
        }

        return commands;
    }

    /// <summary>Applies <paramref name="command"/>. <paramref name="seed"/> is used only when a new game starts.</summary>
    public static FileEdit Apply(GameFile file, Command command, uint seed)
    {
        switch (command.Kind)
        {
            case CommandKind.NewGame:
            case CommandKind.Restart:
            case CommandKind.PlayAgain:
                var game = command.Game!;
                var fresh = game.NewGame(seed);
                return new FileEdit(Header.Serialize(game, fresh), game.Canvas(fresh), KeysFor(game, ""));
            case CommandKind.SwitchGame:
            case CommandKind.ResetGame:
                return new FileEdit(Header.ReadyLine, null);
            case CommandKind.RepairCanvas:
                // Keep the header and any typed keys; only the canvas (and a missing keys line) is rebuilt.
                var damaged = file.Header.Game!;
                return new FileEdit(file.HeaderLine.Trim(), damaged.Canvas(file.Header.State!), KeysFor(damaged, file.Keys));
            case CommandKind.Pause:
                return Rewrite(file, file.State! with { Status = GameStatus.Paused });
            case CommandKind.Resume:
                return Rewrite(file, file.State! with { Status = GameStatus.Playing });
            case CommandKind.SaveProgress:
                return Rewrite(file, file.State!);
            default:
                var current = file.Header.Game!;
                var next = current.Step(file.State!, command.Action!);
                return new FileEdit(Header.Serialize(current, next), current.DynamicCanvas ? current.Canvas(next) : null, KeysFor(current, ""));
        }
    }

    private static Command ActionCommand(IGame game, GameAction action) =>
        new(CommandKind.Action, "squiggly." + game.Id + "." + action.Id, Prefix + action.Title, game, action);

    private static FileEdit Rewrite(GameFile file, GameState state) =>
        new(Header.Serialize(file.Header.Game!, state), null, KeysFor(file.Header.Game!, ""));

    /// <summary>Games with key controls always get a keys line; others never do.</summary>
    private static string? KeysFor(IGame game, string keys) => game.KeyControls ? keys : null;
}
