# Pseudo DSL Draft

Reviewed on March 15, 2026.

## Purpose

This document turns the current design semantics into a pseudo-DSL draft.

It is not final syntax.

Its purpose is to make the intended semantics concrete enough to test for consistency across:

- regular workflow definitions
- saga workflow definitions
- fluent management APIs
- step decorators and policies

## 1. Design rules this draft follows

This pseudo-DSL follows the current baseline decisions.

- workflow and saga are different semantic definition kinds
- ephemeral and durable are different execution modes
- steps describe workflow structure or business action
- policies decorate steps, scopes, or definitions
- management commands are outside workflow definitions
- management APIs are fluent and LINQ-like
- `Where(...)` is the canonical filter mechanism
- durable-only features should be absent from ephemeral-facing APIs where practical

## 2. Regular workflow pseudo-DSL

### Example: `PriceUpdateWorkflow`

```text
workflow PriceUpdateWorkflow
  state PriceUpdateState

  init
    use request as state
    set state.status = PendingValidation

  if state.isValid == false
    end FailedValidation

  parallel
    branch Catalog
      step SendCatalogPriceUpdate
        with retry max 3
        with timeout 30s
      wait event CatalogPriceConfirmed
        correlate by state.requestId

    branch Search
      step SendSearchPriceUpdate
        with retry max 3
        with timeout 30s
      wait event SearchPriceConfirmed
        correlate by state.requestId

    branch Recommendation
      step SendRecommendationPriceUpdate
        with retry max 3
        with timeout 30s
      wait event RecommendationPriceConfirmed
        correlate by state.requestId

  when all

  if all branches succeeded
    publish event PriceUpdated
      with payload state
    end Completed

  publish event PriceUpdateFailed
    with payload state
  end Failed
```

### Semantics expressed here

- `workflow` defines a regular workflow, not a saga
- `state` defines business state
- `init`, `if`, `parallel`, `when all`, and `end` are control-flow primitives
- `step` is a business step
- `with retry` and `with timeout` are decorators
- `wait event` is an explicit wait primitive
- `publish event` is an explicit runtime-owned effect

## 3. Saga workflow pseudo-DSL

### Example: `OrderFulfillmentSaga`

```text
saga OrderFulfillmentSaga
  state OrderFulfillmentState

  init
    use request as state
    set state.status = Started

  compensation scope Fulfillment

    step ReserveInventory
      with retry max 3
      with timeout 30s
      compensate by ReleaseInventory

    step AuthorizePayment
      with retry max 2
      with timeout 20s
      compensate by RefundPayment

    step CreateShipment
      with timeout 60s
      compensate by CancelShipment

  publish event OrderFulfilled
    with payload state
  end Completed

  catch failure
    compensate scope Fulfillment
    publish event OrderFulfillmentCompensated
      with payload state
    end Compensated
```

### Semantics expressed here

- `saga` defines a different semantic kind than `workflow`
- compensation is first-class, not a regular workflow flag
- `compensation scope` marks compensatable forward execution
- `compensate by` binds forward and reverse actions
- `catch failure` expresses scoped failure semantics
- saga terminal states may differ from regular workflow terminal states

## 4. Durable-only behavior in pseudo-DSL

### Durable long wait example

```text
durable workflow CustomerApprovalWorkflow
  state ApprovalState

  init
    use request as state

  step SendApprovalRequest

  wait long event CustomerApproved
    correlate by state.customerId
    with timeout 7d

  if event received
    end Approved

  end TimedOut
```

Meaning:

- `wait long` is durable-only semantics
- it should not be available from ephemeral-only authoring/runtime surfaces
- timeout here is part of the wait policy, not a separate management command

### Ephemeral limitation example

```text
ephemeral workflow CustomerApprovalWorkflow
  state ApprovalState

  wait long event CustomerApproved
```

This should be rejected by API or fail fast by configuration because `wait long` is durable-only.

## 5. Step decorators and policy pseudo-DSL

### Step-level decorators

