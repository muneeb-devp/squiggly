# Phase 0 spike findings

Branch: `spike/feasibility`. Throwaway code; nothing here is kept after the brief is updated.

## How to run the spike

1. Open `Squiggly.slnx` (or the `samples/Arcade` folder) in the IDE.
2. Build once so the analyzer and code-fix DLLs exist. The build **fails on purpose**: SQ0001 pixels are errors.
3. Open `samples/Arcade/Game.cs`.
4. Caret on line 1 (the `// squiggly` header), press Ctrl+. (Rider: Alt+Enter), pick **Squiggly: Tick +1**. Repeat a few times.
5. Caret on the `// controls` line, open the lightbulb, check the same two actions appear there.
6. Open `samples/Arcade/Ordinary.cs` and confirm it has no Squiggly diagnostics.

After editing analyzer code, VS and Rider usually need a restart to load the new DLL.

### What Game.cs shows

Canvas rows 1-6 use `·` (U+00B7); rows 7-12 repeat the same patterns with ASCII `.`.

| Row (mod 6) | Pattern | IDs |
|---|---|---|
| 1 | 4-cell bar that moves right one cell per tick | SQ0001 (Error) |
| 2 | 6 adjacent cells | SQ0002 (Warning) |
| 3 | 3 isolated single cells | SQ0003 (Info) |
| 4 | 6 cells, faded | SQ0004 (Hidden + Unnecessary) |
| 5 | 6 cells, faded | SQ0005 (Info + Unnecessary) |
| 6 | alternating cells | SQ0001 / SQ0002 |

Header line: SQ0010 HUD (Info, "Spike, tick N") and SQ0100 controller (Hidden).
`// controls` line: SQ0101 controller (Info), the visible fallback.

## Results

Fill one column per IDE. Record the IDE version and theme used.

| Question | Visual Studio 2022 | VS Code + C# Dev Kit | Rider |
|---|---|---|---|
| IDE version / theme | untested | C# Dev Kit, dark theme | untested |
| Analyzer loads (built against Roslyn 4.8.0)? | | Yes | |
| Squiggles render on 2-char cells inside comments? | | Yes | |
| Adjacent cells read as one continuous line? | | Yes | |
| Neighbouring SQ0001/SQ0002 cells distinguishable? | | Yes (red/yellow alternate visibly) | |
| `·` vs `.`: which renders squiggles better? | | `.` (owner's call) | |
| Error colour | | Red squiggle | |
| Warning colour | | Yellow squiggle | |
| Info colour / style | | Small grey dots under the cell, not a blue squiggle | |
| SQ0004 (Hidden + Unnecessary) fades text? | | No visible effect | |
| SQ0005 (Info + Unnecessary) fades text? | | No visible effect | |
| Lightbulb offered from hidden SQ0100 on header? | | Yes | |
| Lightbulb offered from Info SQ0101 on `// controls`? | | | |
| Caret stays on the header after a fix? | | Yes | |
| Can press Ctrl+. again immediately, without moving? | | Yes | |
| Fix to redrawn frame, approx. time | | | |
| Preview pane matches applied change? | | | |
| `Ordinary.cs` clean? | | Yes | |
| Error List / Problems panel usable with ~40 pixels? | | | |

Screenshots: `assets/spike/<ide>-*.png`. VS Code: [vscode-dark-tick23.png](../assets/spike/vscode-dark-tick23.png).

Blank cells in the VS Code column were not checked. Visual Studio 2022 and Rider are still to be tested before Phase 3 (rendering) is signed off.

## Brief changes required

Applied to docs/BRIEF.md (kept local, not in git):

- Fill character: switched from `·` to ASCII `.`.
- Palette: VS Code verified as error = red squiggle, warning = yellow squiggle, info = grey dots.

Decided from VS Code, to re-check in VS 2022 and Rider:

- Controller placement: keep the hidden SQ0100 on the header line. The `// controls` fallback is not needed in VS Code.
- Roslyn pin: keep 4.8.0.

Open:

- Ghost colour for Tetris: the `Unnecessary` tag shows nothing in VS Code. Unless VS or Rider fade it, Tetris ships without a ghost piece (or draws it as SQ0003 info dots).

## Notes from the command line (macOS, .NET SDK 10.0.302)

- Analyzer and code fix build against Microsoft.CodeAnalysis 4.8.0 with Microsoft.CodeAnalysis.Analyzers 3.3.4 and `EnforceExtendedAnalyzerRules`, with no analyzer-authoring warnings (RS2008 release tracking suppressed for the spike only).
- `dotnet build samples/Arcade` reports SQ0001 errors and SQ0002 warnings at the expected columns: cell 0 at column 4, each cell 2 characters wide. Info and Hidden diagnostics do not appear in CLI output, as expected.
- `Ordinary.cs` mentions "squiggly" in code and has `// squiggly` on line 6, just past the 5-line header window. It must show zero diagnostics in the IDE (the CLI cannot show this, since Info/Hidden diagnostics are not printed).
