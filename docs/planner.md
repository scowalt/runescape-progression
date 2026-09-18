# Deterministic planning seam (#3)

The pure F# library now searches finite synthetic worlds through:

```fsharp
Planning.plan : PlanningCatalog -> AccountKnowledge -> SearchBudget
    -> Result<PlanningOutcome, PlanningError list>
```

`Domain.fs` defines the inputs and results. Callers supply typed records, not executable rules. `PlanningCatalog` describes structure; its name and successful validation **do not establish operator approval**. There are no bundled game records or catalog-selection adapters yet. Synthetic records live only in `tests/RuneSprout.Tests/` and cannot be selected through the CLI.

## Supported model

- One location from a finite declared set, named Boolean facts, and exact nonnegative resource quantities.
- Activities require locations, fact values, and minimum quantities; consume/produce resources; optionally move; and assign fact values.
- Resource inputs are required even without a separate quantity requirement. Effects are simultaneous: consumed quantities are removed before checking the resulting quantities against explicit storage capacities. An infeasible effect disables an activity; it is never clamped.
- Capacities are supplied **world rules**, not solver-selected caps. With finitely many locations and facts and each resource in `0..capacity`, the complete modeled state space is finite. No pruning by arbitrary quantity or route-length limits is used.
- Duration estimates are separate from mechanics in `DurationEstimatesMs`. They are fixed nonnegative `int64` milliseconds, including zero; no floating-point or display rounding participates in comparison.

This ticket requires fully known values for exactly the declared facts and resources. Missing values produce input errors, never invented zeroes or falsehoods. Partial knowledge, lower bounds, assumptions, inventory-space guarantees, game-specific state, and missing-information diagnostics belong to #4. This narrow representation is not a complete OSRS account model.

## Search and return guarantees

Dijkstra's search keys on the **entire** immutable `AccountKnowledge` value. Its queue and cost dictionary are local to one call. Activities are ordered by their typed string identifiers (ordinal comparison); equal priorities use insertion order. A state is queued only when its established cost improves, and stale entries do not spend expansions.

An expansion is one evaluation of a non-goal state's outgoing activities, including when none are available. `SearchBudget.defaultBudget` is 100,000 expansions; overrides must be positive. On reaching the limit the solver may still skip stale entries and select a minimum-cost goal without expanding it. It does not expand any completed-goal state, so future benefits cannot extend the optimization horizon.

| Outcome | Stopping reason | Meaning |
| --- | --- | --- |
| `ProvenFastest` | `MinimumCostGoalSelected` | A minimum-cost goal was selected from the queue: shortest-path certificate under the supplied finite model, known starting facts, and estimates. |
| `BestFound` | `ExpansionBudgetExhausted` | The cheapest complete route discovered so far, including goals not yet selected from the queue; no optimality certificate. |
| `NoCompleteModeledRouteFound` | `ExpansionBudgetExhausted` | Search stopped without a complete route; this is not evidence that none exists. |
| `NoCompleteModeledRouteFound` | `FrontierExhausted` | All reachable modeled states were evaluated without a goal. This is not impossibility in the game. |

Every outcome includes the expansion count. Every complete plan includes ordered activity identifiers, per-step estimates, exact total milliseconds, and resulting knowledge. An already-complete valid snapshot returns zero steps and zero time without expansion.

Before a complete result leaves `Planning.plan`, the library replays it from the supplied knowledge, checking each requirement, resource effect, supplied duration, the goal horizon, completion, final knowledge, and exact total. Replay failure is an error, not a partial plan.

Malformed identifiers/references, missing durations, negative durations or quantities, out-of-capacity starting quantities, and nonpositive budgets return `InvalidInput` errors. All arithmetic is checked. If an evaluated feasible path exceeds `Int64.MaxValue` milliseconds, the call fails closed with `CostOverflow` naming the activity, even if another route was found. The solver never wraps the cost, converts it to zero, or silently drops that path to make an optimality claim.

These times are supplied estimates, not guaranteed completion times or measured game rates. No I/O, knowledge acceptance, actual work history, or live account updates occur in the library.

## Verification

The agreed test seam is `Planning.plan`, not the internal queue or transition functions:

```sh
mise exec -- dotnet test tests/RuneSprout.Tests --configuration Release \
  --filter FullyQualifiedName~PlanningTests
mise exec -- dotnet test tests/RuneSprout.Tests --configuration Release \
  --filter FullyQualifiedName~OracleTests
mise run test
```

`PlanningTests.fs` exercises costs, validation, capacity, deterministic ties, state improvement, cycles, goal-dependent access, budgets, and result classification. `OracleTests.fs` independently enumerates simple paths using arbitrary-precision costs across all 4,096 directed three-location worlds whose edges are absent or cost 0–2 ms. Removing cycles cannot increase a nonnegative route cost, so this enumeration needs no arbitrary search-depth bound. Separate resource-preparation and travel-ordering worlds compare complete routes against enumeration. Both proven-fastest and budget-limited repeated-resource-trip routes are replayed by independent test-world rules.

The full v0 scenarios, approved game catalog, and snapshot-to-plan CLI remain future ticket work.
