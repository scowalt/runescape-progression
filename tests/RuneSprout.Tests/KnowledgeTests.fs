module RuneSprout.Tests.KnowledgeTests

open RuneSprout.Planner
open RuneSprout.Tests.SyntheticFixtures
open Xunit

let private home, field = LocationId "home", LocationId "field"
let private complete, eligible, accepted = FactId "complete", FactId "members-ironman", FactId "quest-accepted"
let private space, item = ResourceId "inventory-space", ResourceId "item"
let private banked, stored, coins = ResourceId "banked-item", ResourceId "stored-product", ResourceId "banked-coins"

let private world facts limits goal activities costs =
    { Locations = Set.ofList [ home; field ]
      Facts = Set.ofList (complete :: facts)
      ResourceLimits = Map.ofList limits
      Goal = goal
      Activities = activities
      DurationEstimatesMs = costs |> List.map (fun (id, cost) -> ActivityId id, cost) |> Map.ofList }

let private knowledge location facts resources =
    { Location = location
      Facts = Map.ofList ((complete, Known false) :: facts)
      Resources = Map.ofList resources }

let private planFrom world knowledge = Planning.plan world (snapshot knowledge) SearchBudget.defaultBudget

let private proven = function
    | Ok(ProvenFastest(plan, report)) -> plan, report
    | other -> failwithf "Expected a proven fastest plan, got %A" other

let private stepIds plan = plan.Steps |> List.map (fun step -> step.ActivityId)

let private steps names = names |> List.map ActivityId

let private travel name source target =
    { activity name [ AtLocation source ] [] [] [] with Destination = Some target }

let private pickupWorld goal =
    world
        []
        [ space, Capacity 2L; item, OccupiesSpace space ]
        goal
        [ activity "pickup" [] [] [ item, 1L ] []
          activity "finish" [ ResourceAtLeast(item, 1L) ] [] [] [ complete, true ] ]
        [ "pickup", 5L; "finish", 1L ]

[<Fact>]
let ``known zero differs from an unknown count and a pickup establishes only a lower bound`` () =
    let pickup = pickupWorld [ FactIs(complete, true) ]

    let knownPlan, _ =
        planFrom pickup (knowledge (Known home) [] [ space, Exactly 2L; item, Exactly 0L ]) |> proven
    Assert.Equal<ActivityId list>(steps [ "pickup"; "finish" ], stepIds knownPlan)
    Assert.Equal(Exactly 1L, knownPlan.FinalKnowledge.Resources[item])
    Assert.Equal(Exactly 1L, knownPlan.FinalKnowledge.Resources[space])

    match planFrom pickup (knowledge (Known home) [] [ space, AtLeast 1L; item, AtLeast 0L ]) with
    | Ok(BestFound(plan, report, mayRelyOn)) ->
        Assert.Equal<ActivityId list>(steps [ "pickup"; "finish" ], stepIds plan)
        Assert.Equal(6L, plan.TotalEstimatedMilliseconds)
        Assert.Equal(AtLeast 1L, plan.FinalKnowledge.Resources[item])
        Assert.Equal(AtLeast 0L, plan.FinalKnowledge.Resources[space])
        Assert.Equal(MinimumCostGoalSelected, report.StoppingReason)
        Assert.Equal<AccountFact list>([ ResourceQuantity item ], mayRelyOn)
    | other -> failwithf "Expected a best-found plan that may be beaten by an owned item, got %A" other

[<Fact>]
let ``unknown inventory space is not an empty inventory`` () =
    let pickup = pickupWorld [ ResourceAtLeast(item, 1L) ]

    match planFrom pickup (knowledge (Known home) [] [ space, AtLeast 0L; item, Exactly 0L ]) with
    | Ok(NeedsAccountInformation(necessary, report)) ->
        Assert.Equal<AccountFact list>([ ResourceQuantity space ], necessary)
        Assert.Equal(FrontierExhausted, report.StoppingReason)
    | other -> failwithf "Expected a request for inventory space, got %A" other

    Assert.Equal<Result<PlanningOutcome, PlanningError list>>(
        Ok(NoCompleteModeledRouteFound({ ExpandedStates = 1; StoppingReason = FrontierExhausted }, [])),
        planFrom pickup (knowledge (Known home) [] [ space, Exactly 0L; item, Exactly 0L ]))

    let plan, _ = planFrom pickup (knowledge (Known home) [] [ space, AtLeast 1L; item, Exactly 0L ]) |> proven
    Assert.Equal<ActivityId list>(steps [ "pickup" ], stepIds plan)
    Assert.Equal(AtLeast 0L, plan.FinalKnowledge.Resources[space])

