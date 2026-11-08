namespace RuneSprout.Planner

open System.Collections.Generic

module Planning =
    type private Belief =
        { Current: AccountKnowledge
          Start: AccountKnowledge
          Relied: Set<AccountFact> }

    let private believe knowledge = { Current = knowledge; Start = knowledge; Relied = Set.empty }

    let private atLeast catalog optimistic belief resource count =
        let raise knowledge bound = { knowledge with Resources = Map.add resource (AtLeast(int64 bound)) knowledge.Resources }
        let limit = Bounds.limit catalog resource
        match belief.Current.Resources[resource], belief.Start.Resources[resource] with
        | quantity, _ when Bounds.lower quantity >= count -> Some belief
        | AtLeast current, AtLeast start when Set.contains (ResourceQuantity resource) optimistic ->
            let start = bigint start + count - bigint current
            if count > limit || start > limit then
                None
            else
                Some
                    { Current = raise belief.Current count
                      Start = raise belief.Start start
                      Relied = Set.add (ResourceQuantity resource) belief.Relied }
        | _ -> None

    let private settle optimistic belief fact known value refine =
        match known with
        | Known current when current = value -> Some belief
        | Unknown when Set.contains fact optimistic ->
            Some
                { Current = refine belief.Current
                  Start = refine belief.Start
                  Relied = Set.add fact belief.Relied }
        | _ -> None

    let private assume catalog optimistic belief = function
        | AtLocation location ->
            settle optimistic belief CurrentLocation belief.Current.Location location (fun knowledge ->
                { knowledge with Location = Known location })
        | FactIs(fact, value) ->
            settle optimistic belief (FactValue fact) belief.Current.Facts[fact] value (fun knowledge ->
                { knowledge with Facts = Map.add fact (Known value) knowledge.Facts })
        | ResourceAtLeast(resource, count) -> atLeast catalog optimistic belief resource (bigint count)

    let private satisfy catalog optimistic belief conditions =
        conditions
        |> List.fold (fun result condition -> Option.bind (fun current -> assume catalog optimistic current condition) result) (Some belief)
        |> Option.filter (fun belief -> Bounds.consistent catalog belief.Current && Bounds.consistent catalog belief.Start)

    let private goal catalog optimistic belief = satisfy catalog optimistic belief catalog.Goal

    let private effects catalog (activity: Activity) resource =
        let amount id (source: Map<ResourceId, int64>) = Map.tryFind id source |> Option.map bigint |> Option.defaultValue 0I
        match Bounds.occupants catalog resource with
        | [] -> amount resource activity.Consumes, amount resource activity.Produces
        | occupants ->
            let net = occupants |> List.sumBy (fun id -> amount id activity.Produces - amount id activity.Consumes)
            max net 0I, max -net 0I

    let private transition catalog optimistic belief activity =
        let changes = belief.Current.Resources |> Map.map (fun resource _ -> effects catalog activity resource)
        let withInputs =
            changes
            |> Map.fold
                (fun result resource (consumed, _) ->
                    if consumed = 0I then result
                    else Option.bind (fun current -> atLeast catalog optimistic current resource consumed) result)
                (satisfy catalog optimistic belief activity.Requirements)

        withInputs
        |> Option.bind (fun belief ->
            let state = belief.Current
            let after resource =
                let consumed, produced = changes[resource]
                Bounds.lower state.Resources[resource] - consumed + produced
            let unbounded resource =
                let consumed, produced = changes[resource]
                match catalog.ResourceLimits[resource], Bounds.occupants catalog resource with
                | Capacity _, [] -> Bounds.upper catalog state resource - consumed + produced > Bounds.limit catalog resource
                | _ -> false
            let overflows = state.Resources |> Map.filter (fun resource _ -> unbounded resource) |> Map.keys |> Seq.map ResourceQuantity |> Set.ofSeq
            let fits = state.Resources |> Map.forall (fun resource _ -> after resource <= Bounds.limit catalog resource)
            if not fits || not (Set.isSubset overflows optimistic) then
                None
            else
                let next =
                    { Location = activity.Destination |> Option.map Known |> Option.defaultValue state.Location
                      Facts = Map.fold (fun facts key value -> Map.add key (Known value) facts) state.Facts activity.FactChanges
                      Resources =
                        state.Resources
                        |> Map.map (fun resource quantity ->
                            match quantity with
                            | Exactly _ -> Exactly(int64 (after resource))
                            | AtLeast _ -> AtLeast(int64 (after resource))) }
                if Bounds.consistent catalog next then
                    Some { belief with Current = next; Relied = Set.union belief.Relied overflows }
                else
                    None)

    type private Route =
        { Belief: Belief
          Cost: int64
          ReversedSteps: ActivityId list }

    type private PassEnd =
        | Reached of Route
        | OutOfBudget of Route option
        | Exhausted

    let private addCost catalog cost id =
        try
            Ok(Checked.(+) cost catalog.DurationEstimatesMs[id])
        with :? System.OverflowException ->
            Error(CostOverflow id)

    let private explore catalog knowledge optimistic budget limit =
        let activities = catalog.Activities |> List.sortBy (fun activity -> activity.Id)
        let frontier = PriorityQueue<Route, struct (int64 * int64)>()
        let costs = Dictionary<Belief, int64>()
        let mutable sequence = 0L
        let enqueue route =
            frontier.Enqueue(route, struct (route.Cost, sequence))
            sequence <- Checked.(+) sequence 1L

        let start = believe knowledge
        costs.Add(start, 0L)
        enqueue { Belief = start; Cost = 0L; ReversedSteps = [] }
        let mutable expanded = 0
        let mutable ending = None
        let mutable bestComplete: Route option = None
        let mutable failure = None

        while ending.IsNone && failure.IsNone && frontier.Count > 0 do
            let route = frontier.Dequeue()
            if costs[route.Belief] = route.Cost then
                match limit, goal catalog optimistic route.Belief with
                | Some limit, _ when route.Cost >= limit -> ending <- Some Exhausted
                | _, Some reached -> ending <- Some(Reached { route with Belief = reached })
                | _ when expanded >= budget -> ending <- Some(OutOfBudget bestComplete)
                | _ ->
                    expanded <- Checked.(+) expanded 1
                    for activity in activities do
                        if failure.IsNone then
                            match transition catalog optimistic route.Belief activity with
                            | None -> ()
                            | Some belief ->
                                match addCost catalog route.Cost activity.Id with
                                | Error error -> failure <- Some error
                                | Ok cost ->
                                    match costs.TryGetValue belief with
                                    | true, previous when previous <= cost -> ()
                                    | _ ->
                                        costs[belief] <- cost
                                        let next =
                                            { Belief = belief
                                              Cost = cost
                                              ReversedSteps = activity.Id :: route.ReversedSteps }
                                        enqueue next
                                        if (goal catalog optimistic belief).IsSome then
                                            match bestComplete with
                                            | Some best when best.Cost <= cost -> ()
                                            | _ -> bestComplete <- Some next

        match failure with
        | Some error -> Error [ error ]
        | None -> Ok(ending |> Option.defaultValue Exhausted, expanded)

    let private personalPlan catalog assumptions route =
        { Assumptions = assumptions
          Steps =
            route.ReversedSteps
            |> List.rev
            |> List.map (fun id -> { ActivityId = id; EstimatedMilliseconds = catalog.DurationEstimatesMs[id] })
          TotalEstimatedMilliseconds = route.Cost
          FinalKnowledge = route.Belief.Current }

    let private classify catalog assumptions knowledge budget =
        let unknowns = Bounds.unknowns knowledge |> Set.ofList
        let pass optimistic spent limit =
            explore catalog knowledge optimistic (budget.MaxExpandedStates - spent) limit
            |> Result.map (fun (ending, expanded) -> ending, spent + expanded)
        let report spent reason = { ExpandedStates = spent; StoppingReason = reason }
        let plan = personalPlan catalog assumptions

        let necessity (route: Route) spent =
            let folder state fact =
                state
                |> Result.bind (fun (spent, necessary, mayRelyOn, limited) ->
                    pass (Set.remove fact unknowns) spent None
                    |> Result.map (fun (ending, spent) ->
                        match ending with
                        | Exhausted -> spent, Set.add fact necessary, mayRelyOn, limited
                        | Reached alternative -> spent, necessary, Set.union mayRelyOn alternative.Belief.Relied, limited
                        | OutOfBudget _ -> spent, necessary, Set.add fact mayRelyOn, true))
            route.Belief.Relied
            |> Set.fold folder (Ok(spent, Set.empty, route.Belief.Relied, false))
            |> Result.map (fun (spent, necessary, mayRelyOn, limited) ->
                let report = report spent (if limited then ExpansionBudgetExhausted else FrontierExhausted)
                if Set.isEmpty necessary then
                    NoCompleteModeledRouteFound(report, Set.toList mayRelyOn)
                else
                    NeedsAccountInformation(Set.toList necessary, report))

        pass Set.empty 0 None
        |> Result.bind (fun (ending, spent) ->
            match ending with
            | Reached route when Set.isEmpty unknowns -> Ok(ProvenFastest(plan route, report spent MinimumCostGoalSelected))
            | Reached route ->
                pass unknowns spent (Some route.Cost)
                |> Result.map (fun (check, spent) ->
                    match check with
                    | Reached cheaper -> BestFound(plan route, report spent MinimumCostGoalSelected, Set.toList cheaper.Belief.Relied)
                    | OutOfBudget _ -> BestFound(plan route, report spent ExpansionBudgetExhausted, [])
                    | Exhausted -> ProvenFastest(plan route, report spent MinimumCostGoalSelected))
            | OutOfBudget(Some best) -> Ok(BestFound(plan best, report spent ExpansionBudgetExhausted, []))
            | OutOfBudget None -> Ok(NoCompleteModeledRouteFound(report spent ExpansionBudgetExhausted, []))
            | Exhausted when Set.isEmpty unknowns -> Ok(NoCompleteModeledRouteFound(report spent FrontierExhausted, []))
            | Exhausted ->
                pass unknowns spent None
                |> Result.bind (fun (diagnosis, spent) ->
                    match diagnosis with
                    | Reached route -> necessity route spent
                    | OutOfBudget _ -> Ok(NoCompleteModeledRouteFound(report spent ExpansionBudgetExhausted, []))
                    | Exhausted -> Ok(NoCompleteModeledRouteFound(report spent FrontierExhausted, []))))

    let private verifyPlan catalog knowledge (plan: PersonalPlan) =
        let activities = catalog.Activities |> List.map (fun activity -> activity.Id, activity) |> Map.ofList
        let complete state = (goal catalog Set.empty (believe state)).IsSome
        let invalid message = Error(ReplayVerificationFailed message)
        let replayStep result step =
            result
            |> Result.bind (fun (state, cost) ->
                if complete state then
                    invalid "Route continues after goal completion."
                else
                    match Map.tryFind step.ActivityId activities with
                    | None -> invalid "Route names an activity outside the catalog."
                    | Some activity ->
                        if step.EstimatedMilliseconds <> catalog.DurationEstimatesMs[activity.Id] then
                            invalid "Step duration differs from the supplied estimate."
                        else
                            match transition catalog Set.empty (believe state) activity with
                            | None -> invalid "Route has a requirement, resource input, or space that is not guaranteed."
                            | Some next -> addCost catalog cost activity.Id |> Result.map (fun total -> next.Current, total))

        plan.Steps
        |> List.fold replayStep (Ok(knowledge, 0L))
        |> Result.bind (fun (state, cost) ->
            if not (complete state) then invalid "Route does not complete the goal."
            elif state <> plan.FinalKnowledge then invalid "Replayed knowledge differs from the route result."
            elif cost <> plan.TotalEstimatedMilliseconds then invalid "Replayed total differs from the route cost."
            else Ok())

    let plan catalog (snapshot: AccountSnapshot) budget =
        match Validation.inputs catalog snapshot budget with
        | Error errors -> Error(List.map InvalidInput errors)
        | Ok knowledge ->
            classify catalog snapshot.Assumptions knowledge budget
            |> Result.bind (fun outcome ->
                match outcome with
                | ProvenFastest(plan, _)
                | BestFound(plan, _, _) ->
                    verifyPlan catalog knowledge plan
                    |> Result.mapError List.singleton
                    |> Result.map (fun () -> outcome)
                | NeedsAccountInformation _
                | NoCompleteModeledRouteFound _ -> Ok outcome)
