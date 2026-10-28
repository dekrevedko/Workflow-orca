# Summary
<details open><summary>Summary</summary>

|||
|:---|:---|
| Generated on: | 10/27/2026 - 10:07:20 PM |
| Coverage date: | 10/27/2026 - 10:03:06 PM - 10/27/2026 - 10:06:44 PM |
| Parser: | MultiReport (14x Cobertura) |
| Assemblies: | 2 |
| Classes: | 180 |
| Files: | 122 |
| **Line coverage:** | 79.3% (15952 of 20112) |
| Covered lines: | 15952 |
| Uncovered lines: | 4160 |
| Coverable lines: | 20112 |
| Total lines: | 25866 |
| **Branch coverage:** | 66.3% (4206 of 6336) |
| Covered branches: | 4206 |
| Total branches: | 6336 |
| **Method coverage:** | [Feature is only available for sponsors](https://reportgenerator.io/pro) |

</details>

## Coverage
<details><summary>OrcaCore.Engine.Durable - 81.6%</summary>

|**Name**|**Line**|**Branch**|
|:---|---:|---:|
|**OrcaCore.Engine.Durable**|**81.6%**|**67.3%**|
|OrcaCore.Engine.Durable.Aggregates.DurableActiveTimer|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableActiveWait|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableAggregateCheckpoint|95.6%||
|OrcaCore.Engine.Durable.Aggregates.DurableAggregateSnapshot|50%||
|OrcaCore.Engine.Durable.Aggregates.DurableAggregateState|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableCompleteCommand|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableContinuationAttemptFailedCommand|37.5%||
|OrcaCore.Engine.Durable.Aggregates.DurableContinuationAttemptResetCommand|0%||
|OrcaCore.Engine.Durable.Aggregates.DurableDecision|100%|100%|
|OrcaCore.Engine.Durable.Aggregates.DurableFailCommand|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableFiberFailedCommand|93.3%||
|OrcaCore.Engine.Durable.Aggregates.DurableLeaseStopConfirmedCommand|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableLifecycleCommandHandler|82%|56.4%|
|OrcaCore.Engine.Durable.Aggregates.DurableParkCommand|100%||
|OrcaCore.Engine.Durable.Aggregates.DurablePendingResume|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableQueuedResourceCancellation|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableResourcePoolAcquirePlan|75%||
|OrcaCore.Engine.Durable.Aggregates.DurableResourcePoolCommandHandler|82.1%|58.3%|
|OrcaCore.Engine.Durable.Aggregates.DurableResourcePoolEventContext|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableResourcePoolReplayEffects|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableResourcePoolState|98%|77.7%|
|OrcaCore.Engine.Durable.Aggregates.DurableStepCompletedCommand|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableStepFailedCommand|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableTerminalLifecycleCommand|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableTimeoutCommand|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableTimerState|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableUnparkCommand|0%||
|OrcaCore.Engine.Durable.Aggregates.DurableWaitMatchedCommand|0%||
|OrcaCore.Engine.Durable.Aggregates.DurableWaitRegisteredCommand|100%||
|OrcaCore.Engine.Durable.Aggregates.DurableWaitState|100%|65.9%|
|OrcaCore.Engine.Durable.Aggregates.DurableWaitTimerCommandHandler|70.6%|60%|
|OrcaCore.Engine.Durable.Aggregates.DurableWorkflowAggregate|86.8%|73.8%|
|OrcaCore.Engine.Durable.Aggregates.DurableWorkflowReplayApplier|87.5%|77.3%|
|OrcaCore.Engine.Durable.Aggregates.DurableYieldCommand|100%||
|OrcaCore.Engine.Durable.Building.DurableWaitDefinition`1|0%||
|OrcaCore.Engine.Durable.Building.DurableWorkflowBuilder`1|0%||
|OrcaCore.Engine.Durable.Definitions.DurableDefinitionKey|50%||
|OrcaCore.Engine.Durable.Definitions.DurableDefinitionRegistry|71.1%|58.3%|
|OrcaCore.Engine.Durable.Definitions.DurableRegisteredDefinition|0%||
|OrcaCore.Engine.Durable.Definitions.DurableRegisteredDefinitionBinding|50%||
|OrcaCore.Engine.Durable.Diagnostics.OrcaCoreDurableDiagnostics|89.1%|75%|
|OrcaCore.Engine.Durable.Driver.DurableContinuationObservation|100%||
|OrcaCore.Engine.Durable.Driver.DurableContinuationPump|66.8%|58.8%|
|OrcaCore.Engine.Durable.Driver.DurableDriverBudget|100%||
|OrcaCore.Engine.Durable.Driver.DurableDriverCatalog|72.7%|50%|
|OrcaCore.Engine.Durable.Driver.DurableDriverContext|100%||
|OrcaCore.Engine.Durable.Driver.DurableDriverSegmentObservation|100%||
|OrcaCore.Engine.Durable.Driver.DurableFiberDriverExecutor`1|74.5%|66.1%|
|OrcaCore.Engine.Durable.Driver.DurableFiberEnvelopeMapper|95.6%|62.5%|
|OrcaCore.Engine.Durable.Driver.DurableFiberEnvelopeValidation|75%||
|OrcaCore.Engine.Durable.Driver.DurableFiberEnvelopeValidator|48.4%|50%|
|OrcaCore.Engine.Durable.Driver.DurableInboxContinuationPump|63.3%|62.7%|
|OrcaCore.Engine.Durable.Driver.DurablePolicyWinnerSelector|100%|100%|
|OrcaCore.Engine.Durable.Driver.DurableResourceLeaseDiagnostics|97.8%|71.7%|
|OrcaCore.Engine.Durable.Driver.DurableResourceLeaseRecovery|87.7%|73.1%|
|OrcaCore.Engine.Durable.Driver.DurableSegmentResult|91.6%||
|OrcaCore.Engine.Durable.Driver.DurableStepThrottleCoordinator|91.9%|58.3%|
|OrcaCore.Engine.Durable.Driver.DurableStepThrottleLease|100%|100%|
|OrcaCore.Engine.Durable.Driver.DurableWorkflowDriver|62.4%|53.5%|
|OrcaCore.Engine.Durable.Driver.NullDurableDriverObserver|100%||
|OrcaCore.Engine.Durable.Driver.StepContextContracts|100%||
|OrcaCore.Engine.Durable.Driver.StepThrottleDebugSnapshot|100%||
|OrcaCore.Engine.Durable.DurableFacadeNotificationHub|100%|100%|
|OrcaCore.Engine.Durable.DurableStagedWorkflowDefinition`1|100%|50%|
|OrcaCore.Engine.Durable.DurableStagedWorkflowDefinition`2|80%|50%|
|OrcaCore.Engine.Durable.DurableWaitResidency|83.3%|75%|
|OrcaCore.Engine.Durable.DurableWorkflowDefinitionRegistry|91.2%|72.1%|
|OrcaCore.Engine.Durable.DurableWorkflowEventIngressCore|86.8%|72.2%|
|OrcaCore.Engine.Durable.Execution.DurableAggregateLoader|100%|100%|
|OrcaCore.Engine.Durable.Execution.DurableCheckpointMapper|100%||
|OrcaCore.Engine.Durable.Execution.DurableCommandProcessor|86.9%|60.9%|
|OrcaCore.Engine.Durable.Execution.DurableCommandResult|100%||
|OrcaCore.Engine.Durable.Execution.DurableCommandRuntime|86.6%|66.6%|
|OrcaCore.Engine.Durable.Execution.DurableCommandTelemetry|94.9%|74.7%|
|OrcaCore.Engine.Durable.Execution.DurableCommitMaterializer|88%|85.7%|
|OrcaCore.Engine.Durable.Execution.DurableCommitPipeline|61.3%|71.4%|
|OrcaCore.Engine.Durable.Execution.DurableCorrelationRouteResult|0%||
|OrcaCore.Engine.Durable.Execution.DurableEventEnvelopeFingerprint|93.2%|78.5%|
|OrcaCore.Engine.Durable.Execution.DurableFacadeStartResult|100%||
|OrcaCore.Engine.Durable.Execution.DurableInboundEventNormalizer|98.3%|70%|
|OrcaCore.Engine.Durable.Execution.DurableInboxDelivery|100%||
|OrcaCore.Engine.Durable.Execution.DurableInboxPreflight|11.7%|30%|
|OrcaCore.Engine.Durable.Execution.DurableRearmRequest|0%||
|OrcaCore.Engine.Durable.Execution.DurableResourcePoolCommitEffects|100%|87.5%|
|OrcaCore.Engine.Durable.Execution.DurableStartService|97.6%|89.4%|
|OrcaCore.Engine.Durable.Execution.DurableWorkflowInputFingerprint|100%||
|OrcaCore.Engine.Durable.Execution.DurableWorkflowRuntime|79%|74.2%|
|OrcaCore.Engine.Durable.Execution.DurableWorkflowStartResult|60%||
|OrcaCore.Engine.Durable.Execution.JsonWorkflowPayloadSerializer|80%|50%|
|OrcaCore.Engine.Durable.Execution.NormalizedDurableInboundEvent|100%||
|OrcaCore.Engine.Durable.Execution.NullWorkflowRuntimeObserver|100%||
|OrcaCore.Engine.Durable.Execution.PendingStartMaterializationResult|88.8%||
|OrcaCore.Engine.Durable.Execution.SerializedPayload|100%||
|OrcaCore.Engine.Durable.Execution.StartOrGetRequest|100%||
|OrcaCore.Engine.Durable.Execution.StartOrGetResult|100%||
|OrcaCore.Engine.Durable.Execution.WorkflowProviderCommitFailureObservation|28.5%||
|OrcaCore.Engine.Durable.Execution.WorkflowResourcePoolTelemetryItem|100%||
|OrcaCore.Engine.Durable.Execution.WorkflowRuntimeEventObservation|95.2%||
|OrcaCore.Engine.Durable.Execution.WorkflowRuntimeObservation|77.2%||
|OrcaCore.Engine.Durable.Execution.WorkflowRuntimeTelemetryContext|100%||
|OrcaCore.Engine.Durable.Internal.DurableContractAdapter|90.9%|54.1%|
|OrcaCore.Engine.Durable.Outbox.DurableOutboxPump|85.6%|73.3%|
|OrcaCore.Engine.Durable.Outbox.DurableWorkflowOutboundEventCodec|85.7%|62.5%|
|OrcaCore.Engine.Durable.Outbox.DurableWorkflowOutboundEventData|100%||
|OrcaCore.Engine.Durable.Outbox.IOutboxPumpObserver|100%||
|OrcaCore.Engine.Durable.Outbox.OutboxDispatchObservation|42.8%||
|OrcaCore.Engine.Durable.Outbox.OutboxDispatchOutcome|100%||
|OrcaCore.Engine.Durable.Outbox.OutboxKindDispatcherRoute|100%||
|OrcaCore.Engine.Durable.Outbox.OutboxKindMessageDispatcher|84.2%|75%|
|OrcaCore.Engine.Durable.Outbox.OutboxPumpObservation|100%||
|OrcaCore.Engine.Durable.ResourceGovernance.DurableResourceLeaseCommitBarrie<br/>rFact|91.6%||
|OrcaCore.Engine.Durable.ResourceGovernance.NullDurableResourceLeaseCertific<br/>ationGate|100%||
|OrcaCore.Engine.Durable.Versioning.DurableVersionCompatibility|0%|0%|

</details>
<details><summary>OrcaCore.Engine.Ephemeral - 77%</summary>

|**Name**|**Line**|**Branch**|
|:---|---:|---:|
|**OrcaCore.Engine.Ephemeral**|**77%**|**65.5%**|
|OrcaCore.Engine.Ephemeral|76.3%|64.5%|
|OrcaCore.Engine.Ephemeral.Diagnostics.EphemeralOperatorStatistics|80%||
|OrcaCore.Engine.Ephemeral.Diagnostics.EphemeralOperatorStatisticsGroup|100%||
|OrcaCore.Engine.Ephemeral.Diagnostics.OrcaCoreEphemeralDiagnostics|80.4%|56.6%|
|OrcaCore.Engine.Ephemeral.EphemeralDefinitionRuntime|88.8%||
|OrcaCore.Engine.Ephemeral.EphemeralEventRouteResult|100%||
|OrcaCore.Engine.Ephemeral.EphemeralStagedWorkflowDefinition`1|100%|50%|
|OrcaCore.Engine.Ephemeral.EphemeralStagedWorkflowDefinition`2|83.3%|50%|
|OrcaCore.Engine.Ephemeral.EphemeralTerminalRetentionSettings|100%||
|OrcaCore.Engine.Ephemeral.EphemeralWorkflowDefinitionRegistry|93.6%|84.3%|
|OrcaCore.Engine.Ephemeral.EphemeralWorkflowEngine|72%|60.7%|
|OrcaCore.Engine.Ephemeral.EphemeralWorkflowEngineOptions|100%||
|OrcaCore.Engine.Ephemeral.EphemeralWorkflowEventRouter|92.1%|81.2%|
|OrcaCore.Engine.Ephemeral.Execution.ConditionEvaluator`1|10%|0%|
|OrcaCore.Engine.Ephemeral.Execution.EphemeralActiveStepSnapshot|100%||
|OrcaCore.Engine.Ephemeral.Execution.EphemeralActiveWaitSnapshot|100%||
|OrcaCore.Engine.Ephemeral.Execution.EphemeralCompositionBranchOutcomeSnapsh<br/>ot|0%||
|OrcaCore.Engine.Ephemeral.Execution.EphemeralForEachGroupSnapshot|0%||
|OrcaCore.Engine.Ephemeral.Execution.EphemeralForEachWorkItemSnapshot|0%||
|OrcaCore.Engine.Ephemeral.Execution.EphemeralInstanceLifetime|98.5%|79.7%|
|OrcaCore.Engine.Ephemeral.Execution.EphemeralLifecycleEventSnapshot|100%||
|OrcaCore.Engine.Ephemeral.Execution.EphemeralRetentionCoordinator|95.1%|85.9%|
|OrcaCore.Engine.Ephemeral.Execution.EphemeralRoutingIndex|64.1%|75%|
|OrcaCore.Engine.Ephemeral.Execution.EphemeralWorkflowInstanceSnapshot|100%||
|OrcaCore.Engine.Ephemeral.Execution.InMemoryExecutionStateAdapter`1|88.6%|71.8%|
|OrcaCore.Engine.Ephemeral.Execution.InMemoryInstanceRegistry|57.1%|33.3%|
|OrcaCore.Engine.Ephemeral.Execution.InstanceExecutionLane|91.1%|73.3%|
|OrcaCore.Engine.Ephemeral.Execution.Interpreter`1|28.9%|4.1%|
|OrcaCore.Engine.Ephemeral.Execution.InterpreterFactory|100%||
|OrcaCore.Engine.Ephemeral.Execution.InterpreterRunState`1|0%||
|OrcaCore.Engine.Ephemeral.Execution.IWorkflowInstance|0%||
|OrcaCore.Engine.Ephemeral.Execution.ParallelJoin|0%|0%|
|OrcaCore.Engine.Ephemeral.Execution.ResumeEventSlot|0%||
|OrcaCore.Engine.Ephemeral.Execution.RuntimeTimerRecord|92.3%|50%|
|OrcaCore.Engine.Ephemeral.Execution.RuntimeWaitRecord|100%|100%|
|OrcaCore.Engine.Ephemeral.Execution.SequenceExecutionContext`2|0%||
|OrcaCore.Engine.Ephemeral.Execution.StepContextContracts|100%||
|OrcaCore.Engine.Ephemeral.Execution.StepExecutionResult|0%||
|OrcaCore.Engine.Ephemeral.Execution.StepExecutor`1|6.9%|0%|
|OrcaCore.Engine.Ephemeral.Execution.StructuredEphemeralValueCodec|100%||
|OrcaCore.Engine.Ephemeral.Execution.SuspensionScheduler`1|44.7%|12.5%|
|OrcaCore.Engine.Ephemeral.Execution.WaitExecutor`1|12%|0%|
|OrcaCore.Engine.Ephemeral.Execution.WhileNodeRunner`1|2.9%|0%|
|OrcaCore.Engine.Ephemeral.Execution.WorkflowErrorDetails|100%||
|OrcaCore.Engine.Ephemeral.Execution.WorkflowFailureHandler`1|11.1%||
|OrcaCore.Engine.Ephemeral.Execution.WorkflowInstance`1|69.7%|56%|
|OrcaCore.Engine.Ephemeral.Execution.WorkflowLifecycleTransition|75%|50%|
|OrcaCore.Engine.Ephemeral.Execution.YieldContinuationScheduler|75%|75%|
|OrcaCore.Engine.Ephemeral.FacadeValueFactory|95.6%||
|OrcaCore.Engine.Ephemeral.Governance.GovernanceDebugSnapshot|0%||
|OrcaCore.Engine.Ephemeral.Governance.GovernanceLease|85.7%|90%|
|OrcaCore.Engine.Ephemeral.Governance.ResourceGovernanceCoordinator|79.3%|88.3%|
|OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter|100%|50%|
|OrcaCore.Engine.Ephemeral.Timers.EphemeralTimerService|96.2%|63.4%|
|OrcaCore.Engine.Ephemeral.Timers.ScheduledTimer|87.5%|66.6%|
|OrcaCore.Hosting.EngineRoleRegistration|87.5%|87.5%|
|OrcaCore.Hosting.EphemeralEngineHostOptions|87%|91.6%|
|OrcaCore.Hosting.EphemeralHostLifetime|100%|92.5%|
|OrcaCore.Hosting.EphemeralHostShutdownIncompleteException|100%||
|OrcaCore.Hosting.EphemeralHostShutdownResult|100%||
|OrcaCore.Hosting.EphemeralHostStoppingException|100%||
|OrcaCore.Hosting.EphemeralTerminalRetentionPolicy|100%|100%|
|OrcaCore.Hosting.OrcaCoreEphemeralEngineBuilder|100%|50%|
|OrcaCore.Hosting.OrcaCoreEphemeralEngineServiceCollectionExtensions|100%|100%|
|OrcaCore.Hosting.OrcaCoreEphemeralWorkflowCatalogReadinessHostedService|88.8%|100%|
|OrcaCore.Hosting.TransientPoolDefinition|100%||
|OrcaCore.Hosting.ValidatedEphemeralEngineHostOptions|100%||

</details>
