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
    match expected, Planning.plan world (snapshot knowledge) SearchBudget.defaultBudget with
    | Some cost, Ok(ProvenFastest(plan, _)) ->
        Assert.Equal(cost, bigint plan.TotalEstimatedMilliseconds)
        replay initial goal successors plan
        Some plan
    | None, Ok(NoCompleteModeledRouteFound(report, [])) ->
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
              ResourceLimits = Map.empty
              Goal = [ AtLocation(location 2) ]
              Activities =
                edges |> List.map (fun (name, source, target, _) ->
                    { activity name [ AtLocation(location source) ] [] [] [] with Destination = Some(location target) })
              DurationEstimatesMs = edges |> List.map (fun (name, _, _, cost) -> ActivityId name, cost) |> Map.ofList }
        let knowledge = { Location = Known(location 0); Facts = Map.empty; Resources = Map.empty }
        assertOptimum world knowledge 0 ((=) 2) successors |> ignore

[<Theory>]
[<InlineData(3L, 3L)>]
[<InlineData(20L, 11L)>]
let ``enumeration prices the cheapest isolated choice with all its preparation`` directCost expectedTotal =
    let home, complete, token = LocationId "home", FactId "complete", ResourceId "token"
    let world =
        { Locations = Set.singleton home
          Facts = Set.singleton complete
          ResourceLimits = Map.ofList [ token, Capacity 1L ]
          Goal = [ FactIs(complete, true) ]
          Activities =
            [ activity "direct" [] [] [] [ complete, true ]
              activity "prepare" [] [] [ token, 1L ] []
              activity "cheap-isolated" [] [ token, 1L ] [] [ complete, true ] ]
          DurationEstimatesMs = Map.ofList [ ActivityId "direct", directCost; ActivityId "prepare", 10L; ActivityId "cheap-isolated", 1L ] }
    let knowledge = { Location = Known home; Facts = Map.ofList [ complete, Known false ]; Resources = Map.ofList [ token, Exactly 0L ] }
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
          ResourceLimits = Map.empty
          Goal = [ AtLocation(location 0); FactIs(a, true); FactIs(b, true) ]
          Activities =
            [ for name, source, target, _ in travel do
                  { activity name [ AtLocation(location source) ] [] [] [] with Destination = Some(location target) }
              activity "pick-a" [ AtLocation(location 1); FactIs(a, false) ] [] [] [ a, true ]
              activity "pick-b" [ AtLocation(location 2); FactIs(b, false) ] [] [] [ b, true ] ]
          DurationEstimatesMs =
            [ for name, _, _, cost in travel do ActivityId name, cost
              ActivityId "pick-a", 1L; ActivityId "pick-b", 1L ] |> Map.ofList }
    let knowledge = { Location = Known(location 0); Facts = Map.ofList [ a, Known false; b, Known false ]; Resources = Map.empty }
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
          ResourceLimits = Map.ofList [ carried, Capacity 1L; delivered, Capacity 2L ]
          Goal = [ FactIs(complete, true) ]
          Activities = activities
          DurationEstimatesMs =
            [ "out", 1L; "back", 1L; "take", 2L; "deliver", 1L; "finish", 10L ]
            |> List.map (fun (name, cost) -> ActivityId name, cost) |> Map.ofList }
    let knowledge = { Location = Known home; Facts = Map.ofList [ complete, Known false ]; Resources = Map.ofList [ carried, Exactly 0L; delivered, Exactly 0L ] }
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
        match Planning.plan world (snapshot knowledge) { MaxExpandedStates = budget } with
        | Ok(ProvenFastest(plan, report)) when expectProven ->
            Assert.Equal(MinimumCostGoalSelected, report.StoppingReason)
            plan
        | Ok(BestFound(plan, report, [])) when not expectProven ->
            Assert.Equal({ ExpandedStates = 9; StoppingReason = ExpansionBudgetExhausted }, report)
            plan
        | other -> failwithf "Unexpected classification: %A" other
    replay initial goal successors plan
    Assert.Equal(20L, plan.TotalEstimatedMilliseconds)
    Assert.Equal(Exactly 0L, plan.FinalKnowledge.Resources[carried])
    Assert.Equal(Exactly 2L, plan.FinalKnowledge.Resources[delivered])
    Assert.Equal(Known home, plan.FinalKnowledge.Location)
    Assert.Equal(Known true, plan.FinalKnowledge.Facts[complete])
    Assert.Equal<ActivityId list>(
        [ "out"; "take"; "back"; "deliver"; "out"; "take"; "back"; "deliver"; "finish" ] |> List.map ActivityId,
        plan.Steps |> List.map (fun step -> step.ActivityId))

type private ConcreteAccount =
    { At: LocationId
      Flags: Map<FactId, bool>
      Counts: Map<ResourceId, int64> }

