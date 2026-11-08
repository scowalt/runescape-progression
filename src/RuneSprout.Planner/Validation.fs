namespace RuneSprout.Planner

open System

module internal Validation =
    let private keys map = map |> Map.toSeq |> Seq.map fst |> Set.ofSeq

    let private structure (catalog: PlanningCatalog) (snapshot: AccountSnapshot) budget =
        let knowledge = snapshot.Knowledge
        let resources = keys catalog.ResourceLimits
        let spaces = catalog.ResourceLimits |> Map.toList |> List.choose (function _, OccupiesSpace space -> Some space | _ -> None) |> Set.ofList
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
        let effect id count =
            [ yield! quantity id count
              if Set.contains id spaces then invalid (sprintf "Activities change %A only through the resources that occupy it." id) ]
        let requirement = function
            | AtLocation id -> location id
            | FactIs(id, _) -> fact id
            | ResourceAtLeast(id, count) -> quantity id count
        let identifier kind value =
            [ if String.IsNullOrWhiteSpace value then invalid (sprintf "%s identifier must not be blank." kind) ]
        let account message = InvalidAccountKnowledge message
        let accountReference kind declared id =
            [ if not (Set.contains id declared) then account (sprintf "Undeclared %s: %A." kind id) ]
        let count id = function
            | Exactly value
            | AtLeast value -> [ if value < 0L then account (sprintf "Count for %A must be nonnegative." id) ]

        [ if budget.MaxExpandedStates <= 0 then
              InvalidSearchBudget budget.MaxExpandedStates
          if Set.isEmpty catalog.Locations then invalid "At least one location is required."
          for LocationId id in catalog.Locations do yield! identifier "Location" id
          for FactId id in catalog.Facts do yield! identifier "Fact" id
          for KeyValue(ResourceId id, limit) in catalog.ResourceLimits do
              yield! identifier "Resource" id
              match limit with
              | Capacity capacity -> if capacity < 0L then invalid (sprintf "Capacity for %s must be nonnegative." id)
              | OccupiesSpace space ->
                  match Map.tryFind space catalog.ResourceLimits with
                  | Some(Capacity _) -> ()
                  | _ -> invalid (sprintf "Space occupied by %s must be a declared capacity resource." id)
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
              for KeyValue(id, count) in activity.Consumes do yield! effect id count
              for KeyValue(id, count) in activity.Produces do yield! effect id count
              for KeyValue(id, _) in activity.FactChanges do yield! fact id
              match activity.Destination with
              | Some id -> yield! location id
              | None -> ()
          match knowledge.Location with
          | Known id -> yield! accountReference "location" catalog.Locations id
          | Unknown -> ()
          if keys knowledge.Facts <> catalog.Facts then
              account "Knowledge must state a known or unknown value for exactly the declared facts."
          if keys knowledge.Resources <> resources then
              account "Knowledge must state an exact count or lower bound for exactly the declared resources."
          for KeyValue(id, quantity) in knowledge.Resources do yield! count id quantity
          for assumption in snapshot.Assumptions do
              match assumption with
              | AssumeLocation id -> yield! accountReference "location" catalog.Locations id
              | AssumeFact(id, _) -> yield! accountReference "fact" catalog.Facts id
              | AssumeResource(id, quantity) ->
                  yield! accountReference "resource" resources id
                  yield! count id quantity ]

    let private settle current value =
        match current with
        | Unknown -> Some(Known value)
        | Known known when known = value -> Some current
        | Known _ -> None

    let private refine current assumed =
        match current, assumed with
        | Exactly known, Exactly value when known = value -> Some current
        | Exactly known, AtLeast value when known >= value -> Some current
        | AtLeast known, Exactly value when value >= known -> Some assumed
        | AtLeast known, AtLeast value -> Some(AtLeast(max known value))
        | _ -> None

    let private assume catalog (knowledge: AccountKnowledge) assumption =
        let refined =
            match assumption with
            | AssumeLocation location ->
                settle knowledge.Location location |> Option.map (fun value -> { knowledge with Location = value })
            | AssumeFact(fact, value) ->
                settle knowledge.Facts[fact] value
                |> Option.map (fun value -> { knowledge with Facts = Map.add fact value knowledge.Facts })
            | AssumeResource(resource, quantity) ->
                refine knowledge.Resources[resource] quantity
                |> Option.map (fun value -> { knowledge with Resources = Map.add resource value knowledge.Resources })
        match refined with
        | Some next when Bounds.consistent catalog next -> Ok next
        | _ -> Error [ ContradictoryAssumption assumption ]

    let inputs catalog (snapshot: AccountSnapshot) budget =
        match structure catalog snapshot budget with
        | [] when not (Bounds.consistent catalog snapshot.Knowledge) ->
            Error [ InvalidAccountKnowledge "Known counts and lower bounds exceed their modeled capacity or space." ]
        | [] -> snapshot.Assumptions |> List.fold (fun knowledge assumption -> Result.bind (fun current -> assume catalog current assumption) knowledge) (Ok snapshot.Knowledge)
        | errors -> Error errors
