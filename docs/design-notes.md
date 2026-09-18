# RuneSprout product exploration

This document records the design interview. The user accepted the recommendations through question 33. The consolidated [v0 specification](spec.md) records the agreed product scope, and the [glossary](../CONTEXT.md) contains resolved domain terms only. Product agreement does not authorize implementation.

## Confirmed direction

- Design first for a members ironman account with no additional self-imposed restrictions.
- Preserve the ability to support other account types later.
- Minimize expected remaining time until the currently selected main goal is complete. Do not credit benefits to later goals. Support other optimization objectives later.
- Include necessary money-making in route costs. Allow money-making detours when they reduce the total time to the goal.
- Let the planner seek the fastest modeled route rather than require the player to choose a method. Do not claim global optimality across unmodeled game methods.
- If search stops early with a complete feasible plan, return it as best found rather than proven fastest. Distinguish that result from finding no complete route.
- Select bank retrieval or new acquisition by total time. Existing ownership does not take priority when another method finishes faster.
- Include starting location, travel options, bank visits, inventory capacity, and preparation in the model.
- Include unlock detours when their cost is outweighed by savings before the current goal is complete. Training Magic for a teleport can beat repeated walking.
- Support one active main goal initially.
- Include a recommended execution order, not just a set of available tasks.
- Let actual game state override the task checklist.
- Distinguish unknown account facts from known absence. Plan around known facts and account for new information when it becomes available.
- If essential missing account facts prevent a valid costed plan, return the required information rather than invent defaults. Explicit scenario assumptions are allowed.
- Trigger first-version replanning through explicit state updates and recorded activity outcomes. Continuous observation is deferred.
- Present small, countable tasks with progress such as 0/10 repetitions. Use natural gameplay units for batches. No fixed duration target is agreed.
- Give each batch an explicit counting unit. Count successful pickpockets for XP work, kills for drop attempts, and produced items for crafting. Include failed attempts in expected time where relevant.
- Preserve actual work when replanning retires a partial batch. Do not mark unperformed work as complete.
- Own a game model of activities, requirements, inputs, and effects. Generate personal tasks from that model and account knowledge rather than store a permanent checklist.
- Defer protected-resource preferences, sacrifice valuation, and risk preferences from the initial optimization formula.
- Focus first on the quality of the graph model. Interactive graph exploration is a later concern.
- Start with a standalone planner rather than require a live RuneLite integration.
- Use a small Cook's Assistant dataset for the first verification. Compare the proposed steps with the user's known efficient route.
- Allow explicitly labeled duration estimates initially. Develop state-dependent calculations or estimates later, based on the player, game conditions, and available methods.
- Let the coding-agent operator approve proposed knowledge initially. Automate knowledge acceptance in the future.
- If the model cannot establish a complete route, report that limitation and relevant blockers or data gaps. Do not invent actions or declare the goal impossible in the game.
- Aim for an integrated website and plugin experience. The website eventually displays the player's personal graph and recorded progress.
- Treat graph sharing as a possible future feature, not a current requirement.
- Use the OSRS Wiki as an intended future source of game knowledge. An LLM coding agent populating a project-owned database is a proposed import approach, not yet a chosen implementation.

The user's pickpocketing example illustrates task size and progress counts. It does not establish a verified NPC requirement, experience rate, or action count.

## Product identification

This specification concerns Old School RuneScape. The initial “Runlight” reference is interpreted as RuneLite for the deferred plugin. RuneScape Progression was the working project label during this interview. The user later selected RuneSprout and confirmed `runesprout.app` as its future web address during [first-increment planning](first-increment.md).

## Design tree

### Audience

Personal use comes first: a members ironman account without additional restrictions. Other account types are a long-term requirement. The approved Cook's Assistant scenarios provide the initial account states for verification.

### Efficiency

The initial objective is minimum expected remaining time to the current main goal. It includes money-making, travel, preparation, and unlock detours. Benefits after the goal is complete do not count. The promise is the fastest modeled route, not a globally fastest route across all possible game methods.

The user's teleport example requires comparing complete routes. Selecting the fastest currently available method for each requirement independently can miss a faster route that first unlocks another method.

Explicit duration estimates are acceptable initially. The long-term direction is to calculate or estimate durations from player state, game conditions, and available methods. The user expects substantial computation. Search can return its best complete feasible plan before proving optimality, but it must label that limitation. Search that finds no complete route is a separate result. Concrete budgets, algorithms, and performance targets belong to implementation planning.

Protected-resource preferences, sacrifice valuation, and risk preferences are deferred. This does not remove ordinary resource consumption or required inputs from method feasibility. Whether advanced methods such as intentional-death routes appear in a future dataset remains outside the initial decision.

### Graph meaning

Small action batches are required. The planner selects the route automatically and considers items already in the bank. Bank withdrawal and new acquisition compete by expected total time.

