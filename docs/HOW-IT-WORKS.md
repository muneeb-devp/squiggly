# How Squiggly works

This page explains how Squiggly puts a playable game inside your editor's error squiggles. It's written for people who have never built a Roslyn analyzer: if you know a bit of C# and have seen a red squiggle before, you have everything you need.

## The idea in one paragraph

Your editor already knows how to draw coloured squiggles under text, and how to show a lightbulb menu of quick fixes. Squiggly uses those two things as a screen and a controller. A small plugin reads a special comment block in a file, works out what the game board should look like, and asks the editor to underline the right characters in the right colours. Each underlined patch of comment is one "pixel". When you type a move, the file changes, the plugin runs again, and a new frame appears.

## Background: squiggles, analyzers and lightbulbs

### Diagnostics are the squiggles

When the C# compiler finds a problem, it reports a **diagnostic**: a message, a severity, and a location in the file. Your editor draws each diagnostic as a squiggle under that location and lists it in the Problems or Error List panel. The **severity** decides the look:

| Severity | Typical look (VS Code dark theme) |
|---|---|
| Error | red squiggle |
| Warning | yellow squiggle |
| Info | faint grey dots |

That gives Squiggly three colours to draw with.

### Analyzers add their own diagnostics

The C# compiler is called **Roslyn**, and it lets anyone plug in extra checks called **analyzers**. You've probably used some without noticing: the warning that says a variable is never used comes from one. An analyzer is a C# class that Roslyn calls with your code, and it can report any diagnostics it likes, at any location.

Analyzers ship as NuGet packages. When you `dotnet add package Squiggly`, the analyzer DLL lands in your project, and every editor that uses Roslyn (Visual Studio, VS Code with C# Dev Kit, Rider) loads it. That's why one package works in all three, without a separate plugin for each.

Two facts about analyzers shape the whole design:

1. **The editor re-runs them every time the file changes.** Type a character, and within a moment every analyzer looks at the file again. That's our frame rate.
2. **They only see text.** An analyzer can't see the keyboard, the mouse or the clock. It gets the current contents of the file and nothing else.

### Code fixes are the lightbulb menu

When you put the caret on a diagnostic and press <kbd>Ctrl</kbd>+<kbd>.</kbd> (or <kbd>Alt</kbd>+<kbd>Enter</kbd> in Rider), the editor shows a lightbulb menu of **code fixes**, such as "Add missing using". A code fix is another C# class, a `CodeFixProvider`. It says which diagnostic IDs it can fix, and for each one it offers menu entries that each describe an edit to the file.

Squiggly uses code fixes as buttons: "New game: Snake", "Restart", "Play again", and the Minesweeper cell actions are all code fixes. Choosing one just edits the text, and the analyzer then draws the new state.

## The game file

Everything Squiggly knows is written in the file itself, as comments, so the code still compiles. A Snake game looks like this:

```
// squiggly v1 game=snake seed=8812
//|........................................|
//|........................................|
//|........................................|
//|........................................|
//|........................................|
//|........................................|
//|........................................|
//|........................................|
//|........................................|
//|........................................|
// keys: ddwwaas
```

It has three parts:

- **The header** (`// squiggly v1 game=snake seed=8812`) says which game this is and the **seed**, a number that fixes all the game's randomness: where food appears, which Tetris piece comes next, where the mines are.
- **The canvas** is the drawing surface: one comment line per board row. Each board cell is two dots wide, so Snake's 20-cell row is 40 dots. The dots never change during play. They're just characters for the squiggles to sit under.
- **The keys line** (`// keys: ddwwaas`) records every move you've made. Typing at the end of it is how you play.

## One keystroke, start to finish

Say you're playing Snake and you type `w` at the end of the keys line.

1. **The file changes.** The keys line now reads `// keys: ddwwaasw`.
2. **The editor re-runs the analyzers**, including Squiggly's.
3. **Squiggly looks for its header.** It checks only the first five lines for `// squiggly`. In every ordinary file it finds nothing and stops right there, in well under a microsecond.
4. **It reads the header**: Snake, seed 8812.
5. **It starts a fresh game from the seed**, then **replays every key**: `d`, `d`, `w`, `w`, `a`, `a`, `s`, and now `w`. Each key is one step of the game rules. Letters that aren't moves are ignored.
6. **It renders the final state** into a list of lit cells: the head, the body, the food.
7. **It reports one diagnostic per lit cell.** Each one covers the two dots of that cell, with a severity that picks the colour: Error (red) for the head, Warning (yellow) for the body, Info (grey dots) for the food.
8. **It reports one more diagnostic on the header line**, an Info, with the score, for example "Snake, score 2, length 5, moves 8". That's the HUD, and it's also where the lightbulb menu lives.
9. **The editor draws the squiggles.** You see the snake move up.

Here is the core of the analyzer, slightly simplified from [SquigglyAnalyzer.cs](../src/Squiggly.Analyzers/SquigglyAnalyzer.cs):

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SquigglyAnalyzer : DiagnosticAnalyzer
{
    public override void Initialize(AnalysisContext context)
    {
        // Ask Roslyn to call Analyze for every file.
        context.RegisterSyntaxTreeAction(Analyze);
    }

    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var text = context.Tree.GetText();
        var document = SquigglyDocument.Locate(text);   // look for "// squiggly" in the first 5 lines
        if (document is null) return;                   // an ordinary file: do nothing

        foreach (var mark in document.Read().Marks)     // replay the keys, render, list the lit cells
        {
            var location = Location.Create(context.Tree, document.SpanOf(mark));
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.For(mark.Kind), location, mark.Message));
        }
    }
}
```

## Why replay every move?

It might seem wasteful to replay the whole game on every keystroke rather than just applying the newest move. The reason is that **an analyzer isn't allowed to remember anything**. Roslyn may run it on several threads at once, throw it away and reload it, or run it on an older copy of the file. If Squiggly kept the game in memory, the board would soon drift out of sync with the text. So the file is the only source of truth, and every frame is worked out from scratch:

```
frame = Render(Replay(NewGame(seed), keys))
```

This has some nice side effects:

- **The same file always shows the same board,** on any machine. That makes the game easy to test: the test suite checks exact boards for known seeds and keys.
- **The keys line is a save file.** Commit it, send it in a pull request, or reopen it next week.
- **Undo works for free.** <kbd>Ctrl</kbd>+<kbd>Z</kbd> removes the last key, and the board goes back a step.
- **Cheating is easy too.** Delete the move that killed you.

It's also fast enough: replaying 2,000 Snake moves takes about a quarter of a millisecond. See the [benchmarks](../README.md#benchmarks).

### Randomness that repeats

Games need randomness, but if the food appeared somewhere different every time the analyzer ran, the board would flicker. So Squiggly never uses `System.Random` or the clock. It uses a small, fully specified random number generator ([SplitMix64](../src/Squiggly.Engine/Random/Prng.cs)), fed with the seed and the current move number. "Seed 8812, move 40" always places the food in the same cell, on every computer and every .NET version.

## Starting a game: the lightbulb in action

A brand new file contains just `// squiggly`. The analyzer sees a header with no game, and puts one Info diagnostic on it saying "Squiggly is ready".

