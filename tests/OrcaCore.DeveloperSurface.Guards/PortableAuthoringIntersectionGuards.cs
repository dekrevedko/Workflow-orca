using System.Reflection;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class PortableAuthoringIntersectionGuards
{
    [Fact]
    public void ConcreteFacadeFamilies_HaveTheExactPortableMemberIntersections()
    {
        Intersect(
            typeof(EphemeralWorkflowBuilder<,>),
            typeof(DurableWorkflowBuilder<,>)).Should().Equal(
            "CompleteWithin`0(1)",
            "Delay`0(1)",
            "End`0(0)",
            "End`0(1)",
            "End`1(1)",
            "End`1(2)",
            "ForEach`3(4)",
            "If`0(3)",
            "Parallel`1(1)",
            "Then`1(0)",
            "Wait`0(2)",
            "Wait`0(3)",
            "Wait`1(2)",
            "Wait`1(3)",
            "While`0(2)",
            "WithRetry`0(2)",
            "WithStepTimeout`0(1)");

        Intersect(
            typeof(EphemeralNestedBuilder<,>),
            typeof(DurableNestedBuilder<,>)).Should().Equal(
            "Delay`0(1)",
            "If`0(3)",
            "Then`1(0)",
            "Wait`0(2)",
            "Wait`0(3)",
            "Wait`1(2)",
            "Wait`1(3)",
            "WithRetry`0(2)",
            "WithStepTimeout`0(1)");

        Intersect(
            typeof(EphemeralBranchBuilder<,>),
            typeof(DurableBranchBuilder<,>)).Should().Equal(
            "Delay`0(1)",
            "If`0(3)",
            "Return`0(1)",
            "Then`1(0)",
            "Wait`0(2)",
            "Wait`0(3)",
            "Wait`1(2)",
            "Wait`1(3)",
            "WithRetry`0(2)",
            "WithStepTimeout`0(1)");

        Intersect(
            typeof(EphemeralItemBuilder<,>),
            typeof(DurableItemBuilder<,>)).Should().Equal(
            "Delay`0(1)",
            "If`0(3)",
            "Return`0(1)",
            "Then`1(0)",
            "Wait`0(2)",
            "Wait`0(3)",
            "Wait`1(2)",
            "Wait`1(3)",
            "WithRetry`0(2)",
            "WithStepTimeout`0(1)");

        Intersect(
            typeof(DurableWorkflowBuilder<,>),
            typeof(DurableLeaseWorkflowBuilder<,>)).Should().Equal(
            "Delay`0(1)",
            "If`0(3)",
            "Publish`0(2)",
            "Publish`1(3)",
            "Then`1(0)",
            "Wait`0(2)",
            "Wait`0(3)",
            "Wait`1(2)",
            "Wait`1(3)",
            "WithRetry`0(2)",
            "WithStepTimeout`0(1)");

        Intersect(
            typeof(DurableNestedBuilder<,>),
            typeof(DurableLeaseNestedBuilder<,>)).Should().Equal(
            "Delay`0(1)",
            "If`0(3)",
            "Publish`0(2)",
            "Publish`1(3)",
            "Then`1(0)",
            "Wait`0(2)",
            "Wait`0(3)",
            "Wait`1(2)",
            "Wait`1(3)",
            "WithRetry`0(2)",
            "WithStepTimeout`0(1)");

        Intersect(
            typeof(DurableBranchBuilder<,>),
            typeof(DurableLeaseBranchBuilder<,>)).Should().Equal(
            "Delay`0(1)",
            "If`0(3)",
            "Publish`0(2)",
            "Publish`1(3)",
            "Return`0(1)",
            "Then`1(0)",
            "Wait`0(2)",
            "Wait`0(3)",
            "Wait`1(2)",
            "Wait`1(3)",
            "WithRetry`0(2)",
            "WithStepTimeout`0(1)");

        Intersect(
            typeof(DurableItemBuilder<,>),
            typeof(DurableLeaseItemBuilder<,>)).Should().Equal(
            "Delay`0(1)",
            "If`0(3)",
            "Publish`0(2)",
            "Publish`1(3)",
            "Return`0(1)",
            "Then`1(0)",
            "Wait`0(2)",
            "Wait`0(3)",
            "Wait`1(2)",
            "Wait`1(3)",
            "WithRetry`0(2)",
            "WithStepTimeout`0(1)");
    }

    [Fact]
    public void ConcreteFacades_DoNotIntroduceAPublicPortableBuilderAbstraction()
    {
        var facades = new[]
        {
            typeof(EphemeralWorkflowBuilder<,>), typeof(DurableWorkflowBuilder<,>),
            typeof(EphemeralNestedBuilder<,>), typeof(DurableNestedBuilder<,>),
            typeof(EphemeralBranchBuilder<,>), typeof(DurableBranchBuilder<,>),
            typeof(EphemeralItemBuilder<,>), typeof(DurableItemBuilder<,>),
            typeof(DurableLeaseWorkflowBuilder<,>), typeof(DurableLeaseNestedBuilder<,>),
            typeof(DurableLeaseBranchBuilder<,>), typeof(DurableLeaseItemBuilder<,>)
        };

        facades.Should().OnlyContain(type => type.IsSealed && type.BaseType == typeof(object));
        facades.SelectMany(type => type.GetInterfaces()).Should().BeEmpty();
        typeof(Workflow).Assembly.GetExportedTypes().Should().NotContain(type =>
            (type.IsInterface || type.IsAbstract) &&
            type.Name.Contains("Builder", StringComparison.Ordinal));
    }

    private static string[] Intersect(Type left, Type right)
    {
        var leftMembers = Members(left);
        var rightMembers = Members(right);
        return leftMembers.Intersect(rightMembers, StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] Members(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method =>
                $"{method.Name}`{method.GetGenericArguments().Length}({method.GetParameters().Length})")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
}
