module RuneSprout.Tests.PlanningTests

open RuneSprout.Planner
open Xunit

// All records in this file are synthetic test worlds, never accepted OSRS knowledge.
let private home = LocationId "home"
let private complete = FactId "complete"
let private token = ResourceId "token"

let private activity id requirements consumes produces changes =
    { Id = ActivityId id
      Requirements = requirements
      Consumes = Map.ofList consumes
      Produces = Map.ofList produces
      Destination = None
      FactChanges = Map.ofList changes }

let private catalog activities costs =
    { Locations = Set.singleton home
      Facts = Set.singleton complete
      ResourceCapacities = Map.ofList [ token, 1L ]
      Goal = [ FactIs(complete, true) ]
      Activities = activities
      DurationEstimatesMs = Map.ofList costs }

let private start =
    { Location = home
      Facts = Map.ofList [ complete, false ]
      Resources = Map.ofList [ token, 0L ] }

let private proven = function
    | Ok(ProvenFastest(plan, report)) -> plan, report
    | other -> failwithf "Expected a proven fastest plan, got %A" other

let private stepIds plan = plan.Steps |> List.map (fun step -> step.ActivityId)

[<Fact>]
let ``complete route costs include resource preparation`` () =
    let world =
        catalog
            [ activity "free" [] [] [] [ complete, true ]
              activity "prepare" [] [] [ token, 1L ] []
              activity "paid" [] [ token, 1L ] [] [ complete, true ] ]
            [ ActivityId "free", 20L; ActivityId "prepare", 5L; ActivityId "paid", 8L ]

    let plan, _ = Planning.plan world start SearchBudget.defaultBudget |> proven

    Assert.Equal<ActivityId list>([ ActivityId "prepare"; ActivityId "paid" ], stepIds plan)
    Assert.Equal(13L, plan.TotalEstimatedMilliseconds)
    Assert.Equal(0L, plan.FinalKnowledge.Resources[token])
    Assert.True(plan.FinalKnowledge.Facts[complete])

[<Fact>]
let ``cutoff retains the cheapest discovered complete route before queue selection`` () =
    let world =
        catalog
            [ activity "a-expensive" [] [] [] [ complete, true ]
              activity "b-cheaper" [] [] [] [ complete, true ]
              activity "c-explore" [] [] [ token, 1L ] []
              activity "d-finish" [] [ token, 1L ] [] [ complete, true ] ]
            [ ActivityId "a-expensive", 20L; ActivityId "b-cheaper", 13L
              ActivityId "c-explore", 1L; ActivityId "d-finish", 1L ]

    match Planning.plan world start { MaxExpandedStates = 1 } with
    | Ok(BestFound(plan, report)) ->
        Assert.Equal<ActivityId list>([ ActivityId "b-cheaper" ], stepIds plan)
        Assert.Equal(13L, plan.TotalEstimatedMilliseconds)
        Assert.Equal({ ExpandedStates = 1; StoppingReason = ExpansionBudgetExhausted }, report)
    | other -> failwithf "Expected a complete best-found plan, got %A" other

[<Fact>]
let ``negative duration is invalid even on an unreachable activity`` () =
    let world =
        catalog
            [ activity "invalid" [ FactIs(complete, true) ] [] [] [] ]
            [ ActivityId "invalid", -1L ]

    match Planning.plan world start SearchBudget.defaultBudget with
    | Error errors -> Assert.Contains(InvalidInput(InvalidCatalog "Duration for ActivityId \"invalid\" must be nonnegative."), errors)
    | other -> failwithf "Expected invalid duration, got %A" other

[<Theory>]
[<InlineData(0)>]
[<InlineData(-1)>]
let ``search budget must be positive`` limit =
    Assert.Equal<Result<PlanningOutcome, PlanningError list>>(
        Error [ InvalidInput(InvalidSearchBudget limit) ],
        Planning.plan (catalog [] []) start { MaxExpandedStates = limit })

[<Theory>]
[<InlineData(0L)>]
[<InlineData(1L)>]
let ``route totals use checked integer milliseconds`` finalCost =
    let world =
        catalog
            [ activity "prepare" [] [] [ token, 1L ] []
              activity "finish" [] [ token, 1L ] [] [ complete, true ] ]
            [ ActivityId "prepare", System.Int64.MaxValue; ActivityId "finish", finalCost ]

    let result = Planning.plan world start SearchBudget.defaultBudget
    if finalCost = 0L then
        let plan, _ = proven result
        Assert.Equal(System.Int64.MaxValue, plan.TotalEstimatedMilliseconds)
    else
        Assert.Equal<Result<PlanningOutcome, PlanningError list>>(Error [ CostOverflow(ActivityId "finish") ], result)

