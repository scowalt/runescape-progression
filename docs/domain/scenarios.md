# Planning scenarios

These examples are the agreed acceptance scenarios for the [v0 specification](../spec.md). They are not implemented or executed tests. The durations are hypothetical test values, not measured RuneScape rates. All compared routes are valid for the same members ironman account and the same main goal.

## Money-making can shorten a route

Initial situation: A free method reaches the goal in 20 minutes. Another method takes eight minutes but requires gold the player lacks. Earning that gold takes five minutes. These totals include required travel and preparation.

Attempted action: The player requests an efficient plan.

Expected outcome: The plan earns the gold and uses the paid method. The total time is 13 minutes rather than 20 minutes.

Invariant: Compare the complete route costs, including the work required to obtain resources.

## Existing resources can remove acquisition work

Initial situation: The goal requires an item the player already owns in the bank. Retrieving it takes less total time than every other modeled way to supply it.

Attempted action: The player requests a plan from their current account state.

Expected outcome: The plan retrieves the banked item rather than obtaining another copy.

Invariant: Compare retrieval and new acquisition by total time rather than prioritizing existing ownership.

Counterexample: Retrieving a banked item takes five minutes, while obtaining a nearby replacement takes one minute. All other effects and remaining costs are equal. The plan obtains the replacement.

## An unlock detour beats repeated travel

Initial situation: A route requires 100 trips. Without a teleport, the trips take 200 minutes. Training Magic and acquiring the necessary teleport supplies takes 30 minutes. With that preparation, the trips take 50 minutes. All other work is identical, and both complete routes are feasible.

Attempted action: The planner compares routes to the main goal.

Expected outcome: The plan completes the preparation first and then uses the teleport. The compared costs are 80 minutes and 200 minutes.

Invariant: Charge preparation once and count its savings across the remaining route. Do not choose each trip's method independently from the initial account state.

## Future benefits do not justify a slower current goal

Initial situation: A detour adds five minutes to the current goal but saves 30 minutes on a possible future goal.

Attempted action: The planner evaluates the detour.

Expected outcome: The plan excludes it when there is no other benefit before the current goal is complete.

Invariant: The optimization horizon ends at completion of the selected main goal.

## Expected time governs uncertain methods

Initial situation: A feasible route with uncertain outcomes takes ten minutes on average. Another feasible route takes 12 minutes predictably. All relevant consequences are included in those costs.

Attempted action: The planner compares the routes.

Expected outcome: The plan selects the route with a ten-minute expected duration. It does not present that duration as a guaranteed completion time.

Invariant: Compare expected complete-route costs, not guaranteed outcomes or the best possible outcome.

## Progress outside the plan satisfies a requirement

Initial situation: A remaining action batch exists only to meet a skill requirement. The player gains enough experience through another activity to satisfy that requirement.

Attempted action: The planner receives the updated account state.

Expected outcome: The remaining plan no longer requires that batch. The player does not need to complete redundant work to advance toward the goal.

Invariant: Actual account state determines unmet requirements, not adherence to the original checklist.

## Newly learned account facts change the plan

Initial situation: The goal requires an item. Bank contents are unknown, and a known acquisition route can supply the item. No known facts prevent that route. The planner recommends that route using the available information.

Attempted action: New account knowledge establishes that the bank contains the item and that retrieval finishes the goal sooner.

Expected outcome: The remaining plan accounts for the banked item and recommends retrieval. The original unknown bank state is not recorded as confirmed absence.

Invariant: Unknown facts and known absence remain distinct. A plan based on incomplete knowledge can change when more facts become known.

## Counters name what actually advances

Initial situation: A training batch counts successful pickpockets. The player makes an unsuccessful attempt.

Attempted action: The planner records the outcome and updates account knowledge.

Expected outcome: The success counter does not increase. The method's expected duration accounts for unsuccessful attempts.

Invariant: The counting unit determines progress. An attempt and a successful outcome are not interchangeable.

