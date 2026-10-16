using OrcaCore.Examples;

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

await ExampleRunner.RunAllAsync(shutdown.Token).ConfigureAwait(false);
