# RuneSprout

Players work efficiently toward chosen in-game outcomes through small, countable tasks. The purpose is to make progress toward distant outcomes visible and satisfying from moment to moment.

## Language

**Goal**:
An in-game outcome a player has chosen to work toward.

**Requirement**:
A condition needed to complete a goal or perform an activity.

**Action batch**:
A small task with a target count and a completed count, grouped by a natural unit of gameplay such as an inventory or trip. Its counting unit can be successful pickpockets, kills, or items produced.

**Account state**:
The player's current in-game facts, including skills, quest progress, resources, and location. It determines which requirements are currently satisfied.

**Account knowledge**:
The facts currently known about the player's account state. It can be incomplete and can change when new information becomes available.

**Personal plan**:
The recommended work and execution order toward the player's main goal, based on account knowledge and the optimization objective.

**Game model**:
A description of activities, their requirements, and their effects. It describes game rules independently of any player's current plan.

**Knowledge catalog**:
The accepted descriptions of game facts and methods available to the planner, expressed using the game model.

**Candidate knowledge**:
Proposed game facts or methods that are not yet accepted into the knowledge catalog.

**Activity**:
An in-game operation with requirements and effects, such as consuming resources, producing items, or changing quest progress.

**Method**:
A way to satisfy a requirement or make progress toward it through one or more activities. Alternative methods can differ in prerequisites, resource use, and time.

**Work history**:
The activities and quantities the player actually performed, including partial batches that the current plan no longer needs.

**Unlock detour**:
Work to gain access to a method that can shorten the remaining route to the main goal. An example is training Magic to use a teleport for repeated trips.

**Duration estimate**:
The expected playtime needed to perform an activity or method under stated conditions.

**Expected remaining time**:
The average additional playtime a personal plan needs to complete its main goal, accounting for uncertain action outcomes.

**Proven fastest plan**:
A complete, feasible personal plan with the lowest established expected remaining time under the supplied game model, account knowledge, and duration estimates.

**Best-found plan**:
A complete, feasible personal plan with the lowest expected remaining time among the routes examined so far. A faster route within the supplied model can still exist.
