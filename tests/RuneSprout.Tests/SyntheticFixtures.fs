module RuneSprout.Tests.SyntheticFixtures

open RuneSprout.Planner

let activity id requirements consumes produces changes =
    { Id = ActivityId id
      Requirements = requirements
      Consumes = Map.ofList consumes
      Produces = Map.ofList produces
      Destination = None
      FactChanges = Map.ofList changes }
