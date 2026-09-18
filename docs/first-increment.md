# First increment: snapshot to plan

Status: the user accepted decisions Q1–Q9. This document consolidates those decisions and proposes concrete engineering defaults for final review. No application implementation is authorized by this document.

The [v0 specification](spec.md) remains the product contract. This increment delivers part of v0 rather than replacing it. The [glossary](../CONTEXT.md) defines the domain language.

## Agreed decisions

- **Q1, identity:** Name the product RuneSprout. The user owns `runesprout.app`, its future web address. Do not add hosting, DNS configuration, or a landing page.
- **Q2, milestone:** Turn an account snapshot into a costed, ordered Cook's Assistant plan. Compare alternative routes rather than print a hardcoded checklist.
- **Q3, coverage:** Support all three approved Cook's Assistant scenarios as independent snapshots. The changed-possessions case does not require importing work history.
- **Q4, operation:** Use a local command-line tool with editable JSON snapshots, bundled examples, and readable terminal output. Do not add a database or interactive terminal interface.
- **Q5, catalog:** Include free collection and production, bank retrieval, quest dialogue, and connecting travel. Defer shops, money-making, and teleport unlocks.
- **Q6, approval:** Require explicit operator approval of a versioned catalog through repository review. Preserve sources and separately labeled timing estimates. Do not automate acceptance.
- **Q7, explanation:** Show ordered activities, quantities, travel, per-step and total estimated time, explicit assumptions, and the result's limitations.
- **Q8, search:** Use Dijkstra's shortest-path algorithm over modeled game states, with explicit timing estimates, deterministic effects, preserved unknown facts, and a search budget.
- **Q9, structure:** Use a pure F# planner library, a thin CLI, and automated tests on .NET 10. Pin tools and dependencies. Express catalog records as typed F# data and run build and test checks in CI.

Work recording and the explicit state-update and replanning loop belong to the next increment. Running this tool again with another snapshot does not create an ongoing player session.

## Design tree and review status

The scenario, catalog, and workflow decisions constrain the state model. That model constrains the search and its claims. Those contracts determine the executable tests.

The engineering defaults below make the remaining choices explicit. Final review covers the state representation, budget, command contract, test approach, and delivery sequence. Approval of this plan does not approve unreviewed game records.

## Demonstration

The operator runs the tool separately against each bundled snapshot:

- Ingredients already carried: recommend quest dialogue without collection, production, banking, or money-making.
- Cashless local collection: find a complete free route with containers, an egg, milk, and flour. Compare travel and activity order using supplied costs.
- Partial delivery and changed possessions: retain egg delivery credit, collect existing flour, and obtain milk. Do not produce more flour or infer milk possession from past work.

Add a controlled bank-versus-replacement case to demonstrate genuine route selection. Changing only the supplied travel or activity costs must change the winning method when the total costs reverse.

These demonstrations establish behavior under the supplied model and estimates. They do not establish measured fastest routes in OSRS.

## Planner interface and responsibilities

Use one main planning interface with explicit inputs and a returned result. This sketch describes responsibilities, not final source code:

```fsharp
plan : ApprovedCatalog -> AccountSnapshot -> SearchBudget
    -> Result<PlanningOutcome, InputError list>
```

The library validates domain inputs, evaluates requirements, applies modeled effects, searches routes, and constructs a personal plan. It performs no file access, network access, console output, or approval side effects.

The CLI reads JSON, selects the shipped approved catalog, calls the library, and renders the result. Keep JSON parsing and terminal formatting outside the planner.

Use immutable domain state and explicit dependencies. Keep any mutable priority queue and search bookkeeping local to a planning call. Do not expose the search implementation through the caller's interface.

Do not add a generic plugin system, rule scripting language, database abstraction, or service framework. The first catalog can use the domain types directly.

## Account knowledge and modeled state

Represent only facts needed by the included methods. Do not require a complete OSRS account export.

The state must distinguish:

- Account eligibility: members ironman, with no additional restrictions.
- Location: a position in the small catalog's movement model, not an inferred starting point.
- Inventory: relevant item quantities and guaranteed available space.
- Bank: relevant quantities, separate from carried items.
- Quest progress: acceptance, individual ingredient delivery credit, and completion.
- Intermediate production: flour stored outside inventory and any other intermediate state exposed by an included activity.
- Method availability: relevant facilities, access, and spawn availability under explicit scenario conditions.

