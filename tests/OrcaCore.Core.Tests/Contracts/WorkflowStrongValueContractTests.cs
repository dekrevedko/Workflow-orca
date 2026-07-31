using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

[Trait("AC", "AC-019")]
public sealed class WorkflowStrongValueContractTests
{
    public static TheoryData<Type> CallerCreatedTypes => new()
    {
        typeof(EventName),
        typeof(WorkflowOutcomeName),
        typeof(AuthoredBranchId),
        typeof(ResourcePoolName),
        typeof(TransientPoolName),
        typeof(StartIdempotencyKey),
        typeof(CorrelationId),
        typeof(EventId),
        typeof(StopConfirmationId),
        typeof(ResourcePoolOperationId),
        typeof(ResourceGovernancePartitionId)
    };

    public static TheoryData<Type> RuntimeCreatedTypes => new()
    {
        typeof(InstanceId),
        typeof(WaitId),
        typeof(StepOperationId),
        typeof(LeaseProtectionToken)
    };

    [Theory]
    [MemberData(nameof(CallerCreatedTypes))]
    public void CallerCreatedValues_ExposeOnlyCreate(Type type)
    {
        type.IsClass.Should().BeTrue();
        type.IsSealed.Should().BeTrue();
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();

        var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
        staticMethods.Where(method => method.Name == "Create").Should().ContainSingle()
            .Which.GetParameters().Select(parameter => parameter.ParameterType).Should().Equal(typeof(string));
        staticMethods.Select(method => method.Name).Should().NotContain(["New", "Parse", "TryParse"]);
    }

    [Theory]
    [MemberData(nameof(CallerCreatedTypes))]
    public void CallerCreatedValues_ValidateWithoutNormalizing(Type type)
    {
        var create = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!;

        Action createNull = () => create.Invoke(null, [null]);
        Action createEmpty = () => create.Invoke(null, [string.Empty]);
        Action createWhitespace = () => create.Invoke(null, ["   "]);
        Action createPadded = () => create.Invoke(null, [" value "]);

        createNull.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentNullException>();
        createEmpty.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentException>();
        createWhitespace.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentException>();
        createPadded.Should().Throw<TargetInvocationException>().WithInnerException<ArgumentException>();

        var first = create.Invoke(null, ["Value"]);
        var same = create.Invoke(null, ["Value"]);
        var differentCase = create.Invoke(null, ["value"]);
        first.Should().Be(same);
        first.Should().NotBe(differentCase);
        type.GetProperty("Value")!.GetValue(first).Should().Be("Value");
    }

    [Theory]
    [MemberData(nameof(RuntimeCreatedTypes))]
    public void RuntimeCreatedValues_ExposeOnlyParseAndTryParse(Type type)
    {
        type.IsClass.Should().BeTrue();
        type.IsSealed.Should().BeTrue();
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();

        var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
        staticMethods.Select(method => method.Name).Should().Contain(["Parse", "TryParse"]);
        staticMethods.Select(method => method.Name).Should().NotContain("Create");
    }

    [Theory]
    [MemberData(nameof(CallerCreatedTypes))]
    public void CallerCreatedValues_RoundTripAsOrdinalJsonScalars(Type type)
    {
        var create = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!;
        var value = create.Invoke(null, ["Value-01"]);

        var json = JsonSerializer.Serialize(value, type);
        var roundTripped = JsonSerializer.Deserialize(json, type);

        json.Should().Be("\"Value-01\"");
        roundTripped.Should().Be(value);
    }

    [Fact]
    public void RuntimeCreatedStringValues_ValidateAndRoundTripAsJsonScalars()
    {
        AssertRuntimeStringValue(StepOperationId.Parse("operation-01"), "operation-01");
        AssertRuntimeStringValue(LeaseProtectionToken.Parse("protection-01"), "protection-01");

        StepOperationId.TryParse(" operation ", out var paddedOperation).Should().BeFalse();
        paddedOperation.Should().BeNull();
        LeaseProtectionToken.TryParse(string.Empty, out var emptyProtection).Should().BeFalse();
        emptyProtection.Should().BeNull();
    }

    private static void AssertRuntimeStringValue<T>(T value, string expected)
    {
        var json = JsonSerializer.Serialize(value);
        json.Should().Be($"\"{expected}\"");
        JsonSerializer.Deserialize<T>(json).Should().Be(value);
    }
}
