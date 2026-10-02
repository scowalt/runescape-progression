# Coding-agent handoff

This is a private repository for RuneSprout. The product interview is complete for v0. The executable scaffold provides CLI help, a library with no planning behavior, and process-level tests. Read [README.md](README.md) for setup and verification commands; implement application behavior only within the requested ticket's scope.

## Read before work

1. Read [GLOSSARY.md](GLOSSARY.md) for the agreed domain language.
2. Read [docs/spec.md](docs/spec.md) for the approved product specification.
3. Read [docs/domain/scenarios.md](docs/domain/scenarios.md) for the acceptance scenarios.
4. Read [docs/design-notes.md](docs/design-notes.md) when a decision needs context.
5. Read the relevant files in `docs/research/` before relying on quest mechanics or planning wiki ingestion.

The standalone planner's agreed increment scope and design are recorded in [docs/first-increment.md](docs/first-increment.md), with engineering defaults for final review. Read that plan before further design or implementation. Catalog scope approval does not approve individual game records. Follow the user's next request rather than restarting the product interview. Do not expand v0 into a website, RuneLite plugin, or comprehensive scraper.

## Preserve these distinctions

- Keep game rules, accepted game knowledge, account knowledge, personal plans, and actual work history separate.
- Keep unknown account facts distinct from known absence.
- Keep best-found routes distinct from proven fastest routes.
- Keep operator-approved knowledge distinct from agent proposals.
- Keep sourced mechanics distinct from timing estimates.

Use Mission Control's F# domain-design defaults when implementation begins: pure domain logic, explicit dependencies and state, discriminated unions, and `Result`. Apply Domain-Driven Design proportionally. Keep implementation details outside `GLOSSARY.md`, and avoid choosing web infrastructure for the standalone proof.

## Repository practices

Use mise for pinned tools. Run `mise install` and `mise run setup` to install the pre-commit hook. Do not bypass secret scanning.

Use `gh` for GitHub operations. Keep this repository private. Do not add secrets or live account credentials to fixtures.

Broad wiki ingestion and automated knowledge acceptance are deferred. Read `docs/research/wiki-ingestion.md` before that work, including unresolved access and reuse obligations. Do not let a planning failure silently trigger scraping or acceptance of invented mechanics.
