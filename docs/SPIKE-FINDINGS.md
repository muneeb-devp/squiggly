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
| IDE version / theme | | | |
| Analyzer loads (built against Roslyn 4.8.0)? | | | |
| Squiggles render on 2-char cells inside comments? | | | |
| Adjacent cells read as one continuous line? | | | |
| Neighbouring SQ0001/SQ0002 cells distinguishable? | | | |
| `·` vs `.`: which renders squiggles better? | | | |
| Error colour | | | |
| Warning colour | | | |
| Info colour / style | | | |
| SQ0004 (Hidden + Unnecessary) fades text? | | | |
| SQ0005 (Info + Unnecessary) fades text? | | | |
| Lightbulb offered from hidden SQ0100 on header? | | | |
| Lightbulb offered from Info SQ0101 on `// controls`? | | | |
| Caret stays on the header after a fix? | | | |
| Can press Ctrl+. again immediately, without moving? | | | |
| Fix to redrawn frame, approx. time | | | |
| Preview pane matches applied change? | | | |
| `Ordinary.cs` clean? | | | |
| Error List / Problems panel usable with ~40 pixels? | | | |

Screenshots: `assets/spike/<ide>-*.png`.

## Brief changes required

To be agreed after the results are in. Candidates the results decide:

- Fill character: keep `·` or switch to `.`.
- Controller placement: hidden diagnostic on the header, or a visible one on a dedicated `// controls` line.
- Ghost colour for Tetris: which severity + Unnecessary combination fades, if any.
- Palette table per IDE for the README.
- Roslyn pin: keep 4.8.0 or change it.

## Notes from the command line (macOS, .NET SDK 10.0.302)

- Analyzer and code fix build against Microsoft.CodeAnalysis 4.8.0 with Microsoft.CodeAnalysis.Analyzers 3.3.4 and `EnforceExtendedAnalyzerRules`, with no analyzer-authoring warnings (RS2008 release tracking suppressed for the spike only).
- `dotnet build samples/Arcade` reports SQ0001 errors and SQ0002 warnings at the expected columns: cell 0 at column 4, each cell 2 characters wide. Info and Hidden diagnostics do not appear in CLI output, as expected.
- `Ordinary.cs` mentions "squiggly" in code and has `// squiggly` on line 6, just past the 5-line header window. It must show zero diagnostics in the IDE (the CLI cannot show this, since Info/Hidden diagnostics are not printed).
