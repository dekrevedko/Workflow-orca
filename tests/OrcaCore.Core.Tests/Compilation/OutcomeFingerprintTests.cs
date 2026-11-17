using Xunit;
using System.Globalization;

namespace OrcaCore.Core.Tests.Compilation;

public sealed class OutcomeFingerprintTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FingerprintPreservesEveryAcceptedUtf16CodeUnit(bool durable)
    {
        var id = DefinitionId.New();
        string[] names = ["outcome\ud800", "outcome\ud801", "outcome\udc00", "outcome\ufffd",
            "outcome\\ud800", "outcomeé", "outcomee\u0301", "outcome\ud83d\ude80"];
        var fingerprints = names.Select(name => durable
            ? Workflow.Durable<State>(id, DefinitionVersion.Initial).Init<int>(v => new State(v))
                .End(WorkflowOutcomeName.Create(name)).Build().DefinitionFingerprint
            : Workflow.Ephemeral<State>(id, DefinitionVersion.Initial).Init<int>(v => new State(v))
                .End(WorkflowOutcomeName.Create(name)).Build().DefinitionFingerprint).ToArray();
        Assert.Equal(names.Length, fingerprints.Distinct().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EventNamesCannotInjectAnotherWaitIntoTheFingerprint(bool durable)
    {
        var id = DefinitionId.New();
        var injected = Contract($"x:1:{(durable ? "Cold" : "Resident")}::opaque>;Wait<y");
        var x = Contract("x");
        var y = Contract("y");
        var one = durable
            ? Workflow.Durable<State>(id, DefinitionVersion.Initial).Init<int>(v => new State(v))
                .Wait(injected, _ => CorrelationId.Create("key")).End().Build().DefinitionFingerprint
            : Workflow.Ephemeral<State>(id, DefinitionVersion.Initial).Init<int>(v => new State(v))
                .Wait(injected, _ => CorrelationId.Create("key")).End().Build().DefinitionFingerprint;
        var two = durable
            ? Workflow.Durable<State>(id, DefinitionVersion.Initial).Init<int>(v => new State(v))
                .Wait(x, _ => CorrelationId.Create("key")).Wait(y, _ => CorrelationId.Create("key")).End().Build().DefinitionFingerprint
            : Workflow.Ephemeral<State>(id, DefinitionVersion.Initial).Init<int>(v => new State(v))
                .Wait(x, _ => CorrelationId.Create("key")).Wait(y, _ => CorrelationId.Create("key")).End().Build().DefinitionFingerprint;
        Assert.NotEqual(one, two);

        static WorkflowEventContract Contract(string name) =>
            WorkflowEventContract.Create(EventName.Create(name), EventContractVersion.Initial);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiteralUnnamedOutcomeDoesNotAliasAnAbsentOutcome(bool durable)
    {
        var id = DefinitionId.New();
        var unnamed = durable
            ? Workflow.Durable<State>(id, DefinitionVersion.Initial).Init<int>(v => new State(v)).End(s => s.Value.Value).Build().DefinitionFingerprint
            : Workflow.Ephemeral<State>(id, DefinitionVersion.Initial).Init<int>(v => new State(v)).End(s => s.Value.Value).Build().DefinitionFingerprint;
        var named = durable
            ? Workflow.Durable<State>(id, DefinitionVersion.Initial).Init<int>(v => new State(v)).End(s => s.Value.Value, WorkflowOutcomeName.Create("unnamed")).Build().DefinitionFingerprint
            : Workflow.Ephemeral<State>(id, DefinitionVersion.Initial).Init<int>(v => new State(v)).End(s => s.Value.Value, WorkflowOutcomeName.Create("unnamed")).Build().DefinitionFingerprint;
        Assert.NotEqual(unnamed, named);
    }

    public sealed record State(int Value);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OutcomeEncodingIsStableAcrossCulturesAndDistinguishesTagsAndPunctuation(bool durable)
    {
        var original = CultureInfo.CurrentCulture;
        var id = DefinitionId.New();
        string[] names = ["unnamed", "absent", "selector:opaque", "named:6:absent", "ok:<>;é", "ok:<>;e"];
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var expected = names.Select(Fingerprint).ToArray();
            Assert.Equal(names.Length, expected.Distinct().Count());
            foreach (var culture in new[] { "tr-TR", "ar-SA", "fr-FR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Assert.Equal(expected, names.Select(Fingerprint));
            }
        }
        finally { CultureInfo.CurrentCulture = original; }

        DefinitionFingerprint Fingerprint(string name) => durable
            ? Workflow.Durable<State>(id, DefinitionVersion.Initial).Init<int>(v => new State(v))
                .End(s => s.Value.Value, WorkflowOutcomeName.Create(name)).Build().DefinitionFingerprint
            : Workflow.Ephemeral<State>(id, DefinitionVersion.Initial).Init<int>(v => new State(v))
                .End(s => s.Value.Value, WorkflowOutcomeName.Create(name)).Build().DefinitionFingerprint;
    }
}
