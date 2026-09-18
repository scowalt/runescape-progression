# Domain docs

## Layout and reading rules

This is a single-context repository.

Before exploring:
- Read root `CONTEXT.md` for domain language.
- Read relevant decisions under `docs/adr/`.
- Follow the additional required reading in `AGENTS.md`.

If optional domain documents or the ADR directory are absent,
proceed silently. Create them lazily when terms or decisions are
resolved, rather than scaffolding placeholder documents.

## Vocabulary

Use the terms defined in `CONTEXT.md` in issues, proposals, and tests.
If a needed concept is missing, reconsider the term or flag the gap
for domain modeling.

## Decision conflicts

Explicitly flag any proposal that contradicts an existing ADR.
Identify the decision and explain why it should be reopened rather
than silently overriding it.