Use discriminated unions for non-quantity facts that are known or unknown. For quantities, preserve exact counts when known and guaranteed lower bounds when only those are known.

An unknown egg count followed by a verified pickup establishes at least one egg, not an exact total of one. A known zero remains different from an unknown count. Acquisition can therefore establish a requirement without first resolving every unrelated account fact.

Quantity and space calculations must remain consistent. Unknown inventory contents cannot silently become an empty inventory. An activity can use only space and resources that the supplied facts guarantee.

Missing account-fact fields mean unknown, not zero or false. Invalid syntax, negative quantities, contradictory facts, and unsupported identifiers are input errors. Unsupported snapshot versions are also input errors.

Allow an explicit assumptions section in the snapshot. Apply assumptions visibly, reject contradictions with known facts, and repeat the assumptions in the output. Do not supply hidden defaults for account facts.

A snapshot that already establishes quest completion produces an empty plan with zero remaining time. It does not require unrelated location or bank information.

## Activities and initial catalog

An activity record needs an identifier, requirements, resource inputs, effects, an explicit duration estimate, and supporting evidence. Requirements describe conditions, not checklist completion.

Cover these operations with the smallest faithful records:

- Travel between included locations.
- Pick up free containers, an egg, and grain.
- Transform an empty bucket into milk.
- Process grain into stored flour, then collect flour with an empty pot.
- Retrieve known banked ingredients or containers.
- Accept the quest and deliver ingredients, including pre-collected ingredients and separate delivery credit.

Travel and production records must state their start and end positions. If an activity includes movement within a facility, its estimate includes that movement exactly once.

Support completion during the initial dialogue when all ingredients are carried. Do not force extra conversations or visits.

The first catalog includes bank withdrawals, not a general bank-management system. It does not model deposits, arbitrary item disposal, all transports, or all possible player locations. Report missing modeled methods when those omissions block a route.

Inventory capacity, stored-flour limits, access rules, and availability conditions need evidence or explicit scenario assumptions. Facilities remain available unless a sourced rule says otherwise. Transformed containers do not remain separately available.

The [quest research](research/cooks-assistant.md) leaves ordinary container returns at hand-in unresolved. Do not invent either a returned container or a guaranteed empty slot. Record only supported effects and use conservative space guarantees where needed.

If that evidence gap prevents a required demonstration or a defensible route comparison, stop for targeted research or operator clarification. Do not silently choose a return behavior.

Collection must not depend on quest acceptance. Delivered ingredients must not become acquisition requirements again. Methods whose access requires completing the goal cannot supply its prerequisites.

### Evidence and approval

Keep proposed records distinct from the shipped approved catalog. Structural validation establishes usable structure, not correct mechanics or operator approval.

Prepare the candidate catalog with stable record identifiers, source links, conditions, and a separate timing set. The operator reviews the exact record set before it becomes eligible for application planning.

Record approval against a catalog version and the reviewed content identity. Changes to mechanics require renewed approval of the changed content. Do not let a caller-controlled JSON flag grant approval.

The approval record documents a repository-review workflow. It is not a cryptographic proof of human review. No automated check or coding agent can substitute its own acceptance.

Keep hypothetical test costs, estimated game timings, and any future measured timings distinguishable. Identify the catalog and timing-set versions in each output. The solver does not fetch sources or propose missing methods.

## Search and result contract

Search states, not requirements in isolation. A state key includes every represented fact that can affect feasibility, effects, or remaining cost. Two routes can share a search state only when those future consequences are equivalent.

Use fixed nonnegative durations, represented in integer milliseconds, with checked arithmetic. Do not compare rounded display values. Reject invalid costs rather than turn them into free actions.

The first increment models deterministic activity effects. It does not implement drop probabilities, repeated random attempts, contingent policies, or state-dependent timing formulas. Those v0 capabilities remain later work.

Dijkstra's algorithm keeps the least established cost for each state. Use stable activity ordering and deterministic tie handling. Revisit a state only when its cost improves, so repeated travel and zero-cost cycles do not create endless work.

