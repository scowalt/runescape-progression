# RuneSprout

An Old School RuneScape planner that turns a selected goal into small, countable tasks. It seeks the fastest modeled route from the player's known state. A website and RuneLite plugin are long-term parts of the project. The future web address is `runesprout.app`.

This private repository contains the approved v0 specification and supporting research. There is no application implementation yet. The first artifact is a standalone planner for a members ironman account, verified with Cook's Assistant and controlled planning scenarios.

## Start here

- Read [AGENTS.md](AGENTS.md) for the next coding-agent session.
- Read [CONTEXT.md](CONTEXT.md) for the domain glossary.
- Read the [approved specification](docs/spec.md) for scope and behavior.
- Read the [acceptance scenarios](docs/domain/scenarios.md) for verification requirements.
- Read the [design interview](docs/design-notes.md) for decisions and deferred work.
- Read the [quest research](docs/research/cooks-assistant.md) and [wiki ingestion research](docs/research/wiki-ingestion.md) for source evidence and limits.

The [first-increment plan](docs/first-increment.md) records the agreed scope and design, with concrete engineering defaults for final review. The first milestone turns an account snapshot into a costed, ordered Cook's Assistant plan. Do not treat the long-term website, plugin, or wiki importer as requirements for v0.

## Local setup

Only development guardrails are configured. Gitleaks scans staged changes for secrets through a Lefthook pre-commit hook. Both tools have exact versions in `.mise.toml`.

```sh
mise trust
mise install
mise run setup
```

No development server or application test command exists yet. The acceptance scenarios describe required behavior, not tests that already pass.
