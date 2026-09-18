# RuneSprout: v0 specification

The user approved this product specification through the design interview, then selected RuneSprout as the product name during first-increment planning. This document does not authorize implementation.

## Purpose

Make progress toward distant Old School RuneScape goals visible through small, countable tasks. Efficiency takes priority over choosing an enjoyable or inexpensive route. The long-term product combines a website and RuneLite plugin, but v0 proves the planning model first.

## Initial scope

The first artifact is a standalone planner with these boundaries:

- Support a members ironman account without additional self-imposed restrictions.
- Plan for one active main goal.
- Use a small Cook's Assistant knowledge catalog.
- Accept explicit account snapshots, state updates, and recorded activity outcomes.
- Produce recommended work in an efficient execution order.
- Group visible work into natural gameplay units, such as inventories or trips.
- Allow explicit duration estimates rather than require live measurements.
- Require the coding-agent operator to approve proposed game knowledge before use.

The first version does not require a website, plugin, live game connection, or comprehensive wiki importer. It does not perform in-game actions.

## Model ownership

The [glossary](../CONTEXT.md) defines the domain language. The project owns its game model rather than adopting wiki page structure as its model.

The game model describes activities, requirements, resource inputs, and effects. The knowledge catalog contains accepted descriptions expressed through that model. It remains separate from account knowledge, the personal plan, and work history.

Requirements describe what must be true. Activities describe work that changes the situation. Alternative methods can introduce different prerequisites, preparation, resource costs, and travel.

Personal tasks are generated from the model and account knowledge. They are not permanent nodes whose completion alone determines game state. Necessary preparation for a selected method is not automatically a mandatory requirement of the main goal.

Resource quantities and effects matter. Reusable access, consumed ingredients, transformed containers, existing possessions, and delivered quest items are not interchangeable. Shared requirements can occur within one main goal.

## Optimization contract

Minimize expected remaining time, the average additional playtime until the current main goal is complete. Do not credit benefits that occur only after that goal.

Compare complete routes rather than choosing each method independently:

- Include necessary money-making, preparation, and resource acquisition in route costs.
- Include starting location, travel options, bank visits, and inventory capacity.
- Compare bank retrieval with new acquisition by total time. Existing ownership has no unconditional priority.
- Include an unlock detour when its preparation cost is outweighed by savings before goal completion.
- Include unsuccessful attempts in expected time where relevant.
- Distinguish estimated duration from guaranteed completion time.

Training Magic to use a teleport can beat repeated walking. The comparison includes training and supplies, then counts savings across the remaining route. It does not evaluate each trip independently from the original account state.

Protected-resource preferences, sacrifice valuation, and risk preferences are deferred. Ordinary resource inputs and consumption still constrain feasible routes.

The objective concerns modeled methods, supplied costs, and available account knowledge. It does not promise the globally fastest route across all game possibilities.

## Knowledge and timing evidence

An agent can propose game knowledge, but the coding-agent operator approves its initial use. Structural validation alone does not establish correct mechanics. Keep supporting sources traceable and preserve conditions, quantities, account restrictions, and intermediate states.

Explicit timing estimates are acceptable. Keep sourced mechanics, estimated durations, and measured durations distinguishable. State-dependent timing calculations are a future capability, not a prerequisite for the first dataset.

The planner does not invent missing activities or approve new knowledge while solving a route. Knowledge gathering and acceptance are separate from planning. Future automated acceptance requires a later design.

## Account knowledge and progress

Unknown facts and known absence are different states. Plan around known facts where they support a valid costed route. Newly learned facts can change the remaining plan.

If essential unknown account facts prevent a valid costed plan, identify the missing information. Explicit scenario assumptions are acceptable. Silent defaults are not.

The standalone version replans through explicit state updates and recorded activity outcomes. It does not require continuous client observation. Actual state overrides the checklist when the player progresses through another method.

Each batch names its counting unit. Count successful pickpockets for XP work, kills for drop attempts, and produced items for crafting. Advancing a kill counter does not establish receipt of a random drop.

Batch boundaries use natural gameplay units. No fixed duration target is selected. Presentation boundaries do not force pauses or extra game actions.

Work history records actual activity. If a batch becomes unnecessary at 12/28, preserve twelve performed actions and retire the remaining sixteen. Do not manufacture completion or erase real work. A state update that satisfies a requirement does not prove the player performed the originally planned method.

## Planning results

The result must distinguish these outcomes:

- Proven fastest plan: a complete feasible route with established optimality under the supplied model, knowledge, and costs.
- Best-found plan: a complete feasible route returned before search establishes optimality.
- Needs account information: essential unknown account facts prevent a valid costed plan.
- No complete modeled route found: search does not establish a complete route. Report known blockers, data gaps, or search limits without inventing an explanation.

If a computation budget expires after search finds a complete feasible plan, return the best such plan with its limitation. Do not label it proven fastest. Exhausting a budget without a plan does not prove the model has no route.

Failure to find a modeled route never establishes that the goal is impossible in the game. A complete best-found plan is also different from an incomplete list of tasks.

## Acceptance evidence

The [acceptance scenarios](domain/scenarios.md) are agreed requirements, not executed tests. Three game-specific cases establish the initial Cook's Assistant proof:

- Ingredients already carried before quest acceptance.
- Cashless local collection with explicit starting conditions.
- Partial deliveries, stored flour, and changed possessions.

Controlled cases also cover money-making and teleport detours, bank retrieval versus replacement, expected-time comparisons, and the current-goal optimization horizon. Further cases cover unknown-state updates, counter units, partial work history, knowledge approval, missing information, and incomplete search.

Route selection is checked against explicit supplied costs. Compare Cook's Assistant output with the user's expected efficient steps to find missing methods or incorrect estimates. Do not treat synthetic test costs as measured game timings.

The source-backed mechanics include gathering before acceptance, separate ingredient deliveries, and flour stored outside inventory. The [quest research](research/cooks-assistant.md) records evidence and unresolved details, including ordinary container return behavior at hand-in. Unsupported effects must not be invented.

## Wiki acquisition boundary

The intended long-term source is https://oldschool.runescape.wiki/. The [ingestion research](research/wiki-ingestion.md) identifies Bucket as its current structured-data API. The former Semantic MediaWiki interface is deprecated.

Prefer deterministic mapping for structured facts and consider LLM interpretation for narrative rules or missing mappings. The specific importer remains a future implementation choice. Broad ingestion is not part of v0.

Clarify permitted automated-access scope before bulk ingestion. Review attribution and reuse obligations before distributing imported material. Article licensing does not automatically resolve the legal treatment of the eventual extracted dataset.

## Deferred scope and next phase

The following work remains outside v0:

- Additional account types, optimization objectives, and simultaneous main goals.
- Website graph exploration, RuneLite integration, synchronization, and sharing.
- Continuous observation, background scheduling, and large-scale caching.
- Bulk wiki ingestion and automated knowledge acceptance.
- Rich timing calculations from player state, game conditions, and available methods.
- Risk preferences and protection of valuable resources.

Concrete search algorithms, computation budgets, performance targets, data representations, storage, and operator interfaces belong to implementation planning. This specification does not silently select a database or deployment architecture. No application code, importer, or service was created during the interview.