[<Fact>]
let ``a container transformation needs no free slot and leaves no empty container`` () =
    let container, filled = ResourceId "container", ResourceId "filled-container"
    let fill goal =
        world
            []
            [ space, Capacity 1L; container, OccupiesSpace space; filled, OccupiesSpace space ]
            goal
            [ activity "fill" [] [ container, 1L ] [ filled, 1L ] []
              activity "finish" [ ResourceAtLeast(filled, 1L) ] [] [] [ complete, true ] ]
            [ "fill", 2L; "finish", 1L ]
    let full = knowledge (Known home) [] [ space, Exactly 0L; container, Exactly 1L; filled, Exactly 0L ]

    let plan, _ = planFrom (fill [ FactIs(complete, true) ]) full |> proven
    Assert.Equal<ActivityId list>(steps [ "fill"; "finish" ], stepIds plan)
    Assert.Equal(Exactly 0L, plan.FinalKnowledge.Resources[container])
    Assert.Equal(Exactly 1L, plan.FinalKnowledge.Resources[filled])
    Assert.Equal(Exactly 0L, plan.FinalKnowledge.Resources[space])

    Assert.Equal<Result<PlanningOutcome, PlanningError list>>(
        Ok(NoCompleteModeledRouteFound({ ExpandedStates = 3; StoppingReason = FrontierExhausted }, [])),
        planFrom (fill [ FactIs(complete, true); ResourceAtLeast(container, 1L) ]) full)

[<Fact>]
let ``bank inventory and stored production stay separate and stored production is reused`` () =
    let supply =
        world
            []
            [ space, Capacity 1L; item, OccupiesSpace space; banked, Capacity 5L; stored, Capacity 1L ]
            [ FactIs(complete, true) ]
            [ activity "produce" [] [] [ stored, 1L ] []
              activity "collect" [] [ stored, 1L ] [ item, 1L ] []
              activity "withdraw" [] [ banked, 1L ] [ item, 1L ] []
              activity "deliver" [] [ item, 1L ] [] [ complete, true ] ]
            [ "produce", 10L; "collect", 2L; "withdraw", 4L; "deliver", 1L ]
    let state storedCount bankedCount freeSlots =
        knowledge (Known home) [] [ space, Exactly freeSlots; item, Exactly 0L; banked, Exactly bankedCount; stored, Exactly storedCount ]

    let reused, _ = planFrom supply (state 1L 0L 1L) |> proven
    Assert.Equal<ActivityId list>(steps [ "collect"; "deliver" ], stepIds reused)
    Assert.Equal(3L, reused.TotalEstimatedMilliseconds)
    Assert.Equal(Exactly 1L, reused.FinalKnowledge.Resources[space])

    let withdrawn, _ = planFrom supply (state 0L 1L 1L) |> proven
    Assert.Equal<ActivityId list>(steps [ "withdraw"; "deliver" ], stepIds withdrawn)
    Assert.Equal(Exactly 0L, withdrawn.FinalKnowledge.Resources[banked])

    let produced, _ = planFrom supply (state 0L 0L 1L) |> proven
    Assert.Equal<ActivityId list>(steps [ "produce"; "collect"; "deliver" ], stepIds produced)
    Assert.Equal(13L, produced.TotalEstimatedMilliseconds)

    match planFrom supply (state 1L 1L 0L) with
    | Ok(NoCompleteModeledRouteFound(report, [])) -> Assert.Equal(FrontierExhausted, report.StoppingReason)
    | other -> failwithf "Expected a full inventory to block both supplies, got %A" other