```text
step AuthorizePayment
  with retry max 2 backoff exponential
  with timeout 20s
  with idempotency key state.paymentRequestId
  with visibility tag Payment
```

### Scope-level decorators

```text
parallel
  with cancellation first winner cancels losers
  with timeout 2m
```

```text
compensation scope Fulfillment
  with compensation order reverse-success-order
  with failure policy fail-saga-if-compensation-fails
```

### Definition-level decorators

```text
workflow PriceUpdateWorkflow
  with retention keep completed 7d
  with stuck detection instance 10m step 2m
  with lifecycle publish all
```

### Rule

Policies modify behavior. They do not replace structure.

## 6. Fluent management pseudo-DSL

### Engine-wide scope

```text
WorkflowEngine
  .All()
  .Where(x => x.Status == Running)
  .Pause()
```

```text
WorkflowEngine
  .Where(x => x.IsStuck)
  .Statistics()
```

### Definition-scoped scope

```text
WorkflowEngine<PriceUpdateWorkflow>
  .Start(request)
```

```text
WorkflowEngine<PriceUpdateWorkflow>
  .StartOrGet(requestId, request)
```

```text
WorkflowEngine<PriceUpdateWorkflow>
  .Where(x => x.Status == Waiting)
  .List()
```

```text
WorkflowEngine<PriceUpdateWorkflow>
  .Where(x => x.Status == Running)
  .Pause()
```

### Instance-scoped scope

```text
WorkflowEngine
  .Instance(instanceId)
  .RaiseEvent(priceConfirmedEvent)
```

```text
WorkflowEngine
  .Instance(instanceId)
  .Pause()
```

```text
WorkflowEngine
  .Instance(instanceId)
  .Retry()
```

### Step-scoped management

```text
WorkflowEngine
  .Instance(instanceId)
  .Step(stepId)
  .Retry()
```

```text
WorkflowEngine
  .Instance(instanceId)
  .Step(stepId)
  .GetHistory()
```

### Saga-focused management

```text
WorkflowEngine
  .Instance(instanceId)
  .Saga()
  .GetCompensationState()
```

## 7. Query semantics rule

Management API should look LINQ-like, but `Where(...)` must remain constrained and translatable.

Good examples:

```text
.Where(x => x.Status == Running)
.Where(x => x.DefinitionVersion == version)
.Where(x => x.CreatedAt < cutoff)
```

Bad examples for public semantics:

```text
.Where(x => ExternalService.IsEligible(x.Id))
.Where(x => DateTime.Now > x.CreatedAt.AddDays(1))
.Where(x => CustomHelper(x))
```

## 8. Good and bad examples

### Good

```text
WorkflowEngine<OrderFulfillmentSaga>
  .Where(x => x.Status == Failed)
  .Retry()
```

Why:

- scope is explicit
- selection is explicit
- terminal command is explicit

### Bad

```text
WorkflowEngine<OrderFulfillmentSaga>.RetryAllFailed()
```

Why:

- selection is hidden inside the method name
- does not scale well as the API grows

### Good

```text
step SendInvoice
  with retry max 3
  with timeout 10s
```

### Bad

```text
Retry
  SendInvoice
Timeout
  10s
```

Why:

- retry and timeout are policies, not control-flow steps

## 9. Open syntax questions

This pseudo-DSL does not yet decide:

- whether final authoring syntax is fluent C# only, graph builder, attributes, or hybrid
- whether saga-specific blocks are different classes or different builders over shared base abstractions
- whether `Delay` should be separate from `Wait`
- whether `publish event` is a step or a returned runtime effect expression in final code

## 10. Final position

The current pseudo-DSL suggests that the design is internally coherent if OrcaCore keeps the following rules:

- workflow structure is separate from policy
- workflow semantics are separate from runtime guarantees
- management is separate from definition structure
- filtering is fluent and canonical through `Where(...)`
- durable-only constructs stay out of ephemeral-facing APIs

That is a good sign that the architecture direction is holding together.
