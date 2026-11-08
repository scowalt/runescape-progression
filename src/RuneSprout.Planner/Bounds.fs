namespace RuneSprout.Planner

module internal Bounds =
    let lower = function
        | Exactly count
        | AtLeast count -> bigint count

    let occupants catalog space =
        catalog.ResourceLimits
        |> Map.toList
        |> List.choose (fun (resource, limit) -> if limit = OccupiesSpace space then Some resource else None)

    let limit catalog resource =
        let capacity id =
            match catalog.ResourceLimits[id] with
            | Capacity capacity -> bigint capacity
            | OccupiesSpace _ -> 0I
        match catalog.ResourceLimits[resource] with
        | Capacity capacity -> bigint capacity
        | OccupiesSpace space -> capacity space

    let private occupied catalog knowledge space =
        occupants catalog space |> List.sumBy (fun resource -> lower knowledge.Resources[resource])

    let upper catalog knowledge resource =
        match knowledge.Resources[resource] with
        | Exactly count -> bigint count
        | AtLeast count ->
            match catalog.ResourceLimits[resource] with
            | Capacity capacity -> bigint capacity - occupied catalog knowledge resource
            | OccupiesSpace space ->
                limit catalog resource - lower knowledge.Resources[space] - occupied catalog knowledge space + bigint count

    let lowers knowledge = knowledge.Resources |> Map.map (fun _ quantity -> lower quantity)

    let overfull catalog (counts: Map<ResourceId, bigint>) =
        [ for KeyValue(resource, limit) in catalog.ResourceLimits do
              match limit with
              | Capacity capacity when counts[resource] + List.sumBy (fun id -> counts[id]) (occupants catalog resource) > bigint capacity -> resource
              | _ -> () ]

    let consistent catalog knowledge = List.isEmpty (overfull catalog (lowers knowledge))

    let unknowns knowledge =
        [ if knowledge.Location = Unknown then CurrentLocation
          for KeyValue(fact, value) in knowledge.Facts do
              if value = Unknown then FactValue fact
          for KeyValue(resource, quantity) in knowledge.Resources do
              match quantity with
              | AtLeast _ -> ResourceQuantity resource
              | Exactly _ -> () ]