[<Fact>]
let ``delivery credit is preserved and acquisition does not wait for quest acceptance`` () =
    let deliveredA, deliveredB = FactId "delivered-a", FactId "delivered-b"
    let a, b = ResourceId "a", ResourceId "b"
    let quest goal =
        world
            [ accepted; deliveredA; deliveredB ]
            [ space, Capacity 2L; a, OccupiesSpace space; b, OccupiesSpace space ]
            goal
            [ activity "accept" [ FactIs(accepted, false) ] [] [] [ accepted, true ]
              activity "pick-a" [] [] [ a, 1L ] []
              activity "pick-b" [] [] [ b, 1L ] []
              activity "deliver-a" [ FactIs(accepted, true) ] [ a, 1L ] [] [ deliveredA, true ]
              activity "deliver-b" [ FactIs(accepted, true) ] [ b, 1L ] [] [ deliveredB, true ]
              activity "finish" [ FactIs(deliveredA, true); FactIs(deliveredB, true) ] [] [] [ complete, true ] ]
            [ "accept", 1L; "pick-a", 3L; "pick-b", 3L; "deliver-a", 1L; "deliver-b", 1L; "finish", 1L ]
    let progress acceptance creditA =
        knowledge
            (Known home)
            [ accepted, acceptance; deliveredA, creditA; deliveredB, Known false ]
            [ space, Exactly 2L; a, Exactly 0L; b, Exactly 0L ]

    let credited, _ = planFrom (quest [ FactIs(complete, true) ]) (progress (Known true) (Known true)) |> proven
    Assert.Equal<ActivityId list>(steps [ "pick-b"; "deliver-b"; "finish" ], stepIds credited)
    Assert.Equal(Exactly 0L, credited.FinalKnowledge.Resources[a])

    let gathered, _ = planFrom (quest [ ResourceAtLeast(a, 1L) ]) (progress Unknown (Known false)) |> proven
    Assert.Equal<ActivityId list>(steps [ "pick-a" ], stepIds gathered)
    Assert.Equal(Unknown, gathered.FinalKnowledge.Facts[accepted])

[<Fact>]
let ``a snapshot establishing completion needs no location or bank information`` () =
    let errand =
        world
            []
            [ banked, Capacity 5L ]
            [ FactIs(complete, true) ]
            [ { activity "finish" [ AtLocation field ] [ banked, 1L ] [] [ complete, true ] with Destination = Some home } ]
            [ "finish", 1L ]
    let finished = { knowledge Unknown [] [ banked, AtLeast 0L ] with Facts = Map.ofList [ complete, Known true ] }

    let plan, report = planFrom errand finished |> proven
    Assert.Empty plan.Steps
    Assert.Equal(0L, plan.TotalEstimatedMilliseconds)
    Assert.Equal(finished, plan.FinalKnowledge)
    Assert.Equal({ ExpandedStates = 0; StoppingReason = MinimumCostGoalSelected }, report)

let private errand =
    world
        [ eligible ]
        [ space, Capacity 1L; item, OccupiesSpace space; coins, Capacity 100L ]
        [ FactIs(complete, true) ]
        [ travel "go-field" home field
          travel "go-home" field home
          activity "pick" [ AtLocation field; FactIs(eligible, true) ] [] [ item, 1L ] []
          activity "finish" [ AtLocation home ] [ item, 1L ] [] [ complete, true ] ]
        [ "go-field", 2L; "go-home", 2L; "pick", 1L; "finish", 1L ]

let private errandStart location eligibility =
    knowledge location [ eligible, eligibility ] [ space, Exactly 1L; item, Exactly 0L; coins, AtLeast 0L ]

[<Fact>]
let ``needs account information names only the essential unknown facts`` () =
    match planFrom errand (errandStart Unknown (Known true)) with
    | Ok(NeedsAccountInformation(necessary, report)) ->
        Assert.Equal<AccountFact list>([ CurrentLocation ], necessary)
        Assert.Equal(FrontierExhausted, report.StoppingReason)
    | other -> failwithf "Expected a request for the starting location, got %A" other

    match planFrom errand (errandStart Unknown Unknown) with
    | Ok(NeedsAccountInformation(necessary, _)) ->
        Assert.Equal<AccountFact list>([ CurrentLocation; FactValue eligible ], necessary)
    | other -> failwithf "Expected requests for location and eligibility, got %A" other

[<Fact>]
let ``an explicit assumption supplies a missing fact visibly`` () =
    let assumed =
        Planning.plan
            errand
            { Knowledge = errandStart Unknown (Known true); Assumptions = [ AssumeLocation home ] }
            SearchBudget.defaultBudget
    let plan, _ = proven assumed
    Assert.Equal<Assumption list>([ AssumeLocation home ], plan.Assumptions)
    Assert.Equal<ActivityId list>(steps [ "go-field"; "pick"; "go-home"; "finish" ], stepIds plan)
    Assert.Equal(6L, plan.TotalEstimatedMilliseconds)
    Assert.Equal(AtLeast 0L, plan.FinalKnowledge.Resources[coins])

    let unassumed, _ = planFrom errand (errandStart (Known home) (Known true)) |> proven
    Assert.Empty unassumed.Assumptions