Do not expand completed-goal states. This enforces the current-goal optimization horizon.

The represented state space must be finite. Derive bounds from the approved scope, inventory capacity, storage limits, and finite initial bank quantities. Do not add silent resource caps that remove otherwise valid modeled routes.

If the representation cannot remain finite without such a restriction, revisit the model before claiming optimality. Search exhaustion and an arbitrary truncation are different facts.

### Proposed budget

Use a default limit of 100,000 expanded states, with a positive-integer CLI override. Count an expansion when search evaluates the outgoing activities of a state. Track expansion counts and the stopping reason.

This is a starting computation limit, not a latency or real-game performance promise. The bundled scenarios must finish within the default limit. If they do not, inspect the model and search before proposing a documented change to the limit.

Keep the least-cost complete feasible route found so far, including goal states discovered before removal from the priority queue. A budget expiry must not discard that route.

### Outcomes

Return one of the four product outcomes after valid input:

- Proven fastest plan: a complete route with an established minimum under the supplied model, facts, and costs.
- Best-found plan: a complete feasible route without an established minimum, including a route retained when the budget expires.
- Needs account information: essential unknown account facts prevent a valid costed route.
- No complete modeled route found: no complete route is established, with known blockers, evidence gaps, or the search limit reported.

For Dijkstra's algorithm, selecting a minimum-cost goal from the queue establishes the usual shortest-path certificate over the searched model. Budget exhaustion without that certificate does not establish optimality or absence of a route.

Unknown-dependent alternatives need explicit treatment. A complete supported route can be returned while bank contents remain unknown. If unresolved relevant facts prevent a defensible optimality claim, use best found and explain the limitation.

Missing-information diagnostics must identify necessary facts, not demand every omitted field. Distinguish an unknown account fact from an absent game method. If essentiality cannot be established, report the uncertainty instead of inventing a cause.

Before returning a complete plan, replay its activities from the supplied knowledge. Confirm feasible requirements, valid resource effects, goal completion, and the reported total cost.

## CLI and presentation defaults

Provide a `plan` command with a required `--snapshot` path and optional `--max-expanded-states` override. Cook's Assistant is the only goal in this increment. Do not add an arbitrary goal-selection interface yet.

Proposed source-tree invocation after implementation:

```sh
mise exec -- dotnet run --project src/RuneSprout.Cli -- plan \
  --snapshot examples/cooks-assistant/already-carried.json
```

Use a versioned JSON snapshot format and bundle examples for all three required cases. Document unknown facts, explicit zero quantities, supported locations, and assumptions. Do not accept arbitrary executable rules or unapproved catalogs through JSON.

Print the result classification, catalog and timing versions, assumptions, search counts, ordered work, and total estimated time. Each step names its quantity or counting unit and expected duration. Include travel and required preparation explicitly.

Use natural trip or production groupings where they clarify the route. Grouping must not add pauses, actions, costs, or dependencies. Do not imply recorded progress or expose a completion counter that this increment cannot maintain.

Explain timings as estimates rather than guaranteed completion times. Explain optimality relative to the supplied model, not every method in OSRS. Listing every rejected route is not required.

Proposed exit codes are 0 for a complete plan, 2 for invalid input, 3 for required account information, and 4 for no complete modeled route. Reserve 1 for an unexpected execution error. Machine-readable plan output is deferred.

## Repository structure and tooling defaults

Use two production projects and one test project:

```text
src/RuneSprout.Planner/    Domain types, validation, catalog data, transitions, search
src/RuneSprout.Cli/        JSON input and terminal output
tests/RuneSprout.Tests/    Domain, planning, catalog, and CLI tests
examples/cooks-assistant/  Explicit JSON snapshots
```

The paths above describe planned files, not files that already exist. Keep the generic search implementation independent of Cook's Assistant identifiers, even when both live in the same library.

Use xUnit with `dotnet test`. Prefer behavior tests through the planner interface over tests of private search bookkeeping. Add process-level CLI tests for parsing, rendering, and exit codes.

Pin a supported .NET 10 SDK patch in mise and `global.json`. Select the servicing patch when implementation starts rather than assume the installed patch is current. Pin NuGet dependencies and use locked restore.

