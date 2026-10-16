using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Providers.SqlServer;

static IServiceCollection ConfigureSqlServer(IServiceCollection services) =>
    services.AddOrcaCoreSqlServerDurableProvider(
        new SqlServerDurableProviderOptions(
            "Server=localhost;Database=orcacore;Integrated Security=true;TrustServerCertificate=true",
            "orcacore"));

_ = (Func<IServiceCollection, IServiceCollection>)ConfigureSqlServer;
