# Historical Requirements Tree

This folder preserves the earlier slice-oriented requirements decomposition. It is no longer
the active first-release baseline: use [the consolidated specs](../specs/README.md), especially
[the selected-mode matrix](../specs/17-selected-mode-capability-matrix.md), and the active
OpenSpec changes. Exact declarations are mirrored in
[`17-public-authoring-contract.cs`](../specs/17-public-authoring-contract.cs). Superseded names in the files below are retained as design history and must
not be copied into source or guards without an explicit matrix amendment.

It separates requirements by product track and by maturity:

- `regular/`
  - `initial/`: first implementation slice for regular workflows
  - `advanced/`: later regular-workflow capabilities beyond the minimal core
- `saga/`
  - `initial/`: first supported saga scope
  - `advanced/`: later saga capabilities and operational guarantees
- `durable/`
  - `initial/`: first durable execution scope
  - `advanced/`: later durable execution capabilities and scale/operability goals

This structure is intentional even though `workflow vs saga` and `ephemeral vs durable` are orthogonal axes.

Why:

- the implementation will be delivered in slices, not as one finished matrix
- the first slice was framed as `regular + ephemeral`; the codebase now also includes **durable state-driven** execution (`DurableWorkflowEngine` + `IWorkflowStore`) and an **event-driven prototype** — requirements remain sliced by track
- saga and durable each introduce enough distinct semantics that they need their own requirement and acceptance documents
- a separate durable track makes it easier to keep durable guarantees explicit and avoid pretending ephemeral mode is durable

Historical implementation target:

- [Regular / Initial requirements](regular/initial/requirements.md)
- [Regular / Initial acceptance criteria](regular/initial/acceptance-criteria.md)
- [Implementation plan for minimal core](../plans/implementation-plan-minimal-core.md)

Supporting baseline documents:

- [Design proposal: minimal core](../architecture/design-proposal-minimal-core.md)
- [Design decisions tracking](../architecture/design-decisions-tracking.md)
- [Workflow kinds and runtime modes](../architecture/workflow-kinds-and-runtime-modes.md)
- [Design synthesis](../architecture/design-synthesis.md)
- [Project technical overview / code map](../project-technical-overview.md)
