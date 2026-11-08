# Release checklist

Run this before every release. Claude Code can't open an IDE, so the IDE checks are manual.

## One-time setup

- [ ] Make the GitHub repository public (Settings → General → Danger Zone → Change visibility).
- [ ] On nuget.org, sign in, open **Trusted Publishing**, and add a policy: repository owner `muneeb-devp`, repository `squiggly`, workflow file `release.yml`.
- [ ] In the GitHub repository, add the secret `NUGET_USER` with your nuget.org account name (Settings → Secrets and variables → Actions).
- [ ] Optional: protect `main` so CI must pass before merging.

If Trusted Publishing is not available for the account, replace the "NuGet login" step in `.github/workflows/release.yml` with a `NUGET_API_KEY` repository secret, and pass that secret to `dotnet nuget push` instead.

## 1. Automated checks

- [ ] CI is green on `main` for both ubuntu-latest and windows-latest.
- [ ] Locally: `dotnet build -c Release && dotnet test -c Release` passes.

## 2. Pack to a local feed

```sh
eng/pack-local.sh ~/ArcadeTest            # macOS / Linux
eng/pack-local.ps1 -Playground C:\ArcadeTest   # Windows
```

This packs the real package with a unique version and creates (or updates) a console project that uses it, with a ready-state `Game.cs`. It tests the package, not project references.

## 3. Manual IDE checks

Do this in each IDE. Close and reopen the IDE first, since IDEs cache analyzer DLLs.

| Step | Visual Studio 2022 | VS Code + C# Dev Kit | Rider |
|---|---|---|---|
| `Game.cs` shows the "Squiggly is ready" hint on line 1 | [ ] | [ ] | [ ] |
| Lightbulb on line 1 lists New game: Snake, Tetris, Minesweeper | [ ] | [ ] | [ ] |
| Start Snake: canvas appears, snake and food are drawn | [ ] | [ ] | [ ] |
| Click at the end of `// keys:` and type 20 moves with w a s d; the board redraws as you type | [ ] | [ ] | [ ] |
| Hold `d`: the snake keeps moving until you let go | [ ] | [ ] | [ ] |
| A move from the lightbulb menu is appended to the keys line; the caret stays on line 1 | [ ] | [ ] | [ ] |
| The preview matches what gets applied | [ ] | [ ] | [ ] |
| Lose (run into a wall): GAME OVER appears on line 1 | [ ] | [ ] | [ ] |
| Play again works; Switch game returns to the ready menu | [ ] | [ ] | [ ] |
| Tetris: start, then type a d w s x on the keys line to move, rotate, drop and clear a line | [ ] | [ ] | [ ] |
| Minesweeper: lightbulb on a dotted cell offers Reveal and Flag; reveal shows numbers | [ ] | [ ] | [ ] |
| Corrupt the header (e.g. `seed=x`): one warning, Reset game fixes it | [ ] | [ ] | [ ] |
| Delete a canvas row: one warning, Repair canvas fixes it | [ ] | [ ] | [ ] |
| `Program.cs` and other files show no Squiggly diagnostics | [ ] | [ ] | [ ] |

Record the colours you see, and update the palette table in `README.md` if anything differs:

| Severity | Visual Studio 2022 | VS Code + C# Dev Kit | Rider |
|---|---|---|---|
| Error (SQ0001) | | | |
| Warning (SQ0002) | | | |
| Info (SQ0003) | | | |

## 4. Release

- [ ] Update `src/Squiggly.Analyzers/AnalyzerReleases.Shipped.md`: move the rules from `Unshipped.md` under a `## Release x.y.z` heading.
- [ ] Commit, push, and wait for CI to pass.
- [ ] Tag and push: `git tag v1.0.0 && git push origin v1.0.0`. MinVer turns the tag into the package version.
- [ ] Watch the **Release** workflow: it builds, tests, packs, pushes to nuget.org and creates the GitHub release.
- [ ] Check the package page on nuget.org, then install it into a brand-new project with `dotnet add package Squiggly` and play one game.