Related case: A batch for a random drop counts kills. A kill without the drop advances that counter, but the item requirement remains unmet.

## Retiring a partial batch preserves actual work

Initial situation: The player completes twelve actions in a batch with a target of 28. Newly learned account facts make the remaining actions unnecessary.

Attempted action: The planner updates the remaining plan.

Expected outcome: Work history retains the twelve performed actions. The remaining sixteen actions are retired rather than recorded as completed.

Invariant: Work history records actual activity independently of whether the current plan still needs that activity.

## Proposed knowledge needs operator approval

Initial situation: A coding agent proposes a new method, and automated checks accept the record's structure. The operator has not approved it.

Attempted action: The planner builds a route from the knowledge catalog.

Expected outcome: The unapproved method is not used. After operator approval, it becomes eligible for consideration when its requirements are met.

Invariant: Automated structural checks do not replace operator acceptance in the first version.

## Missing game knowledge does not justify invented actions

Initial situation: No complete route can be established from the current model. The available evidence identifies an unsupported acquisition method as a gap.

Attempted action: The player requests a plan.

Expected outcome: The planner reports that no complete modeled route was found and identifies the gap. It does not invent the method or declare the goal impossible in the game.

Invariant: Route planning uses accepted game knowledge. Acquiring additional knowledge is a separate process.

## An incomplete search can return a complete plan

Initial situation: Search finds complete feasible routes with expected durations of 20 and 13 minutes. Its computation budget expires before it establishes whether another modeled route is faster.

Attempted action: The planner returns a result.

Expected outcome: It returns the 13-minute route as the best found, not as proven fastest. If search had found no complete route, it would report that distinct outcome instead.

Invariant: Finding a complete route, proving optimality, and exhausting a search budget are different facts.

## Essential unknown account facts need an explicit response

Initial situation: The player's starting location is unknown. None of the supplied methods establishes a valid costed route without that fact. No explicit scenario assumption supplies it.

Attempted action: The player requests a plan.

Expected outcome: The planner requests the missing account information. It does not invent a starting location or misreport the problem as missing game knowledge.

Invariant: Partial account knowledge is allowed, but essential unknown facts cannot silently become defaults.

## Game-specific verification

Cook's Assistant is the selected first goal. The [research notes](../research/cooks-assistant.md) establish relevant mechanics. The user approved the following hypothetical starting states for the first acceptance suite. They establish required behavior, not measured fastest routes.

### Ingredients collected before quest acceptance

Initial situation: The quest is unstarted. The player stands beside the Cook with all three ingredients in inventory and no coins.

Attempted action: The player selects Cook's Assistant as the main goal.

Expected outcome: The plan accepts and completes the quest through dialogue. It does not require collection, production, banking, or money-making.

Invariant: Gathering ingredients does not depend on accepting the quest first.

### Cashless local collection

Initial situation: The quest is unstarted. The player stands in the castle kitchen with no ingredients, containers, coins, or stored flour. No relevant items are in the bank. The player has full run energy, Agility level one, and no usable transport. Required free spawns are available, and inventory space is sufficient.

Attempted action: The player requests a plan.

Expected outcome: The model can produce a feasible route using free containers, an egg pickup, milking, and flour production. No money-making step is compulsory. The fastest order remains subject to movement costs and route comparison.

Invariant: A purchasable resource does not make gold a mandatory requirement when eligible free methods exist.

### Partial delivery and changed possessions

Initial situation: The quest is underway, and the egg is already delivered. The player stands at Mill Lane Mill with an empty pot and bucket. One unit of flour is already in the bin. No ingredients are in the bank. A previous task records one milking, but the player subsequently emptied that milk.

Attempted action: The planner receives the current account state.

Expected outcome: The remaining plan does not require another egg or new flour production. It includes collecting the stored flour and obtaining milk before the remaining deliveries.

Invariant: Preserve delivery credit, recognize intermediate production, and do not confuse completed work with current possession.