Add mise tasks for restore, build, test, and the sample invocation. Preserve the existing setup task and secret-scanning hook.

Add GitHub Actions checks for restore, build, tests, and secret scanning on pushes and pull requests. Give workflows only the permissions they need. Keep the repository private and do not configure deployment.

## Delivery sequence

1. Establish the F# projects and reproducible toolchain. Add build and test tasks, a minimal executable check, and CI without placeholder application behavior.
2. Implement validated state and activity types. Test unknown versus zero, quantity transformations, delivery credit, inventory constraints, and explicit assumptions.
3. Prepare candidate Cook's Assistant records and timing estimates. Review the exact catalog with the operator before any application route uses it.
4. Implement state transitions and shortest-path search against clearly labeled synthetic fixtures. Cover complete-route costs, cycles, proof labels, budgets, and diagnostics.
5. Integrate the approved game catalog and the three snapshots. Compare emitted routes with the operator's expected efficient steps under the supplied costs.
6. Add JSON parsing and terminal rendering. Run the full command for each scenario and document how to edit a snapshot and rerun it.

Synthetic fixtures describe test worlds, not accepted OSRS mechanics. They remain test-only and cannot be selected by the application CLI.

Each step must leave executable evidence for its behavior. Do not substitute completed data types or an attractive output example for a working route search.

## Required verification

The tests must cover:

- All three approved Cook's Assistant cases, with complete feasible routes under their stated assumptions.
- Bank retrieval versus replacement in both cost orderings, with the winning route changing when supplied totals reverse.
- Alternative activity orderings with different travel totals, rather than independent per-ingredient method selection.
- Unknown bank contents permitting a known acquisition route, followed by a separate snapshot that establishes a faster bank route.
- Unknown starting location producing a missing-information result when required, without a silent location default.
- Acquisition establishing a guaranteed item quantity when starting possession is unknown.
- Already-complete quest state producing a zero-action, zero-time plan.
- Partial delivery, stored flour, resource consumption, container transformation, and insufficient guaranteed inventory space.
- Cyclic movement and goal-dependent access, without infinite search or circular prerequisite routes.
- Unapproved catalog content remaining ineligible despite structural validity.
- A budget cutoff after finding complete routes retaining the cheapest found route with the correct limitation.
- A budget cutoff before finding a route differing from exhaustive modeled failure.
- A known small optimum receiving the proven-fastest label, with costs checked against independent enumeration on tiny synthetic models.
- Invalid input, inconsistent facts, invalid costs, overflow handling, deterministic ties, and the documented CLI exit codes.

Replay validation must succeed for every returned complete route. Exact shortest-path tests use supplied fixture costs, not an unsupported real-game timing claim.

Work-history retirement, skill training, money-making and teleport detours, random outcomes, and ongoing replanning remain unimplemented v0 scenarios. Do not describe the full v0 acceptance suite as passing after this increment.

## Completion and stop conditions

Completion evidence consists of a clean restore, build, and test run; passing CI; and captured CLI output for all three bundled snapshots. Include the changed-cost demonstration and the operator's catalog approval reference.

The operator must be able to edit a supported snapshot and obtain a newly computed route without changing source code. All required examples must complete under the default search budget.

If a source gap, model limitation, approval requirement, or toolchain problem blocks that evidence, report the blocker and attempted paths. Request the specific missing input. Do not invent mechanics, raise search limits silently, broaden scope, or start scraping.

The next increment adds work recording and explicit state updates. This increment does not select persistence for that future work.

## Toolchain reconnaissance

Read-only inspection found .NET SDK 10.0.201 with F# 10.0 installed locally. The repository does not activate a .NET version through mise. Its current `dotnet` shim therefore cannot select an SDK here.

The repository currently contains no application projects, solution, `global.json`, executable tests, or GitHub Actions workflow files. Gitleaks and Lefthook are installed, and the pre-commit hook is present. Remote CI and branch-protection configuration were not inspected.

No separate Mission Control guidance was found in the inspected local documentation. The F# defaults in `AGENTS.md` remain the available project guidance. No new tools or application files were installed or created during this planning session.
