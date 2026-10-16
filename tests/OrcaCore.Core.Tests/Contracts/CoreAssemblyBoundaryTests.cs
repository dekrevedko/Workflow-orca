using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using OrcaCore.Core.Compilation;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class CoreAssemblyBoundaryTests
{
    [Fact]
    public void CoreAssembly_ExportsNoTypes()
    {
        var assembly = typeof(CompiledWorkflowPlan).Assembly;

        assembly.GetExportedTypes().Should().BeEmpty();
    }

    [Fact]
    public void CoreAssembly_GrantsOnlyTheExactImplementationAndOwningTestFriends()
    {
        var friendNames = typeof(CompiledWorkflowPlan).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        friendNames.Should().Equal(
            "OrcaCore.Core.Tests",
            "OrcaCore.Engine.Durable",
            "OrcaCore.Engine.Ephemeral");
    }

    [Fact]
    public void CoreAssembly_ContainsNoWhenFirstAuthoringEntryPoint()
    {
        var types = typeof(CompiledWorkflowPlan).Assembly.GetTypes();
        var methodNames = types
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly))
            .Select(method => method.Name);
        methodNames.Should().NotContain("WhenFirst");
    }
}
