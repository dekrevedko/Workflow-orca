namespace OrcaCore.Engine.Durable.Internal;

internal static class DurableInboxPoisonCodes
{
    internal const string DefinitionBindingUnavailable = "definition-binding-unavailable";
    internal const string InboxContinuationFailed = "inbox-continuation-failed";
    internal const string InboxEnvelopeMissing = "inbox-envelope-missing";
    internal const string StartIntentUnresolvable = "start-intent-unresolvable";
    internal const string DirectTargetMissing = "direct-target-missing";
    internal const string TargetTerminal = "target-terminal";
    internal const string AmbiguousActiveWait = "ambiguous-active-wait";
    internal const string FanoutTargetMissing = "fanout-target-missing";
    internal const string FanoutTargetTerminal = "fanout-target-terminal";
    internal const string StartTargetMissing = "start-target-missing";
    internal const string StartTargetTerminal = "start-target-terminal";
    internal const string StartIntentInvalid = "start-intent-invalid";
    internal const string StartDefinitionVersionUnavailable = "start-definition-version-unavailable";
    internal const string StartBindingIncompatible = "start-binding-incompatible";
}
