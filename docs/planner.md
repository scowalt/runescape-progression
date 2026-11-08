# Deterministic planning seam (#3, #4)

The pure F# library now searches finite synthetic worlds through:

```fsharp
Planning.plan : PlanningCatalog -> AccountSnapshot -> SearchBudget
    -> Result<PlanningOutcome, PlanningError list>
```

`Domain.fs` defines the inputs and results. Callers supply typed records, not executable rules. `PlanningCatalog` describes structure; its name and successful validation **do not establish operator approval**. There are no bundled game records or catalog-selection adapters yet. Synthetic records live only in `tests/RuneSprout.Tests/` and cannot be selected through the CLI.

## Supported model

- One location from a finite declared set, named Boolean facts, and nonnegative resource quantities.
- Activities require locations, fact values, and minimum quantities; consume/produce resources; optionally move; and assign fact values.
- Resource inputs are required even without a separate quantity requirement. Effects are simultaneous: consumed quantities are removed before checking the resulting quantities against their limits. An infeasible effect disables an activity; it is never clamped.
- Each resource has a `ResourceLimit`: `Capacity n` for a separately stored quantity, such as free inventory slots, a bank item, or stored production, or `OccupiesSpace space` for a quantity that fills another resource's slots. An activity's net change to occupants is taken from or returned to their space, so a transformation inside a full inventory needs no free slot and the consumed container is gone. Activities cannot change a space resource directly. A space resource counts free slots; occupants plus free slots never exceed its capacity, and the remainder is space the model does not track.
- Capacities are supplied **world rules**, not solver-selected caps. With finitely many locations and facts and each resource bounded by its capacity or its space's capacity, the complete modeled state space is finite. No pruning by arbitrary quantity or route-length limits is used.

Eligibility, quest acceptance and completion, individual delivery credit, and facility, access, or spawn availability are facts. Inventory, bank, and intermediate-storage quantities are distinct resources, and current possessions, not past work, are what the snapshot states.
- Duration estimates are separate from mechanics in `DurationEstimatesMs`. They are fixed nonnegative `int64` milliseconds, including zero; no floating-point or display rounding participates in comparison.

## Partial account knowledge

`AccountKnowledge` states every declared fact and resource. The location and each fact are `Known` or `Unknown`; each quantity is `Exactly n` or `AtLeast n`. `AtLeast 0` means nothing is known about that count, and it is a different input from `Exactly 0`. Omitted or extra entries are input errors, so no default can enter the planner silently.

`AccountSnapshot.Assumptions` are applied in order on top of the knowledge. An assumption may settle an unknown or raise a lower bound. One that contradicts a known value, an earlier assumption, or a capacity returns `ContradictoryAssumption`. Every returned plan repeats the snapshot's assumptions.

An activity is permitted only when its requirements, inputs, space, and capacity are guaranteed in every account the knowledge describes. A count's upper bound follows it through the route, so room freed by spending from an unknown count is guaranteed room, and an unknown location in a one-location world is that location. Effects keep the kind of each quantity: a pickup from `AtLeast 0` gives `AtLeast 1`, never `Exactly 1`, and unrelated unknowns stay unknown in the final knowledge.

To decide what an unknown might change, the planner also searches a relaxed model where an unknown may resolve favorably. A relaxed route records what it assumed about the starting account: one location, one value per fact, and a lowest and highest starting count for each resource. Needing more of a count raises its lowest start; adding to a count that might overflow its capacity lowers its highest start. A route is dropped once some count's lowest start passes its highest, or the lowest starts no longer fit a capacity or space together. Every relaxed route is therefore possible in at least one account the knowledge describes. This narrow representation is not a complete OSRS account model.

## Search and return guarantees

Dijkstra's search keys on the **entire** immutable `AccountKnowledge` value; every search also keys on the starting count bounds it assumes and the unknowns it relies on. Its queue and cost dictionary are local to one call. Activities are ordered by their typed string identifiers (ordinal comparison); equal priorities use insertion order. A state is queued only when its established cost improves, and stale entries do not spend expansions.

An expansion is one evaluation of a non-goal state's outgoing activities, including when none are available. `SearchBudget.defaultBudget` is 100,000 expansions; overrides must be positive. On reaching the limit the solver may still skip stale entries and select a minimum-cost goal without expanding it. It does not expand any completed-goal state, so future benefits cannot extend the optimization horizon.