When you open the lightbulb on that line, the editor asks Squiggly's `CodeFixProvider` what it can offer for that diagnostic. It answers with one entry per game: "New game: Snake", "New game: Tetris", "New game: Minesweeper". Each entry is a ready-made edit: replace the header with `// squiggly v1 game=snake seed=<a new random number>`, then insert an empty canvas and an empty keys line.

Picking an entry applies the edit, the file changes, and the analyzer draws the opening frame. From then on you play by typing. The same menu, on the header line, offers **Restart** while you play, and **Play again** and **Switch game** after a game over.

## Minesweeper is a little different

In Minesweeper you pick a cell instead of a direction, so a key per move doesn't work. Instead every covered cell has its own grey-dot diagnostic, and the lightbulb on a cell offers **Reveal** and **Flag**. Choosing one writes a short token onto the keys line, such as `r5,3` (reveal column 5, row 3) or `f2,2` (flag column 2, row 2), and also writes the revealed numbers into the canvas so you can read them.

## Playing by an analyzer's rules

Analyzers run inside your editor and your build, so they have strict rules. Each one explains a limitation of the game:

| Rule | What it means for Squiggly |
|---|---|
| Analyzers only run when the text changes. There are no timers. | The game is turn-based: one key, one step. Snake waits for you. |
| Analyzers only see text, not key presses. | Arrow keys can't steer, because they move the caret without changing the text. You type letters instead. |
| Analyzers must be fast, because they run on every file. | The header check reads at most five lines and allocates no memory, so ordinary files cost almost nothing. |
| An analyzer that crashes gets switched off by the editor. | Every error is caught and shown as a warning on the header (SQ9000) instead. |
| Error diagnostics fail the build. | A game in progress turns `dotnet build` red. Keep games in a separate project, or lower the severity in `.editorconfig` (see [Build safety](../README.md#build-safety)). |
| The editor picks the colours. | Squiggly chooses a severity, and your theme decides what red, yellow and grey look like. |

## Where the code lives

| Folder | What it does |
|---|---|
| [src/Squiggly.Engine](../src/Squiggly.Engine) | The games themselves: rules, rendering, the header and keys line, the random number generator. It's plain C# with no Roslyn code, so it's easy to test. |
| [src/Squiggly.Analyzers](../src/Squiggly.Analyzers) | The analyzer: finds the game in the file and turns the engine's lit cells into diagnostics. The engine is compiled into this DLL. |
| [src/Squiggly.CodeFixes](../src/Squiggly.CodeFixes) | The lightbulb menu: turns the engine's commands into code fixes. Roslyn requires code fixes to live in a separate DLL from analyzers. |
| [src/Squiggly.Package](../src/Squiggly.Package) | Packs both DLLs into the NuGet package, in the `analyzers` folder where the compiler looks for them. |
| [tests](../tests) | Engine tests (game rules, exact boards for known seeds) and analyzer tests (squiggle positions and code fix edits). |

If you want to add a game, you mostly work in the engine: implement the `IGame` interface (board size, a new game from a seed, one step per move, and what to draw), and add it to the list in `Games.All`. The analyzer and code fixes pick it up automatically.

## Write your own analyzer

Squiggly is a silly use of a serious tool. The same pieces power real code-quality checks, and Microsoft's tutorial walks through building one from scratch:

- [Tutorial: Write your first analyzer and code fix](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix)
- [The .NET Compiler Platform SDK (Roslyn) overview](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/)
