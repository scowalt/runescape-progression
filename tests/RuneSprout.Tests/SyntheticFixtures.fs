module RuneSprout.Tests.SyntheticFixtures

open RuneSprout.Planner

// Input construction only. Independent oracle rules must not use planner transitions.
// These fixtures describe test worlds, never accepted OSRS knowledge.
let activity id requirements consumes produces changes =
    { Id = ActivityId id
      Requirements = requirements
      Consumes = Map.ofList consumes
      Produces = Map.ofList produces
      Destination = None
      FactChanges = Map.ofList changes }
