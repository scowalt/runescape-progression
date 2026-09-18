namespace RuneSprout.Planner

// These types describe a finite deterministic model, not approval of game knowledge.
type LocationId = LocationId of string
type FactId = FactId of string
type ResourceId = ResourceId of string
type ActivityId = ActivityId of string

type Requirement =
    | AtLocation of LocationId
    | FactIs of FactId * bool
    | ResourceAtLeast of ResourceId * int64

type AccountKnowledge =
    { Location: LocationId
      Facts: Map<FactId, bool>
      Resources: Map<ResourceId, int64> }

type Activity =
    { Id: ActivityId
      Requirements: Requirement list
      Consumes: Map<ResourceId, int64>
      Produces: Map<ResourceId, int64>
      Destination: LocationId option
      FactChanges: Map<FactId, bool> }

type PlanningCatalog =
    { Locations: Set<LocationId>
      Facts: Set<FactId>
      // Explicit world/storage limits. These are feasibility rules, not search caps.
      ResourceCapacities: Map<ResourceId, int64>
      Goal: Requirement list
      Activities: Activity list
      // Supplied estimates are separate from activity mechanics; not measured timings.
      DurationEstimatesMs: Map<ActivityId, int64> }

type SearchBudget = { MaxExpandedStates: int }

module SearchBudget =
    let defaultBudget = { MaxExpandedStates = 100_000 }

type InputError =
    | InvalidCatalog of string
    | InvalidAccountKnowledge of string
    | InvalidSearchBudget of int

type PlanningError =
    | InvalidInput of InputError
    | CostOverflow of ActivityId
    | ReplayVerificationFailed of string

type PlannedActivity =
    { ActivityId: ActivityId
      EstimatedMilliseconds: int64 }

type PersonalPlan =
    { Steps: PlannedActivity list
      TotalEstimatedMilliseconds: int64
      FinalKnowledge: AccountKnowledge }

type StoppingReason =
    | MinimumCostGoalSelected
    | ExpansionBudgetExhausted
    | FrontierExhausted

type SearchReport =
    { ExpandedStates: int
      StoppingReason: StoppingReason }

type PlanningOutcome =
    | ProvenFastest of PersonalPlan * SearchReport
    | BestFound of PersonalPlan * SearchReport
    | NoCompleteModeledRouteFound of SearchReport
