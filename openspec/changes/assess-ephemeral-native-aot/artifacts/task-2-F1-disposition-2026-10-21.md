# Verdict F1 disposition — task 2 candidate, 2026-10-21

Authority: approved contract eea51c9 / immutable verdict-only child dbd829e.

The six required SDK-default cells remain unchanged in identity; the switch is unset.
Four additional cells explicitly enable JsonSerializerIsReflectionEnabledByDefault,
retain their own settings/artifacts/warnings/outcomes, and are at most conditional.
The switch is a candidate LocalRequirement, never a codec replacement or product fix.
Its sufficiency is not assumed: later failures remain unsupported/unclassified.

The [official .NET guidance](https://learn.microsoft.com/en-us/dotnet/core/compatibility/serialization/8.0/publishtrimmed)
explains the default reflection change for trimmed publication. The protocol and
canonical text require the effective public feature value plus exact failure phase,
exception/inner messages, public code/diagnostics and separate default/extra records.

Local Windows 11 discovery (not required native qualification) observed:

| Flavor | Effective JSON reflection | Passed scenarios | Diagnostic identities / occurrences |
|---|---|---:|---:|
| untrimmed JIT | true (unset) | 16/16 | 0 / 0 |
| trimmed | false (unset) | 1/16 | 21 / 28 |
| Native AOT | false (unset) | 1/16 | 51 / 51 |
| trimmed JSON-reflection extra | true (explicit) | 1/16 | 21 / 28 |
| AOT JSON-reflection extra | true (explicit) | 1/16 | 51 / 51 |

Default AOT-01 failed at DefinitionBuild with OrcaCore.WorkflowDefinitionException,
public code WF-DEFINITION-INVALID and SFE-TYPE-002 diagnostics, including:
`No serializer schema is registered for 'Data'.` The true-switch extras reached
Start, then System.InvalidOperationException: `Sequence contains no matching element`.
The original messages and stacks remain in raw receipts. No default failure is
relabelled a codec exception or replaced by an extra. This discovery predates final
fixture refinements and remains separate from required native execution evidence.

The new 90-row static inventory adds DefinitionCompilerOptions.cs:58 and its exact
line hash. The approved dated 89-row inventory and all contract artifacts/verdicts
remain unchanged. No serializer option/resolver, private/whole-assembly rooting,
NoWarn, product annotation, source or dependency change is made.

Fixture precision learned from existing normative/source controls is also explicit:
local structured bodies are cooperative, command cancellation can await its serialized
terminal commit, strong-value factories reject edge whitespace, and the 256 KiB
case uses the actual ForEach snapshot boundary (SFE-LIMIT-011), before any item body.
These strengthen the public consumer and add no runtime behavior or API.

Task 2 cannot be frozen as green until both native hosts, individual findings and
the complete assessment consistency gate have been validated. That complete frozen
target then requires independent review before checkpointing or publication.
