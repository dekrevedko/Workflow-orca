using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Hosting;
using OrcaCore.Providers.PostgreSql;

public static class Consumer
{
    public static IServiceCollection Configure(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddOrcaCore();
        services.AddOrcaCorePostgreSql(connectionString);
        _ = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value))
            .End("configured")
            .Build();
        return services;
    }

    public sealed record State(string Value);
}
