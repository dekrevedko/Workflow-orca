namespace OrcaCore.DeveloperSurface.Guards;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CompileFixtureCollection
{
    public const string Name = "Compile fixture process isolation";
}