[<Fact>]
let ``unknown account facts differ from absent methods and budget limits`` () =
    let missingMethod =
        world [] [ space, Capacity 1L; item, OccupiesSpace space ] [ ResourceAtLeast(item, 1L) ] [ travel "wander" home field ] [ "wander", 1L ]

    match planFrom missingMethod (knowledge Unknown [] [ space, AtLeast 0L; item, Exactly 0L ]) with
    | Ok(NoCompleteModeledRouteFound(report, [])) -> Assert.Equal(FrontierExhausted, report.StoppingReason)
    | other -> failwithf "Expected a missing method rather than missing account facts, got %A" other

    match planFrom missingMethod (knowledge Unknown [] [ space, AtLeast 0L; item, AtLeast 0L ]) with
    | Ok(NeedsAccountInformation(necessary, _)) -> Assert.Equal<AccountFact list>([ ResourceQuantity item ], necessary)
    | other -> failwithf "Expected a request for the owned quantity, got %A" other

    Assert.Equal<Result<PlanningOutcome, PlanningError list>>(
        Ok(NoCompleteModeledRouteFound({ ExpandedStates = 1; StoppingReason = ExpansionBudgetExhausted }, [])),
        Planning.plan errand (snapshot (errandStart Unknown (Known true))) { MaxExpandedStates = 1 })

[<Fact>]
let ``unestablished essentiality is reported as uncertainty`` () =
    let left, right = FactId "left-open", FactId "right-open"
    let either =
        world
            [ left; right ]
            []
            [ FactIs(complete, true) ]
            [ activity "via-left" [ FactIs(left, true) ] [] [] [ complete, true ]
              activity "via-right" [ FactIs(right, true) ] [] [] [ complete, true ] ]
            [ "via-left", 1L; "via-right", 1L ]

    match planFrom either (knowledge (Known home) [ left, Unknown; right, Unknown ] []) with
    | Ok(NoCompleteModeledRouteFound(report, mayRelyOn)) ->
        Assert.Equal(FrontierExhausted, report.StoppingReason)
        Assert.Equal<AccountFact list>([ FactValue left; FactValue right ], mayRelyOn)
    | other -> failwithf "Expected an uncertain blocker, got %A" other

[<Fact>]
let ``one route never resolves an unknown two different ways`` () =
    let flag, prepared = FactId "flag", FactId "prepared"
    let contradictory first second shortcut =
        world
            [ flag; prepared ]
            []
            [ FactIs(complete, true) ]
            [ activity "prepare" [ first ] [] [] [ prepared, true ]
              activity "finish" [ second; FactIs(prepared, true) ] [] [] [ complete, true ]
              yield! shortcut |> List.map (fun name -> activity name [] [] [] [ complete, true ]) ]
            [ "prepare", 1L; "finish", 1L; yield! shortcut |> List.map (fun name -> name, 10L) ]
    let start = knowledge Unknown [ flag, Unknown; prepared, Known false ] []

    for first, second in [ FactIs(flag, true), FactIs(flag, false); AtLocation home, AtLocation field ] do
        match planFrom (contradictory first second []) start with
        | Ok(NoCompleteModeledRouteFound(report, [])) -> Assert.Equal(FrontierExhausted, report.StoppingReason)
        | other -> failwithf "Expected no route in any consistent account, got %A" other

        let plan, _ = planFrom (contradictory first second [ "shortcut" ]) start |> proven
        Assert.Equal<ActivityId list>(steps [ "shortcut" ], stepIds plan)

[<Fact>]
let ``an unknown count is never assumed larger than its storage could have held`` () =
    let stock = ResourceId "stock"
    let spend =
        world
            []
            [ stock, Capacity 2L ]
            [ FactIs(complete, true); ResourceAtLeast(stock, 2L) ]
            [ activity "spend" [] [ stock, 1L ] [] [ complete, true ] ]
            [ "spend", 1L ]

    match planFrom spend (knowledge (Known home) [] [ stock, AtLeast 0L ]) with
    | Ok(NoCompleteModeledRouteFound(report, [])) -> Assert.Equal(FrontierExhausted, report.StoppingReason)
    | other -> failwithf "Expected no route in any consistent account, got %A" other

    let bigger = { spend with ResourceLimits = Map.ofList [ stock, Capacity 3L ] }
    match planFrom bigger (knowledge (Known home) [] [ stock, AtLeast 0L ]) with
    | Ok(NeedsAccountInformation(necessary, _)) -> Assert.Equal<AccountFact list>([ ResourceQuantity stock ], necessary)
    | other -> failwithf "Expected a request for the stock count, got %A" other

