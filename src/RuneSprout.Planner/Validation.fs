namespace RuneSprout.Planner

open System

module internal Validation =
    let private keys map = map |> Map.toSeq |> Seq.map fst |> Set.ofSeq

    let inputs (catalog: PlanningCatalog) (snapshot: AccountSnapshot) budget =
        let knowledge = snapshot.Knowledge
        let resources = keys catalog.ResourceLimits
        let activityIds = catalog.Activities |> List.map (fun activity -> activity.Id)
        let activityIdSet = Set.ofList activityIds
        let invalid message = InvalidCatalog message
        let reference kind declared id =
            [ if not (Set.contains id declared) then
                  invalid (sprintf "Undeclared %s: %A." kind id) ]
        let location id = reference "location" catalog.Locations id
        let fact id = reference "fact" catalog.Facts id
        let resource id = reference "resource" resources id
        let quantity id count =
            [ yield! resource id
              if count < 0L then invalid (sprintf "Quantity for %A must be nonnegative." id) ]
        let requirement = function
            | AtLocation id -> location id
            | FactIs(id, _) -> fact id
            | ResourceAtLeast(id, count) -> quantity id count
        let identifier kind value =
            [ if String.IsNullOrWhiteSpace value then invalid (sprintf "%s identifier must not be blank." kind) ]

        [ if budget.MaxExpandedStates <= 0 then
              InvalidSearchBudget budget.MaxExpandedStates
          if Set.isEmpty catalog.Locations then invalid "At least one location is required."
          for LocationId id in catalog.Locations do yield! identifier "Location" id
          for FactId id in catalog.Facts do yield! identifier "Fact" id
          for KeyValue(ResourceId id, limit) in catalog.ResourceLimits do
              yield! identifier "Resource" id
              match limit with
              | Capacity capacity -> if capacity < 0L then invalid (sprintf "Capacity for %s must be nonnegative." id)
              | OccupiesSpace _ -> invalid "Shared space is not supported yet."
          if activityIds.Length <> activityIdSet.Count then invalid "Activity identifiers must be unique."
          if keys catalog.DurationEstimatesMs <> activityIdSet then
              invalid "Duration estimates must name exactly the catalog activities."
          for KeyValue(id, duration) in catalog.DurationEstimatesMs do
              if duration < 0L then invalid (sprintf "Duration for %A must be nonnegative." id)
          for goalRequirement in catalog.Goal do yield! requirement goalRequirement
          for activity in catalog.Activities do
              let (ActivityId id) = activity.Id
              yield! identifier "Activity" id
              for condition in activity.Requirements do yield! requirement condition
              for KeyValue(id, count) in activity.Consumes do yield! quantity id count
              for KeyValue(id, count) in activity.Produces do yield! quantity id count
              for KeyValue(id, _) in activity.FactChanges do yield! fact id
              match activity.Destination with
              | Some id -> yield! location id
              | None -> ()
          match knowledge.Location with
          | Known location when Set.contains location catalog.Locations -> ()
          | _ -> InvalidAccountKnowledge "Starting location must be a known declared location."
          if not (List.isEmpty snapshot.Assumptions) then InvalidAccountKnowledge "Assumptions are not supported yet."
          if knowledge.Facts |> Map.exists (fun _ value -> value = Unknown) then
              InvalidAccountKnowledge "Unknown facts are not supported yet."
          if keys knowledge.Facts <> catalog.Facts then
              InvalidAccountKnowledge "Explicit values are required for exactly the declared facts; partial knowledge is not supported yet."
          if keys knowledge.Resources <> resources then
              InvalidAccountKnowledge "Exact counts are required for exactly the declared resources; partial knowledge is not supported yet."
          for KeyValue(id, quantity) in knowledge.Resources do
              let count = match quantity with Exactly count -> count | AtLeast _ -> -1L
              if count < 0L then InvalidAccountKnowledge(sprintf "Count for %A must be a nonnegative exact count." id)
              match Map.tryFind id catalog.ResourceLimits with
              | Some(Capacity capacity) when count > capacity ->
                  InvalidAccountKnowledge(sprintf "Count for %A exceeds its modeled capacity." id)
              | _ -> () ]
