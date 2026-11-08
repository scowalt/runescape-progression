namespace RuneSprout.Planner

type LocationId = LocationId of string
type FactId = FactId of string
type ResourceId = ResourceId of string
type ActivityId = ActivityId of string

type Requirement =
    | AtLocation of LocationId
    | FactIs of FactId * bool
    | ResourceAtLeast of ResourceId * int64

type Knowledge<'T> =
    | Known of 'T
    | Unknown

type Quantity =
    | Exactly of int64
    | AtLeast of int64

type ResourceLimit =
    | Capacity of int64
    | OccupiesSpace of ResourceId

type AccountKnowledge =
    { Location: Knowledge<LocationId>
      Facts: Map<FactId, Knowledge<bool>>
      Resources: Map<ResourceId, Quantity> }

type Assumption =
    | AssumeLocation of LocationId
    | AssumeFact of FactId * bool
    | AssumeResource of ResourceId * Quantity

type AccountSnapshot =
    { Knowledge: AccountKnowledge
      Assumptions: Assumption list }

type AccountFact =
    | CurrentLocation
    | FactValue of FactId
    | ResourceQuantity of ResourceId

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
      ResourceLimits: Map<ResourceId, ResourceLimit>
      Goal: Requirement list
      Activities: Activity list
      DurationEstimatesMs: Map<ActivityId, int64> }

type SearchBudget = { MaxExpandedStates: int }

module SearchBudget =
    let defaultBudget = { MaxExpandedStates = 100_000 }

type InputError =
    | InvalidCatalog of string
    | InvalidAccountKnowledge of string
    | ContradictoryAssumption of Assumption
    | InvalidSearchBudget of int

type PlanningError =
    | InvalidInput of InputError
    | CostOverflow of ActivityId
    | ReplayVerificationFailed of string

type PlannedActivity =
    { ActivityId: ActivityId
      EstimatedMilliseconds: int64 }

type PersonalPlan =
    { Assumptions: Assumption list
      Steps: PlannedActivity list
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
    | BestFound of PersonalPlan * SearchReport * cheaperRouteMayRelyOn: AccountFact list
    | NeedsAccountInformation of necessary: AccountFact list * SearchReport
    | NoCompleteModeledRouteFound of SearchReport * routeMayRelyOn: AccountFact list
