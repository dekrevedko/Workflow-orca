using Xunit;
using System.Globalization;

namespace OrcaCore.Core.Tests.Compilation;

public sealed class OutcomeFingerprintTests
{
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
