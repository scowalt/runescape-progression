# RuneSprout

An Old School RuneScape planner that turns a selected goal into small, countable tasks. It seeks the fastest modeled route from the player's known state. A website and RuneLite plugin are long-term parts of the project. The future web address is `runesprout.app`.

This private repository contains the approved v0 specification, supporting research, a pure F# shortest-route planner verified in synthetic worlds, a help-only CLI, and xUnit tests. Game catalogs and CLI planning come in later tickets. The first application milestone targets a members ironman account, verified with Cook's Assistant and controlled scenarios.

## Start here

- Read [AGENTS.md](AGENTS.md) for the next coding-agent session.
- Read [GLOSSARY.md](GLOSSARY.md) for the domain glossary.
- Read the [approved specification](docs/spec.md) for scope and behavior.
- Read the [acceptance scenarios](docs/domain/scenarios.md) for verification requirements.
- Read the [design interview](docs/design-notes.md) for decisions and deferred work.
- Read the [quest research](docs/research/cooks-assistant.md) and [wiki ingestion research](docs/research/wiki-ingestion.md) for source evidence and limits.

The [first-increment plan](docs/first-increment.md) records the agreed scope and design, with concrete engineering defaults for final review. The first milestone turns an account snapshot into a costed, ordered Cook's Assistant plan. Do not treat the long-term website, plugin, or wiki importer as requirements for v0.

## Local setup

Install [mise](https://mise.jdx.dev/getting-started.html), then run from the repository root:

```sh
mise trust
mise install
mise run setup
mise run restore
mise run build
mise run test
mise run help
```

`build` includes locked restore and F# typechecking; `test` and `help` build first. Tests exercise the public planning seam, compare tiny worlds with independent route enumeration, and launch the built CLI to check help and rejection of unsupported commands. No development server is needed.

To invoke help directly:

```sh
mise exec -- dotnet run --project src/RuneSprout.Cli -- --help
```

Only `--help` is supported: it exits 0 and explicitly states that planning is not implemented yet. Other arguments (including `plan`, or no arguments) exit 2 with usage guidance on stderr. There is no planning sample task yet. Synthetic fixtures are test-only and cannot be selected through the CLI. Game-specific acceptance scenarios remain requirements, not passing planner tests. See [the planning seam](docs/planner.md) for the implemented model, guarantees, limitations, and focused test commands.

## Projects and reproducibility

- `src/RuneSprout.Planner/`: pure domain inputs, validation, transitions, Dijkstra search, and route replay; no I/O or game records.
- `src/RuneSprout.Cli/`: executable adapter, referencing the library.
- `tests/RuneSprout.Tests/`: xUnit planning/oracle and process tests, referencing the CLI so its executable and runtime files are built and copied for testing.

.NET SDK `10.0.401` is pinned in `.mise.toml` and `global.json`, with SDK roll-forward disabled. It was selected from Microsoft's [.NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) (2026-09-08 servicing release; active LTS). The explicit mise backend avoids ambiguity with machine-local `dotnet` aliases. Change both SDK pins together when updating.

Direct test dependencies have exact versions; each project's checked-in `packages.lock.json` also locks transitive packages and the SDK-provided FSharp.Core. Restore is locked by default, and `NuGet.Config` selects nuget.org rather than machine-specific feeds. To intentionally update dependencies, edit the versions, run `mise exec -- dotnet restore RuneSprout.slnx -p:RestoreLockedMode=false --force-evaluate`, review all lock changes, and rerun the checks.

Gitleaks scans staged changes through the existing Lefthook pre-commit hook; `mise run setup` installs it. Keep scanning enabled. Woodpecker runs the same locked restore/build/test commands and comment-policy checks on pushes, tags, and pull requests, using pinned containers without deployment credentials. Pull requests are tested with their target branch merged in the isolated checkout.

GitHub Actions remains enabled during this transition. Its full-history secret scan fetches all branch/tag refs; the standard Woodpecker clone plugin's full-depth fetch covers the selected ref and PR target, not every remote branch. That scan has not been weakened or claimed migrated. Existing Actions checks remain until equivalent all-ref scanning is available.