[<Fact>]
let ``room freed from an unknown count is guaranteed room`` () =
    let stock, spent = ResourceId "stock", FactId "spent"
    let refill =
        world
            [ spent ]
            [ stock, Capacity 2L ]
            [ FactIs(complete, true) ]
            [ activity "spend" [] [ stock, 1L ] [] [ spent, true ]
              activity "refill" [ FactIs(spent, true) ] [] [ stock, 1L ] [ complete, true ] ]
            [ "spend", 1L; "refill", 1L ]

    let plan, _ = planFrom refill (knowledge (Known home) [ spent, Known false ] [ stock, AtLeast 1L ]) |> proven
    Assert.Equal<ActivityId list>(steps [ "spend"; "refill" ], stepIds plan)
    Assert.Equal(AtLeast 1L, plan.FinalKnowledge.Resources[stock])

[<Fact>]
let ``an unknown location in a one-location world is not missing information`` () =
    let errand =
        { world [] [] [ FactIs(complete, true) ] [ activity "finish" [ AtLocation home ] [] [] [ complete, true ] ] [ "finish", 1L ] with
            Locations = Set.singleton home }

    let plan, _ = planFrom errand (knowledge Unknown [] []) |> proven
    Assert.Equal<ActivityId list>(steps [ "finish" ], stepIds plan)

let private bankChoice =
    world
        []
        [ space, Capacity 1L; item, OccupiesSpace space; banked, Capacity 5L; coins, Capacity 100L ]
        [ FactIs(complete, true) ]
        [ activity "gather" [] [] [ item, 1L ] []
          activity "withdraw" [] [ banked, 1L ] [ item, 1L ] []
          activity "deliver" [] [ item, 1L ] [] [ complete, true ] ]
        [ "gather", 10L; "withdraw", 3L; "deliver", 1L ]

let private bankState bankedCount =
    knowledge (Known home) [] [ space, Exactly 1L; item, Exactly 0L; banked, bankedCount; coins, AtLeast 0L ]

[<Fact>]
let ``unknown bank contents withhold proven fastest without becoming zero`` () =
    match planFrom bankChoice (bankState (AtLeast 0L)) with
    | Ok(BestFound(plan, report, mayRelyOn)) ->
        Assert.Equal<ActivityId list>(steps [ "gather"; "deliver" ], stepIds plan)
        Assert.Equal(11L, plan.TotalEstimatedMilliseconds)
        Assert.Equal(AtLeast 0L, plan.FinalKnowledge.Resources[banked])
        Assert.Equal(MinimumCostGoalSelected, report.StoppingReason)
        Assert.Equal<AccountFact list>([ ResourceQuantity banked ], mayRelyOn)
    | other -> failwithf "Expected a best-found acquisition route, got %A" other

    let retrieved, _ = planFrom bankChoice (bankState (Exactly 1L)) |> proven
    Assert.Equal<ActivityId list>(steps [ "withdraw"; "deliver" ], stepIds retrieved)
    Assert.Equal(4L, retrieved.TotalEstimatedMilliseconds)
    Assert.Equal(AtLeast 0L, retrieved.FinalKnowledge.Resources[coins])

    let gathered, _ = planFrom bankChoice (bankState (Exactly 0L)) |> proven
    Assert.Equal<ActivityId list>(steps [ "gather"; "deliver" ], stepIds gathered)

[<Fact>]
let ``a consistent assumption refines a lower bound and is repeated in the plan`` () =
    let assumption = AssumeResource(banked, AtLeast 1L)
    let plan, _ =
        Planning.plan bankChoice { Knowledge = bankState (AtLeast 0L); Assumptions = [ assumption ] } SearchBudget.defaultBudget
        |> proven
    Assert.Equal<ActivityId list>(steps [ "withdraw"; "deliver" ], stepIds plan)
    Assert.Equal<Assumption list>([ assumption ], plan.Assumptions)
    Assert.Equal(AtLeast 0L, plan.FinalKnowledge.Resources[banked])

