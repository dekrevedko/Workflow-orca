using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Hosting;

public static class Consumer
{
    public static IServiceProvider Compose()
    {
        var services = new ServiceCollection();
        services.AddOrcaCore();
        _ = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value))
            .End("ready")
            .Build();
        return services.BuildServiceProvider();
    }

    public sealed record State(string Value);
}
