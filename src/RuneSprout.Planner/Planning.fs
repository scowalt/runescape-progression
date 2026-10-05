namespace RuneSprout.Planner

open System.Collections.Generic

module Planning =
    let private satisfies (state: AccountKnowledge) = function
        | AtLocation location -> state.Location = location
        | FactIs(fact, value) -> state.Facts[fact] = value
        | ResourceAtLeast(resource, count) -> state.Resources[resource] >= count

    let private isGoal catalog state = List.forall (satisfies state) catalog.Goal

    let private transition catalog state activity =
        if not (List.forall (satisfies state) activity.Requirements) then
            None
        else
            let quantities =
                state.Resources
                |> Map.toList
                |> List.map (fun (resource, count) ->
                    let consumed = Map.tryFind resource activity.Consumes |> Option.defaultValue 0L
                    let produced = Map.tryFind resource activity.Produces |> Option.defaultValue 0L
                    if consumed > count then
                        None
                    else
                        let remaining = Checked.(-) count consumed
                        if produced > Checked.(-) catalog.ResourceCapacities[resource] remaining then
                            None
                        else
                            Some(resource, Checked.(+) remaining produced))

            if quantities |> List.exists Option.isNone then
                None
            else
                Some
                    { Location = activity.Destination |> Option.defaultValue state.Location
                      Facts = Map.fold (fun facts key value -> Map.add key value facts) state.Facts activity.FactChanges
                      Resources = quantities |> List.choose id |> Map.ofList }

    type private Route =
        { State: AccountKnowledge
          Cost: int64
          ReversedSteps: ActivityId list }

    let private addCost catalog cost id =
        try
            Ok(Checked.(+) cost catalog.DurationEstimatesMs[id])
        with :? System.OverflowException ->
            Error(CostOverflow id)

    let private personalPlan catalog route =
        { Steps =
            route.ReversedSteps
            |> List.rev
            |> List.map (fun id -> { ActivityId = id; EstimatedMilliseconds = catalog.DurationEstimatesMs[id] })
          TotalEstimatedMilliseconds = route.Cost
          FinalKnowledge = route.State }

    let private search (catalog: PlanningCatalog) (knowledge: AccountKnowledge) (budget: SearchBudget) : Result<PlanningOutcome, PlanningError list> =
        let activities = catalog.Activities |> List.sortBy (fun activity -> activity.Id)
        let frontier = PriorityQueue<Route, struct (int64 * int64)>()
        let costs = Dictionary<AccountKnowledge, int64>()
        let mutable sequence = 0L
        let enqueue route =
            frontier.Enqueue(route, struct (route.Cost, sequence))
            sequence <- Checked.(+) sequence 1L

        let initial = { State = knowledge; Cost = 0L; ReversedSteps = [] }
        costs.Add(knowledge, 0L)
        enqueue initial
        let mutable expanded = 0
        let mutable outcome = None
        let mutable bestComplete: Route option = None
        let mutable failure = None

        while outcome.IsNone && failure.IsNone && frontier.Count > 0 do
            let route = frontier.Dequeue()
            if costs[route.State] = route.Cost then
                if isGoal catalog route.State then
                    outcome <- Some(ProvenFastest(personalPlan catalog route, { ExpandedStates = expanded; StoppingReason = MinimumCostGoalSelected }))
                elif expanded >= budget.MaxExpandedStates then
                    let report = { ExpandedStates = expanded; StoppingReason = ExpansionBudgetExhausted }
                    outcome <-
                        Some(
                            match bestComplete with
                            | Some best -> BestFound(personalPlan catalog best, report)
                            | None -> NoCompleteModeledRouteFound report)
                else
                    expanded <- Checked.(+) expanded 1
                    for activity in activities do
                        if failure.IsNone then
                            match transition catalog route.State activity with
                            | None -> ()
                            | Some state ->
                                match addCost catalog route.Cost activity.Id with
                                | Error error -> failure <- Some error
                                | Ok cost ->
                                    match costs.TryGetValue state with
                                    | true, previous when previous <= cost -> ()
                                    | _ ->
                                        costs[state] <- cost
                                        let next = { State = state; Cost = cost; ReversedSteps = activity.Id :: route.ReversedSteps }
                                        enqueue next
                                        if isGoal catalog state then
                                            match bestComplete with
                                            | Some best when best.Cost <= cost -> ()
                                            | _ -> bestComplete <- Some next

        match failure with
        | Some error -> Error [ error ]
        | None ->
            outcome
            |> Option.defaultValue (NoCompleteModeledRouteFound { ExpandedStates = expanded; StoppingReason = FrontierExhausted })
            |> Ok

    let private verifyPlan catalog knowledge (plan: PersonalPlan) =
        let activities = catalog.Activities |> List.map (fun activity -> activity.Id, activity) |> Map.ofList
        let invalid message = Error(ReplayVerificationFailed message)
        let replayStep result step =
            result
            |> Result.bind (fun (state, cost) ->
                if isGoal catalog state then
                    invalid "Route continues after goal completion."
                else
                    match Map.tryFind step.ActivityId activities with
                    | None -> invalid "Route names an activity outside the catalog."
                    | Some activity ->
                        if step.EstimatedMilliseconds <> catalog.DurationEstimatesMs[activity.Id] then
                            invalid "Step duration differs from the supplied estimate."
                        else
                            match transition catalog state activity with
                            | None -> invalid "Route has an infeasible requirement or resource effect."
                            | Some next -> addCost catalog cost activity.Id |> Result.map (fun total -> next, total))

        plan.Steps
        |> List.fold replayStep (Ok(knowledge, 0L))
        |> Result.bind (fun (state, cost) ->
            if not (isGoal catalog state) then invalid "Route does not complete the goal."
            elif state <> plan.FinalKnowledge then invalid "Replayed knowledge differs from the route result."
            elif cost <> plan.TotalEstimatedMilliseconds then invalid "Replayed total differs from the route cost."
            else Ok())

    let plan catalog knowledge budget =
        match Validation.inputs catalog knowledge budget with
        | [] ->
            search catalog knowledge budget
            |> Result.bind (fun outcome ->
                match outcome with
                | ProvenFastest(plan, _)
                | BestFound(plan, _) ->
                    verifyPlan catalog knowledge plan
                    |> Result.mapError List.singleton
                    |> Result.map (fun () -> outcome)
                | NoCompleteModeledRouteFound _ -> Ok outcome)
        | errors -> Error(List.map InvalidInput errors)