[<Fact>]
let ``invalid model references and incomplete snapshots return input errors`` () =
    let finish = activity "finish" [] [] [] [ complete, true ]
    let valid = catalog [ finish ] [ finish.Id, 1L ]
    let invalidInputs =
        [ { valid with DurationEstimatesMs = Map.empty }, start
          { valid with DurationEstimatesMs = Map.add (ActivityId "missing") 0L valid.DurationEstimatesMs }, start
          { valid with Activities = [ finish; finish ] }, start
          { valid with Locations = Set.empty }, start
          { valid with Facts = Set.add (FactId "") valid.Facts }, start
          { valid with ResourceCapacities = Map.ofList [ token, -1L ] }, start
          { valid with Goal = [ FactIs(FactId "undeclared", true) ] }, start
          { valid with Activities = [ { finish with Requirements = [ AtLocation(LocationId "missing") ] } ] }, start
          { valid with Activities = [ { finish with Requirements = [ ResourceAtLeast(token, -1L) ] } ] }, start
          { valid with Activities = [ { finish with Destination = Some(LocationId "missing") } ] }, start
          { valid with Activities = [ { finish with FactChanges = Map.ofList [ FactId "missing", true ] } ] }, start
          { valid with Activities = [ { finish with Consumes = Map.ofList [ token, -1L ] } ] }, start
          { valid with Activities = [ { finish with Produces = Map.ofList [ ResourceId "missing", 1L ] } ] }, start
          valid, { start with Location = LocationId "missing" }
          valid, { start with Facts = Map.empty }
          valid, { start with Facts = Map.add (FactId "missing") false start.Facts }
          valid, { start with Resources = Map.empty }
          valid, { start with Resources = Map.ofList [ token, -1L ] }
          valid, { start with Resources = Map.ofList [ token, 2L ] }
          valid, { start with Resources = Map.add (ResourceId "missing") 0L start.Resources } ]

    for world, knowledge in invalidInputs do
        match Planning.plan world knowledge SearchBudget.defaultBudget with
        | Error errors ->
            Assert.NotEmpty errors
            Assert.All(errors, fun error -> match error with InvalidInput _ -> () | _ -> failwithf "%A" error)
        | other -> failwithf "Expected input error for %A / %A, got %A" world knowledge other

[<Fact>]
let ``the cheapest isolated method loses when its preparation is included`` () =
    let world =
        catalog
            [ activity "direct" [] [] [] [ complete, true ]
              activity "prepare" [] [] [ token, 1L ] []
              activity "cheap-isolated" [] [ token, 1L ] [] [ complete, true ] ]
            [ ActivityId "direct", 3L; ActivityId "prepare", 10L; ActivityId "cheap-isolated", 1L ]
    let plan, _ = Planning.plan world start SearchBudget.defaultBudget |> proven
    Assert.Equal<ActivityId list>([ ActivityId "direct" ], stepIds plan)
    Assert.Equal(3L, plan.TotalEstimatedMilliseconds)

[<Fact>]
let ``budget without a route differs from exhaustive modeled failure`` () =
    let world = catalog [ activity "acquire" [] [] [ token, 1L ] [] ] [ ActivityId "acquire", 0L ]
    Assert.Equal<Result<PlanningOutcome, PlanningError list>>(
        Ok(NoCompleteModeledRouteFound { ExpandedStates = 1; StoppingReason = ExpansionBudgetExhausted }),
        Planning.plan world start { MaxExpandedStates = 1 })
    Assert.Equal<Result<PlanningOutcome, PlanningError list>>(
        Ok(NoCompleteModeledRouteFound { ExpandedStates = 2; StoppingReason = FrontierExhausted }),
        Planning.plan world start { MaxExpandedStates = 2 })

[<Fact>]
let ``selecting a minimum goal needs no outgoing expansion even at the limit`` () =
    let world = catalog [ activity "finish" [] [] [] [ complete, true ] ] [ ActivityId "finish", 1L ]
    let plan, report = Planning.plan world start { MaxExpandedStates = 1 } |> proven
    Assert.Equal(1L, plan.TotalEstimatedMilliseconds)
    Assert.Equal({ ExpandedStates = 1; StoppingReason = MinimumCostGoalSelected }, report)

[<Fact>]
let ``already completed goal has zero remaining work and no expansion`` () =
    let world = catalog [ activity "future" [] [] [ token, 1L ] [] ] [ ActivityId "future", 1L ]
    let knowledge = { start with Facts = Map.ofList [ complete, true ] }
    let plan, report = Planning.plan world knowledge SearchBudget.defaultBudget |> proven
    Assert.Empty plan.Steps
    Assert.Equal(0L, plan.TotalEstimatedMilliseconds)
    Assert.Equal(knowledge, plan.FinalKnowledge)
    Assert.Equal(0, report.ExpandedStates)