Movement and preparation belong in the model. The plan includes a recommended order, and visible batches use natural gameplay units. The permanent game model describes activities, their requirements, inputs, and effects. Personal tasks are generated from this model and account knowledge.

There is one active main goal initially. Shared requirements and resources can still occur within its prerequisites. A selected method can introduce preparation work that is not a mandatory prerequisite of the main goal. Storage layout and algorithms remain undecided.

### Progress

Counted task progress is required. Actual account state determines unmet requirements even when the player uses a different method from the plan. Natural gameplay units define batch boundaries. Counters name the actual unit: successful pickpockets, kills, or produced items. Failed attempts can add time without advancing that counter.

Replanning preserves work actually performed. If new information makes a batch unnecessary at 12/28, record the twelve actions and retire the remaining work. Do not erase progress or invent the other sixteen actions.

Account knowledge can be incomplete. Unknown and absent are different states. Planning uses known facts and accounts for newly learned information. It does not require all unknown state to be resolved before producing any plan. If essential unknown account facts prevent a valid costed plan, return the missing information. Explicit scenario assumptions are allowed, but silent defaults are not. Missing game knowledge is a separate problem: report that no complete modeled route was found, with relevant blockers or gaps.

The standalone version accepts explicit state updates and recorded activity outcomes to trigger replanning. Continuous observation and background orchestration are deferred. The separation of work history and the remaining plan is agreed. History storage and retention belong to implementation planning. Keeping work history does not itself choose event sourcing or any other persistence design.

### Validation scope

Model quality comes first. The first artifact is a standalone planner with a small Cook's Assistant dataset. Broad wiki ingestion is deferred. Explicit estimated durations are acceptable, and the coding-agent operator approves knowledge before use.

The user approved the three Cook's Assistant starting states and the documented difficult-case scenarios. Check route selection against explicit supplied costs, not an unsupported claim of real-game optimality. Source-backed mechanics and timing estimates remain distinguishable from live measurements.

The read-only research is complete. [Research notes](research/cooks-assistant.md) record mechanics, sources, and limits. The [acceptance scenarios](domain/scenarios.md) cover pre-collected ingredients, cashless gathering, and partial delivery with changed possessions, alongside controlled planning cases. These are agreed requirements, not executed tests.

The quest permits collection before acceptance and separate ingredient deliveries. Existing flour can remain in a mill bin. These facts expose dependencies and state that a fixed checklist can miss. No live-client test or fastest-route proof took place.

### Game knowledge acquisition

The intended long-term source is https://oldschool.runescape.wiki/. The user proposed using an LLM coding agent to populate a project-owned model in a database. Initial knowledge acceptance requires the coding-agent operator's approval. Future acceptance will be automated, with the mechanism still undecided. No database or scraper implementation is authorized.

The [wiki ingestion research](research/wiki-ingestion.md) is complete. Bucket is the documented structured-data API, and the former Semantic MediaWiki interface is deprecated. Structured records cover some relevant facts, but complete route logic and timing coverage are not established.

The API guide invites external queries, while the Terms of Use restrict automated use without consent. The scope of permitted bulk ingestion remains unresolved. Article content also has noncommercial and ShareAlike licensing terms, with exceptions and separate media licenses. The applicability to the eventual extracted dataset needs review. No broad scraping, live API testing, or maintainer contact occurred.

The initial slice is a small Cook's Assistant dataset. Proposed records remain candidate knowledge until the coding-agent operator approves them for use. Explicit timing estimates are allowed and remain distinguishable from sourced mechanics or measured timings.

When game knowledge cannot establish a complete route, the planner reports the limitation and relevant blockers or data gaps. Knowledge gathering remains separate from route planning. A failed search does not prove the goal impossible in the game.

The proposed acquisition mechanism keeps the project model separate from wiki transport and page structure. Detailed import tooling, approval storage, automated acceptance gates, and update policy remain future implementation decisions.

### Presentation and integration

Graph exploration comes later. Website independence, plugin observation, synchronization, and sharing rules remain deferred. No answer to those questions is assumed.

## Interview outcome

The user accepted the final recommendations with “Your spec is fine.” The product-model interview is complete for v0. The [specification](spec.md) and [acceptance scenarios](domain/scenarios.md) capture that shared understanding.

No implementation, database, service, or importer exists as a result of this interview. No runtime or test result is claimed.

## Boundaries for the next phase

Concrete search budgets, performance targets, algorithms, data representations, storage, and operator interfaces remain explicit implementation-planning decisions. They are not hidden requirements of the product specification.

Live observation, website presentation, synchronization, sharing, automated knowledge acceptance, bulk ingestion, rich state-dependent timing models, risk preferences, and additional account types remain outside the initial modeling scope. Bulk wiki access and reuse obligations require clarification before that work begins.
