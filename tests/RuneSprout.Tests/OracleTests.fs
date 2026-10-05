module RuneSprout.Tests.OracleTests

open RuneSprout.Planner
open RuneSprout.Tests.SyntheticFixtures
open Xunit

let private enumerateMinimum initial goal (successors: 'state -> (string * int64 * 'state) list) =
    let rec paths visited state total =
        seq {
            if goal state then
                yield total
            else
                for _, cost, next in successors state do
                    if not (Set.contains next visited) then
                        yield! paths (Set.add next visited) next (total + bigint cost)
        }
    paths (Set.singleton initial) initial 0I
    |> Seq.fold (fun best cost -> Some(match best with None -> cost | Some previous -> min previous cost)) None

let private replay initial goal successors (plan: PersonalPlan) =
    let state, total =
        plan.Steps
        |> List.fold (fun (state, total) step ->
            Assert.False(goal state, "The route must stop at the current goal.")
            let (ActivityId id) = step.ActivityId
            let matches = successors state |> List.filter (fun (name, _, _) -> name = id)
            match matches with
            | [ _, cost, next ] ->
                Assert.Equal(cost, step.EstimatedMilliseconds)
                next, total + bigint cost
            | _ -> failwithf "Infeasible or ambiguous step %s from %A" id state) (initial, 0I)
    Assert.True(goal state, "Replay must complete the goal.")
    Assert.Equal(total, bigint plan.TotalEstimatedMilliseconds)

let private assertOptimum world knowledge initial goal successors =
    let expected = enumerateMinimum initial goal successors
    match expected, Planning.plan world knowledge SearchBudget.defaultBudget with
    | Some cost, Ok(ProvenFastest(plan, _)) ->
        Assert.Equal(cost, bigint plan.TotalEstimatedMilliseconds)
        replay initial goal successors plan
        Some plan
    | None, Ok(NoCompleteModeledRouteFound report) ->
        Assert.Equal(FrontierExhausted, report.StoppingReason)
        None
    | _, actual -> failwithf "Expected enumerated optimum %A, got %A" expected actual

[<Fact>]
let ``all 4096 three-location worlds match independent path enumeration`` () =
    let location n = LocationId(string n)
    let pairs = [ for source in 0..2 do for target in 0..2 do if source <> target then source, target ]
    for encoding in 0..4095 do
        let edges =
            pairs
            |> List.mapi (fun index (source, target) ->
                let digit = (encoding >>> (2 * index)) &&& 3
                if digit = 0 then None
                else Some(sprintf "%d-%d" source target, source, target, int64 (digit - 1)))
            |> List.choose id
        let successors state =
            edges |> List.choose (fun (name, source, target, cost) -> if source = state then Some(name, cost, target) else None)
        let world =
            { Locations = Set.ofList [ location 0; location 1; location 2 ]
              Facts = Set.empty
              ResourceCapacities = Map.empty
              Goal = [ AtLocation(location 2) ]
              Activities =
                edges |> List.map (fun (name, source, target, _) ->
                    { activity name [ AtLocation(location source) ] [] [] [] with Destination = Some(location target) })
              DurationEstimatesMs = edges |> List.map (fun (name, _, _, cost) -> ActivityId name, cost) |> Map.ofList }
        let knowledge = { Location = location 0; Facts = Map.empty; Resources = Map.empty }
        assertOptimum world knowledge 0 ((=) 2) successors |> ignore

[<Theory>]
[<InlineData(3L, 3L)>]
[<InlineData(20L, 11L)>]
let ``enumeration prices the cheapest isolated choice with all its preparation`` directCost expectedTotal =
    let home, complete, token = LocationId "home", FactId "complete", ResourceId "token"
    let world =
        { Locations = Set.singleton home
          Facts = Set.singleton complete
          ResourceCapacities = Map.ofList [ token, 1L ]
          Goal = [ FactIs(complete, true) ]
          Activities =
            [ activity "direct" [] [] [] [ complete, true ]
              activity "prepare" [] [] [ token, 1L ] []
              activity "cheap-isolated" [] [ token, 1L ] [] [ complete, true ] ]
          DurationEstimatesMs = Map.ofList [ ActivityId "direct", directCost; ActivityId "prepare", 10L; ActivityId "cheap-isolated", 1L ] }
    let knowledge = { Location = home; Facts = Map.ofList [ complete, false ]; Resources = Map.ofList [ token, 0L ] }
    let successors (hasToken, doneAlready) =
        [ if not doneAlready then
              yield "direct", directCost, (hasToken, true)
              if not hasToken then yield "prepare", 10L, (true, false)
              if hasToken then yield "cheap-isolated", 1L, (false, true) ]
    let plan = assertOptimum world knowledge (false, false) snd successors |> Option.get
    Assert.Equal(expectedTotal, plan.TotalEstimatedMilliseconds)