[<Fact>]
let ``assumptions that contradict known facts or each other are rejected`` () =
    let flag = FactId "flag"
    let base' =
        knowledge (Known field) [ flag, Unknown ] [ space, AtLeast 0L; item, AtLeast 0L; banked, AtLeast 1L; coins, Exactly 2L ]
    let catalog =
        world
            [ flag ]
            [ space, Capacity 1L; item, OccupiesSpace space; banked, Capacity 5L; coins, Capacity 100L ]
            [ FactIs(complete, true) ]
            []
            []
    let contradictions =
        [ [ AssumeLocation home ], AssumeLocation home
          [ AssumeFact(complete, true) ], AssumeFact(complete, true)
          [ AssumeResource(banked, Exactly 0L) ], AssumeResource(banked, Exactly 0L)
          [ AssumeResource(coins, AtLeast 3L) ], AssumeResource(coins, AtLeast 3L)
          [ AssumeResource(coins, Exactly 1L) ], AssumeResource(coins, Exactly 1L)
          [ AssumeResource(banked, AtLeast 6L) ], AssumeResource(banked, AtLeast 6L)
          [ AssumeFact(flag, true); AssumeFact(flag, false) ], AssumeFact(flag, false)
          [ AssumeResource(space, Exactly 1L); AssumeResource(item, AtLeast 1L) ], AssumeResource(item, AtLeast 1L) ]

    for assumptions, rejected in contradictions do
        Assert.Equal<Result<PlanningOutcome, PlanningError list>>(
            Error [ InvalidInput(ContradictoryAssumption rejected) ],
            Planning.plan catalog { Knowledge = base'; Assumptions = assumptions } SearchBudget.defaultBudget)

    let agreeing = [ AssumeLocation field; AssumeResource(coins, AtLeast 2L); AssumeResource(banked, Exactly 3L) ]
    match Planning.plan catalog { Knowledge = base'; Assumptions = agreeing } SearchBudget.defaultBudget with
    | Ok(NoCompleteModeledRouteFound _) -> ()
    | other -> failwithf "Expected agreeing assumptions to be accepted, got %A" other

[<Fact>]
let ``invalid knowledge assumptions and space declarations return input errors`` () =
    let valid = pickupWorld [ FactIs(complete, true) ]
    let start = knowledge (Known home) [] [ space, AtLeast 0L; item, AtLeast 0L ]
    let pickup = valid.Activities.Head
    let invalidInputs =
        [ valid, { Knowledge = start; Assumptions = [ AssumeLocation(LocationId "missing") ] }
          valid, { Knowledge = start; Assumptions = [ AssumeFact(FactId "missing", true) ] }
          valid, { Knowledge = start; Assumptions = [ AssumeResource(ResourceId "missing", AtLeast 0L) ] }
          valid, { Knowledge = start; Assumptions = [ AssumeResource(item, AtLeast -1L) ] }
          valid, { Knowledge = start; Assumptions = [ AssumeResource(item, Exactly -1L) ] }
          valid, snapshot { start with Resources = Map.ofList [ space, Exactly 2L; item, Exactly 1L ] }
          valid, snapshot { start with Resources = Map.ofList [ space, AtLeast 1L; item, AtLeast 2L ] }
          valid, snapshot { start with Resources = Map.ofList [ space, Exactly 3L; item, AtLeast 0L ] }
          { valid with ResourceLimits = Map.ofList [ space, Capacity 2L; item, OccupiesSpace(ResourceId "missing") ] }, snapshot start
          { valid with ResourceLimits = Map.ofList [ space, Capacity 2L; item, OccupiesSpace item ] }, snapshot start
          { valid with ResourceLimits = Map.ofList [ space, OccupiesSpace item; item, OccupiesSpace space ] }, snapshot start
          { valid with Activities = [ { pickup with Produces = Map.ofList [ space, 1L ] } ] }, snapshot start
          { valid with Activities = [ { pickup with Consumes = Map.ofList [ space, 1L ] } ] }, snapshot start ]

    for catalog, accountSnapshot in invalidInputs do
        match Planning.plan catalog accountSnapshot SearchBudget.defaultBudget with
        | Error errors ->
            Assert.NotEmpty errors
            Assert.All(errors, fun error -> match error with InvalidInput _ -> () | _ -> failwithf "%A" error)
        | other -> failwithf "Expected input error for %A / %A, got %A" catalog accountSnapshot other