[<Fact>]
let ``goal-dependent access cannot supply a prerequisite and zero cost cycles terminate`` () =
    let world =
        catalog
            [ activity "circular-access" [ FactIs(complete, true) ] [] [ token, 1L ] []
              activity "finish" [] [ token, 1L ] [] [ complete, true ]
              activity "wait" [] [] [] [] ]
            [ ActivityId "circular-access", 0L; ActivityId "finish", 0L; ActivityId "wait", 0L ]
    Assert.Equal<Result<PlanningOutcome, PlanningError list>>(
        Ok(NoCompleteModeledRouteFound { ExpandedStates = 1; StoppingReason = FrontierExhausted }),
        Planning.plan world start SearchBudget.defaultBudget)

[<Fact>]
let ``future benefits are not appended after goal completion`` () =
    let world =
        catalog
            [ activity "finish" [] [] [] [ complete, true ]
              activity "future-benefit" [ FactIs(complete, true) ] [] [ token, 1L ] [] ]
            [ ActivityId "finish", System.Int64.MaxValue; ActivityId "future-benefit", 1L ]
    // Expanding the goal would overflow. The optimization horizon must end first.
    let plan, report = Planning.plan world start SearchBudget.defaultBudget |> proven
    Assert.Equal<ActivityId list>([ ActivityId "finish" ], stepIds plan)
    Assert.Equal(0L, plan.FinalKnowledge.Resources[token])
    Assert.Equal(1, report.ExpandedStates)

[<Fact>]
let ``stable activity ordering resolves equal costs independently of input order`` () =
    let world =
        catalog
            [ activity "z-last" [] [] [] [ complete, true ]
              activity "a-first" [] [] [] [ complete, true ] ]
            [ ActivityId "z-last", 1L; ActivityId "a-first", 1L ]
    let expected = Planning.plan world start SearchBudget.defaultBudget
    for _ in 1..10 do
        Assert.Equal<Result<PlanningOutcome, PlanningError list>>(
            expected, Planning.plan { world with Activities = List.rev world.Activities } start SearchBudget.defaultBudget)
    let plan, _ = proven expected
    Assert.Equal<ActivityId list>([ ActivityId "a-first" ], stepIds plan)

[<Fact>]
let ``cost comparisons retain millisecond differences`` () =
    let world =
        catalog
            [ activity "a-slower" [] [] [] [ complete, true ]
              activity "z-faster" [] [] [] [ complete, true ] ]
            [ ActivityId "a-slower", 1001L; ActivityId "z-faster", 1000L ]
    let plan, _ = Planning.plan world start SearchBudget.defaultBudget |> proven
    Assert.Equal<ActivityId list>([ ActivityId "z-faster" ], stepIds plan)

[<Fact>]
let ``resource capacity is a modeled rule not a truncation or arithmetic wrap`` () =
    let world =
        { catalog
              [ activity "overfill" [] [] [ token, System.Int64.MaxValue ] [ complete, true ] ]
              [ ActivityId "overfill", 1L ] with
            ResourceCapacities = Map.ofList [ token, System.Int64.MaxValue ] }
    let knowledge = { start with Resources = Map.ofList [ token, 1L ] }
    Assert.Equal<Result<PlanningOutcome, PlanningError list>>(
        Ok(NoCompleteModeledRouteFound { ExpandedStates = 1; StoppingReason = FrontierExhausted }),
        Planning.plan world knowledge SearchBudget.defaultBudget)

[<Fact>]
let ``default budget stops at one hundred thousand outgoing evaluations`` () =
    let world =
        { catalog [ activity "increment" [] [] [ token, 1L ] [] ] [ ActivityId "increment", 0L ] with
            ResourceCapacities = Map.ofList [ token, 100_001L ] }
    Assert.Equal<Result<PlanningOutcome, PlanningError list>>(
        Ok(NoCompleteModeledRouteFound { ExpandedStates = 100_000; StoppingReason = ExpansionBudgetExhausted }),
        Planning.plan world start SearchBudget.defaultBudget)

[<Fact>]
let ``improved states supersede stale queue entries without spending expansions`` () =
    let intermediate, detour = LocationId "intermediate", LocationId "detour"
    let travel name source target =
        { activity name [ AtLocation source ] [] [] [] with Destination = Some target }
    let world =
        { catalog
              [ travel "direct" home intermediate
                travel "detour" home detour
                travel "improve" detour intermediate
                activity "finish" [ AtLocation intermediate ] [] [] [ complete, true ] ]
              [ ActivityId "direct", 10L; ActivityId "detour", 1L
                ActivityId "improve", 1L; ActivityId "finish", 20L ] with
            Locations = Set.ofList [ home; intermediate; detour ] }
    let plan, report = Planning.plan world start { MaxExpandedStates = 3 } |> proven
    Assert.Equal(22L, plan.TotalEstimatedMilliseconds)
    Assert.Equal(3, report.ExpandedStates)
    Assert.Equal<ActivityId list>([ ActivityId "detour"; ActivityId "improve"; ActivityId "finish" ], stepIds plan)