[<Theory>]
[<InlineData(2L, 10L, "pick-a", "pick-b")>]
[<InlineData(10L, 2L, "pick-b", "pick-a")>]
let ``changing travel costs reverses the optimal collection order`` aToB bToA first second =
    let location n = LocationId(string n)
    let a, b = FactId "a", FactId "b"
    let travel =
        [ "home-a", 0, 1, 2L; "a-home", 1, 0, 2L
          "home-b", 0, 2, 1L; "b-home", 2, 0, 1L
          "a-b", 1, 2, aToB; "b-a", 2, 1, bToA ]
    let successors (position, collected) =
        [ for name, source, target, cost in travel do
              if position = source then yield name, cost, (target, collected)
          if position = 1 && collected &&& 1 = 0 then yield "pick-a", 1L, (position, collected ||| 1)
          if position = 2 && collected &&& 2 = 0 then yield "pick-b", 1L, (position, collected ||| 2) ]
    let world =
        { Locations = Set.ofList [ location 0; location 1; location 2 ]
          Facts = Set.ofList [ a; b ]
          ResourceCapacities = Map.empty
          Goal = [ AtLocation(location 0); FactIs(a, true); FactIs(b, true) ]
          Activities =
            [ for name, source, target, _ in travel do
                  { activity name [ AtLocation(location source) ] [] [] [] with Destination = Some(location target) }
              activity "pick-a" [ AtLocation(location 1); FactIs(a, false) ] [] [] [ a, true ]
              activity "pick-b" [ AtLocation(location 2); FactIs(b, false) ] [] [] [ b, true ] ]
          DurationEstimatesMs =
            [ for name, _, _, cost in travel do ActivityId name, cost
              ActivityId "pick-a", 1L; ActivityId "pick-b", 1L ] |> Map.ofList }
    let knowledge = { Location = location 0; Facts = Map.ofList [ a, false; b, false ]; Resources = Map.empty }
    let plan = assertOptimum world knowledge (0, 0) ((=) (0, 3)) successors |> Option.get
    Assert.Equal(7L, plan.TotalEstimatedMilliseconds)
    let pickups = plan.Steps |> List.map (fun step -> step.ActivityId) |> List.filter (fun id -> id = ActivityId "pick-a" || id = ActivityId "pick-b")
    Assert.Equal<ActivityId list>([ ActivityId first; ActivityId second ], pickups)

[<Theory>]
[<InlineData(9, false)>]
[<InlineData(100000, true)>]
let ``repeated resource trips replay for both proven and best-found routes`` budget expectProven =
    let home, source = LocationId "home", LocationId "source"
    let carried, delivered = ResourceId "carried", ResourceId "delivered"
    let complete = FactId "complete"
    let activities =
        [ { activity "out" [ AtLocation home ] [] [] [] with Destination = Some source }
          { activity "back" [ AtLocation source ] [] [] [] with Destination = Some home }
          activity "take" [ AtLocation source ] [] [ carried, 1L ] []
          activity "deliver" [ AtLocation home ] [ carried, 1L ] [ delivered, 1L ] []
          activity "finish" [ AtLocation home; ResourceAtLeast(delivered, 2L) ] [] [] [ complete, true ] ]
    let world =
        { Locations = Set.ofList [ home; source ]
          Facts = Set.singleton complete
          ResourceCapacities = Map.ofList [ carried, 1L; delivered, 2L ]
          Goal = [ FactIs(complete, true) ]
          Activities = activities
          DurationEstimatesMs =
            [ "out", 1L; "back", 1L; "take", 2L; "deliver", 1L; "finish", 10L ]
            |> List.map (fun (name, cost) -> ActivityId name, cost) |> Map.ofList }
    let knowledge = { Location = home; Facts = Map.ofList [ complete, false ]; Resources = Map.ofList [ carried, 0L; delivered, 0L ] }
    let successors (atHome, hasItem, deliveries, doneAlready) =
        [ if not doneAlready then
              if atHome then
                  yield "out", 1L, (false, hasItem, deliveries, false)
                  if hasItem && deliveries < 2 then yield "deliver", 1L, (true, false, deliveries + 1, false)
                  if deliveries = 2 then yield "finish", 10L, (true, hasItem, deliveries, true)
              else
                  yield "back", 1L, (true, hasItem, deliveries, false)
                  if not hasItem then yield "take", 2L, (false, true, deliveries, false) ]
    let initial = true, false, 0, false
    let goal (_, _, _, doneAlready) = doneAlready
    Assert.Equal(Some 20I, enumerateMinimum initial goal successors)
    let plan =
        match Planning.plan world knowledge { MaxExpandedStates = budget } with
        | Ok(ProvenFastest(plan, report)) when expectProven ->
            Assert.Equal(MinimumCostGoalSelected, report.StoppingReason)
            plan
        | Ok(BestFound(plan, report)) when not expectProven ->
            Assert.Equal({ ExpandedStates = 9; StoppingReason = ExpansionBudgetExhausted }, report)
            plan
        | other -> failwithf "Unexpected classification: %A" other
    replay initial goal successors plan
    Assert.Equal(20L, plan.TotalEstimatedMilliseconds)
    Assert.Equal(0L, plan.FinalKnowledge.Resources[carried])
    Assert.Equal(2L, plan.FinalKnowledge.Resources[delivered])
    Assert.Equal(home, plan.FinalKnowledge.Location)
    Assert.True(plan.FinalKnowledge.Facts[complete])
    Assert.Equal<ActivityId list>(
        [ "out"; "take"; "back"; "deliver"; "out"; "take"; "back"; "deliver"; "finish" ] |> List.map ActivityId,
        plan.Steps |> List.map (fun step -> step.ActivityId))