let private occupants (world: PlanningCatalog) space =
    world.ResourceLimits |> Map.filter (fun _ limit -> limit = OccupiesSpace space) |> Map.keys |> List.ofSeq

let private holds account = function
    | AtLocation location -> account.At = location
    | FactIs(fact, value) -> account.Flags[fact] = value
    | ResourceAtLeast(resource, count) -> account.Counts[resource] >= count

let private perform (world: PlanningCatalog) account (step: Activity) =
    let amount resource effects = Map.tryFind resource effects |> Option.defaultValue 0L
    let change resource = amount resource step.Produces - amount resource step.Consumes
    let counts =
        account.Counts
        |> Map.map (fun resource count ->
            match world.ResourceLimits[resource] with
            | OccupiesSpace _ -> count + change resource
            | Capacity _ -> count + change resource - List.sumBy change (occupants world resource))
    let fits resource count =
        account.Counts[resource] >= amount resource step.Consumes
        && count >= 0L
        && match world.ResourceLimits[resource] with
           | Capacity capacity -> count <= capacity
           | OccupiesSpace _ -> true
    if List.forall (holds account) step.Requirements && Map.forall fits counts then
        Some
            { At = step.Destination |> Option.defaultValue account.At
              Flags = Map.fold (fun flags fact value -> Map.add fact value flags) account.Flags step.FactChanges
              Counts = counts }
    else
        None

let private concreteMinimum (world: PlanningCatalog) start =
    let rec search frontier settled =
        if Set.isEmpty frontier then
            None
        else
            let (cost, account) as next = Set.minElement frontier
            let frontier = Set.remove next frontier
            if Set.contains account settled then search frontier settled
            elif List.forall (holds account) world.Goal then Some cost
            else
                let successors =
                    world.Activities
                    |> List.choose (fun step ->
                        perform world account step |> Option.map (fun result -> cost + world.DurationEstimatesMs[step.Id], result))
                search (Set.union frontier (Set.ofList successors)) (Set.add account settled)
    search (Set.singleton (0L, start)) Set.empty

let private completions (world: PlanningCatalog) (knowledge: AccountKnowledge) =
    let rec product = function
        | [] -> [ [] ]
        | (id, values) :: rest -> [ for value in values do for tail in product rest -> (id, value) :: tail ]
    let capacity resource =
        match world.ResourceLimits[resource] with
        | Capacity capacity -> capacity
        | OccupiesSpace space -> match world.ResourceLimits[space] with Capacity capacity -> capacity | _ -> 0L
    let locations = match knowledge.Location with Known location -> [ location ] | Unknown -> Set.toList world.Locations
    let flags = knowledge.Facts |> Map.toList |> List.map (fun (id, value) -> id, match value with Known known -> [ known ] | Unknown -> [ true; false ])
    let counts = knowledge.Resources |> Map.toList |> List.map (fun (id, quantity) -> id, match quantity with Exactly count -> [ count ] | AtLeast count -> [ count .. capacity id ])
    [ for location in locations do
          for flagValues in product flags do
              for countValues in product counts do
                  let counts = Map.ofList countValues
                  let within resource limit =
                      match limit with
                      | OccupiesSpace _ -> true
                      | Capacity capacity -> counts[resource] + List.sumBy (fun id -> counts[id]) (occupants world resource) <= capacity
                  if Map.forall within world.ResourceLimits then
                      { At = location; Flags = Map.ofList flagValues; Counts = counts } ]

let private describes (knowledge: AccountKnowledge) account =
    (match knowledge.Location with Known location -> location = account.At | Unknown -> true)
    && knowledge.Facts |> Map.forall (fun id value -> match value with Known known -> account.Flags[id] = known | Unknown -> true)
    && knowledge.Resources |> Map.forall (fun id quantity ->
        match quantity with
        | Exactly count -> account.Counts[id] = count
        | AtLeast count -> account.Counts[id] >= count)

let private replayConcretely (world: PlanningCatalog) (plan: PersonalPlan) start =
    let activities = world.Activities |> List.map (fun step -> step.Id, step) |> Map.ofList
    let finish, total =
        plan.Steps
        |> List.fold (fun (account, total) step ->
            match perform world account activities[step.ActivityId] with
            | Some next -> next, total + step.EstimatedMilliseconds
            | None -> failwithf "Step %A is infeasible in consistent account %A" step.ActivityId start) (start, 0L)
    Assert.True(List.forall (holds finish) world.Goal, "Replay must complete the goal.")
    Assert.Equal(plan.TotalEstimatedMilliseconds, total)
    Assert.True(describes plan.FinalKnowledge finish, sprintf "Final knowledge %A invents a fact about %A" plan.FinalKnowledge finish)

