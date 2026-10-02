# Domain docs

## Layout and reading rules

This is a single-context repository.

Before exploring:
- Read root `GLOSSARY.md` for domain language.
- Read relevant decisions under `docs/adr/`.
- Follow the additional required reading in `AGENTS.md`.

If optional domain documents or the ADR directory are absent,
proceed silently. Create them lazily when terms or decisions are
resolved, rather than scaffolding placeholder documents.

## Filename migration

Domain docs now use `GLOSSARY.md` and, for multi-context repositories,
`GLOSSARY-MAP.md`. For existing tracked documents using the old convention,
run `git mv CONTEXT.md GLOSSARY.md` and, if a map exists,
`git mv CONTEXT-MAP.md GLOSSARY-MAP.md`, then update references.
Only the new filenames are read going forward; this repository has no map.

## Vocabulary

Use the terms defined in `GLOSSARY.md` in issues, proposals, and tests.
If a needed concept is missing, reconsider the term or flag the gap
for domain modeling.

## Decision conflicts

Explicitly flag any proposal that contradicts an existing ADR.
Identify the decision and explain why it should be reopened rather
than silently overriding it.
