# Cook's Assistant research

The sources below establish game mechanics, not a fastest route. Evidence comes from OSRS Wiki and its reproduced game dialogue. No live-client test or timing proof took place.

## Mechanics

The quest requires one egg, one bucket of milk, and one pot of flour. It has no skill requirements, prerequisite quests, or enemies to defeat. The Cook in the Lumbridge Castle kitchen accepts the ingredients. The player does not bake a cake. [Quest guide](https://oldschool.runescape.wiki/w/Cook%27s_Assistant)

The player can collect all ingredients before accepting the quest and then complete it in the initial conversation. The Cook also accepts separate deliveries and remembers each ingredient delivered. Collection therefore does not depend on quest acceptance. [Game dialogue transcript](https://oldschool.runescape.wiki/w/Transcript:Cook%27s_Assistant)

A free collection route exists. A pot spawns in the castle kitchen, and a bucket spawns in the cellar. An egg spawns in a nearby chicken coop. An empty bucket becomes a bucket of milk when used on a dairy cow. Grain processed at the mill becomes flour that the player collects with an empty pot. Money-making is not compulsory. [Quest guide](https://oldschool.runescape.wiki/w/Cook%27s_Assistant)

The mill stores produced flour outside the player's inventory. A flour bin holds up to 30 pots' worth. Its quantity is shared between windmills except Harmony Island. Existing flour can remove the need to gather and process more grain. [Flour bin](https://oldschool.runescape.wiki/w/Flour_bin)

## Alternative methods and resource effects

Lumbridge General Store lists empty pots at one coin and buckets at two coins. Other shops sell filled ingredients. Port Sarim's Food Store lists flour, and Grand Tree Groceries lists flour and milk. Access, stock, prices, position, and available money affect whether those methods are useful. These are alternatives, not mandatory steps. [Lumbridge General Store](https://oldschool.runescape.wiki/w/Lumbridge_General_Store), [Pot of flour](https://oldschool.runescape.wiki/w/Pot_of_flour), [Bucket of milk](https://oldschool.runescape.wiki/w/Bucket_of_milk)

An ironman cannot use the Grand Exchange or ordinary player trading to buy the ingredients. Shop restrictions also affect eligible purchases. [Ironman rules](https://oldschool.runescape.wiki/w/Ironman_Mode)

Filling a pot or bucket changes the inventory item. The empty container does not remain separately available. The mill and dairy cow are facilities rather than carried resources. Whether ordinary empty containers return after delivery remains unverified. Do not assume a return effect. [Flour](https://oldschool.runescape.wiki/w/Pot_of_flour), [Milk](https://oldschool.runescape.wiki/w/Bucket_of_milk)

The quest guide also documents delivery from a bottomless bucket of milk. Whether the first model includes this alternative remains undecided. [Quest guide](https://oldschool.runescape.wiki/w/Cook%27s_Assistant)

The Culinaromancer's Chest is an invalid supplier for an unfinished Cook's Assistant on an ordinary account. Access requires progress in Recipe for Disaster, which depends on Cook's Assistant. The planner must reject this circular route. [Chest access](https://oldschool.runescape.wiki/w/Culinaromancer%27s_Chest), [Quest dependencies](https://oldschool.runescape.wiki/w/Cook%27s_Assistant)

## Modeling implications to review

- Distinguish possession, location, and delivery credit. An absent item can be already delivered rather than missing.
- Account for intermediate production outside inventory and bank.
- Do not turn the order in a quest guide into mandatory dependencies.
- Distinguish methods that consume or transform resources from facilities that remain available.
- Reject methods whose access depends on the goal they are meant to enable.
- Treat travel, inventory capacity, shop stock, and spawn availability as possible route-changing inputs.

These findings informed the design interview. The agreed behavior for movement, uncertainty, optimality claims, and initial verification is recorded in the [v0 specification](../spec.md). The research establishes mechanics rather than a measured fastest route.

## Verification limits

Cook's Assistant can test acquisition alternatives, production, existing resources, and partial progress. Skill-training batches require a separate example. This research does not establish a timed money-making detour that beats free collection. The separate hypothetical scenarios cover those rules until further game-specific cases are selected.

The parent agent directly reviewed the quest guide, game dialogue transcript, and flour bin page. The read-only research agent supplied the additional cited source findings. Its ID is `68b34ed9-2b37-4680-8926-adc1f45d119d`.
