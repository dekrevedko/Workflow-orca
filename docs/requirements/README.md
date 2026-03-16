# Requirements Tree

This folder is the active requirements baseline for OrcaCore.

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
- the current first slice is `regular + ephemeral`
- saga and durable each introduce enough distinct semantics that they need their own requirement and acceptance documents
- a separate durable track makes it easier to keep durable guarantees explicit and avoid pretending ephemeral mode is durable

Current implementation target:

- [Regular / Initial requirements](/X:/Projects/GitHub/Workflow-orca/docs/requirements/regular/initial/requirements.md)
- [Regular / Initial acceptance criteria](/X:/Projects/GitHub/Workflow-orca/docs/requirements/regular/initial/acceptance-criteria.md)
- [Implementation plan for minimal core](/X:/Projects/GitHub/Workflow-orca/docs/implementation-plan-minimal-core.md)

Supporting baseline documents:

- [Design proposal: minimal core](/X:/Projects/GitHub/Workflow-orca/docs/design-proposal-minimal-core.md)
- [Design decisions tracking](/X:/Projects/GitHub/Workflow-orca/docs/design-decisions-tracking.md)
- [Workflow kinds and runtime modes](/X:/Projects/GitHub/Workflow-orca/docs/workflow-kinds-and-runtime-modes.md)
- [Design synthesis](/X:/Projects/GitHub/Workflow-orca/docs/design-synthesis.md)