let private unknowns (knowledge: AccountKnowledge) =
    [ if knowledge.Location = Unknown then CurrentLocation
      for KeyValue(id, value) in knowledge.Facts do if value = Unknown then FactValue id
      for KeyValue(id, quantity) in knowledge.Resources do
          match quantity with
          | AtLeast _ -> ResourceQuantity id
          | Exactly _ -> () ]

[<Fact>]
let ``partial-knowledge outcomes hold in every consistent concrete account`` () =
    let random = System.Random 4
    let pick (values: 'a list) = values[random.Next values.Length]
    let locations = [ LocationId "a"; LocationId "b" ]
    let facts = [ FactId "f"; FactId "g" ]
    let space, item, stock = ResourceId "space", ResourceId "item", ResourceId "stock"
    let requirement () =
        match random.Next 3 with
        | 0 -> AtLocation(pick locations)
        | 1 -> FactIs(pick facts, random.Next 2 = 0)
        | _ -> ResourceAtLeast(pick [ item; stock; space ], int64 (1 + random.Next 2))
    let maybe chance value = if random.Next chance = 0 then [ value () ] else []
    let quantity () = if random.Next 2 = 0 then Exactly(int64 (random.Next 3)) else AtLeast(int64 (random.Next 2))
    let mutable outcomes = Set.empty
    for index in 0..299 do
        let activities =
            [ for number in 0..3 do
                  { Id = ActivityId(sprintf "act-%d" number)
                    Requirements = maybe 2 requirement @ maybe 3 requirement
                    Consumes = Map.ofList (maybe 3 (fun () -> pick [ item; stock ], 1L))
                    Produces = Map.ofList (maybe 2 (fun () -> pick [ item; stock ], 1L))
                    Destination = if random.Next 3 = 0 then Some(pick locations) else None
                    FactChanges = Map.ofList (maybe 3 (fun () -> pick facts, random.Next 2 = 0)) } ]
        let world =
            { Locations = Set.ofList locations
              Facts = Set.ofList facts
              ResourceLimits = Map.ofList [ space, Capacity 2L; item, OccupiesSpace space; stock, Capacity 2L ]
              Goal = requirement () :: maybe 2 requirement
              Activities = activities
              DurationEstimatesMs = activities |> List.map (fun step -> step.Id, int64 (random.Next 4)) |> Map.ofList }
        let knowledge =
            { Location = if random.Next 2 = 0 then Known(pick locations) else Unknown
              Facts = facts |> List.map (fun id -> id, if random.Next 2 = 0 then Known(random.Next 2 = 0) else Unknown) |> Map.ofList
              Resources = Map.ofList [ space, quantity (); item, quantity (); stock, quantity () ] }
        let accounts = completions world knowledge
        let unresolved = unknowns knowledge
        let named (facts: AccountFact list) = Assert.True(Set.isSubset (set facts) (set unresolved), sprintf "%A names facts outside %A" facts unresolved)
        let result = Planning.plan world (snapshot knowledge) SearchBudget.defaultBudget
        if Result.isOk result then Assert.NotEmpty accounts
        match result with
        | Error [ InvalidInput(InvalidAccountKnowledge _) ] -> Assert.Empty accounts
        | Ok(ProvenFastest(plan, _)) ->
            outcomes <- Set.add "proven" outcomes
            for account in accounts do
                replayConcretely world plan account
                Assert.Equal(Some plan.TotalEstimatedMilliseconds, concreteMinimum world account)
        | Ok(BestFound(plan, report, mayRelyOn)) ->
            outcomes <- Set.add "best-found" outcomes
            Assert.Equal(MinimumCostGoalSelected, report.StoppingReason)
            Assert.NotEmpty mayRelyOn
            named mayRelyOn
            for account in accounts do replayConcretely world plan account
            Assert.Contains(accounts, fun account ->
                concreteMinimum world account |> Option.exists (fun cost -> cost < plan.TotalEstimatedMilliseconds))
        | Ok(NeedsAccountInformation(necessary, _)) ->
            outcomes <- Set.add "needs-information" outcomes
            Assert.NotEmpty necessary
            Assert.Contains(accounts, fun account -> (concreteMinimum world account).IsSome)
            named necessary
        | Ok(NoCompleteModeledRouteFound(report, [])) ->
            outcomes <- Set.add "no-route" outcomes
            Assert.Equal(FrontierExhausted, report.StoppingReason)
            for account in accounts do Assert.Equal(None, concreteMinimum world account)
        | Ok(NoCompleteModeledRouteFound(report, mayRelyOn)) ->
            outcomes <- Set.add "uncertain" outcomes
            Assert.Equal(FrontierExhausted, report.StoppingReason)
            named mayRelyOn
            Assert.Contains(accounts, fun account -> (concreteMinimum world account).IsSome)
        | other -> failwithf "World %d: unexpected result %A" index other
    Assert.Equal<Set<string>>(set [ "proven"; "best-found"; "needs-information"; "no-route"; "uncertain" ], outcomes)
