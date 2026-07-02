namespace OrcaCore.TestSupport;

/// <summary>
/// Marker documenting this project's scope. <c>OrcaCore.TestSupport</c> holds hand-rolled port
/// fakes, object-mother builders, and deterministic time/concurrency harnesses (<see cref="Time.Clock"/>,
/// <see cref="Concurrency.RaceCoordinator"/>) shared across every test project and the
/// certification suite. It never contains assertion helpers (those are AwesomeAssertions calls
/// in the tests themselves) or production logic (that belongs in <c>src/</c>).
/// </summary>
internal static class ProjectScope
{
}
