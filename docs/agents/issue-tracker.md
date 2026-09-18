# Issue tracker: GitHub

Track work in GitHub Issues for `scowalt/runescape-progression`.
Use `gh` for all GitHub operations.

Existing specifications in `docs/` remain authoritative. Link issues
to the relevant specification and acceptance criteria.

## Operations

Run inside this repository, or pass
`--repo scowalt/runescape-progression` explicitly.

- Create: `gh issue create --title "..." --body-file <file>`
- Read: `gh issue view <number> --json number,title,body,labels,comments`
- List: `gh issue list --state open --json number,title,labels`
- Comment: `gh issue comment <number> --body-file <file>`
- Label: `gh issue edit <number> --add-label "..." --remove-label "..."`
- Close: `gh issue close <number> --comment "..."`

When a skill says "publish to the issue tracker", create a GitHub issue.
When it says "fetch the relevant ticket", read its body, labels, and comments.

## Blocking dependencies

Use GitHub's native issue dependencies as the canonical blocking links.

Get the blocker's numeric database ID:

`gh api repos/scowalt/runescape-progression/issues/<blocker> --jq .id`

Add the blocking edge:

`gh api --method POST repos/scowalt/runescape-progression/issues/<child>/dependencies/blocked_by -F issue_id=<blocker-database-id>`

Use the database ID, not the issue number or node ID.

If native dependencies are unavailable, record
`Blocked by: #<number>, #<number>` at the top of the issue body.
A ticket is ready only when all its blockers are closed.

## Wayfinding

Use a `wayfinder:map` issue for the decision map and native sub-issues
for its tickets. If sub-issues are unavailable, use a task list in
the map and `Part of #<map>` in each child.

Label children `wayfinder:research`, `wayfinder:prototype`,
`wayfinder:grilling`, or `wayfinder:task`.

Choose the first open, unassigned, unblocked child in map order.
Claim with `gh issue edit <number> --add-assignee @me`.
Resolve by commenting with the answer, closing the child, and linking
the decision from the map.

## Pull requests as a triage surface

PRs as a request surface: no.