| Outcome | Stopping reason | Meaning |
| --- | --- | --- |
| `ProvenFastest` | `MinimumCostGoalSelected` | A guaranteed minimum-cost route, and no account the knowledge describes has a cheaper one: shortest-path certificate under the supplied finite model, knowledge, and estimates. |
| `BestFound` with facts | `MinimumCostGoalSelected` | The cheapest guaranteed route. Some account the knowledge describes has a cheaper route that relies on the listed unknowns. |
| `BestFound` without facts | `ExpansionBudgetExhausted` | The cheapest guaranteed route discovered so far, including goals not yet selected from the queue; no optimality certificate. |
| `NeedsAccountInformation` | `FrontierExhausted`, or `ExpansionBudgetExhausted` if a later fact check ran out of budget | No guaranteed route exists, some described account has one, and every such route relies on each listed fact. Other unknowns are not requested. |
| `NoCompleteModeledRouteFound` with facts | `FrontierExhausted` | No guaranteed route exists, and some described account has one, but no single unknown is required. Resolving the listed facts may help. |
| `NoCompleteModeledRouteFound` without facts | `ExpansionBudgetExhausted` | Search stopped without a complete route; this is not evidence that none exists. |
| `NoCompleteModeledRouteFound` without facts | `FrontierExhausted` | No account the knowledge describes has a route in the modeled states. This is not impossibility in the game. |

Fully known snapshots run one search and keep the #3 results exactly. Partial snapshots run the guaranteed search, then relaxed searches that share the same expansion budget; `ExpandedStates` is the total. A relaxed search limited by the budget only withholds claims, so `ExpansionBudgetExhausted` then replaces `FrontierExhausted` or `MinimumCostGoalSelected`.

Every outcome includes the expansion count. Every complete plan includes ordered activity identifiers, per-step estimates, exact total milliseconds, and resulting knowledge. An already-complete valid snapshot returns zero steps and zero time without expansion.

Before a complete result leaves `Planning.plan`, the library replays it in the guaranteed model from the knowledge after assumptions, checking each requirement, resource effect, supplied duration, the goal horizon, completion, final knowledge, and exact total. Replay failure is an error, not a partial plan.

Malformed identifiers/references, missing durations, negative durations or quantities, invalid spaces, knowledge that omits or adds entries, starting quantities beyond capacity or space (one error per overfilled capacity), contradictory assumptions, and nonpositive budgets return `InvalidInput` errors. All arithmetic is checked. If an evaluated feasible path exceeds `Int64.MaxValue` milliseconds, the call fails closed with `CostOverflow` naming the activity, even if another route was found. The solver never wraps the cost, converts it to zero, or silently drops that path to make an optimality claim.

These times are supplied estimates, not guaranteed completion times or measured game rates. No I/O, knowledge acceptance, actual work history, or live account updates occur in the library.

## Verification

The agreed test seam is `Planning.plan`, not the internal queue or transition functions:

```sh
mise exec -- dotnet test tests/RuneSprout.Tests --configuration Release \
  --filter FullyQualifiedName~PlanningTests
mise exec -- dotnet test tests/RuneSprout.Tests --configuration Release \
  --filter FullyQualifiedName~KnowledgeTests
mise exec -- dotnet test tests/RuneSprout.Tests --configuration Release \
  --filter FullyQualifiedName~OracleTests
mise run test
```

`PlanningTests.fs` exercises costs, validation, capacity, deterministic ties, state improvement, cycles, goal-dependent access, budgets, and result classification. `OracleTests.fs` independently enumerates simple paths using arbitrary-precision costs across all 4,096 directed three-location worlds whose edges are absent or cost 0–2 ms. Removing cycles cannot increase a nonnegative route cost, so this enumeration needs no arbitrary search-depth bound. Separate resource-preparation and travel-ordering worlds compare complete routes against enumeration. Both proven-fastest and budget-limited repeated-resource-trip routes are replayed by independent test-world rules.

`KnowledgeTests.fs` covers the #4 scenarios: known zero against unknown counts, pickups, inventory space, containers, bank and stored production, delivery credit, assumptions, zero-action plans, and each outcome above. `OracleTests.fs` also generates 300 small partial-knowledge worlds, and 600 worlds that add to unknown capped counts, and enumerates every concrete account each snapshot describes. Each outcome must hold for those accounts: plans run in every one, `ProvenFastest` matches every account's optimum, `BestFound` points to an account with a cheaper route, listed facts point to an account with a route, and an unqualified exhausted frontier means no account has a route. `NeedsAccountInformation` must name exactly the unknowns where, however the other unknowns turn out, no one route works for every value of that unknown. Listed facts with no requested ones mean no unknown is like that.

The full v0 scenarios, approved game catalog, and snapshot-to-plan CLI remain future ticket work.
