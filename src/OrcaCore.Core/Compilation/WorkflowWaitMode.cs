namespace OrcaCore.Core.Compilation;

/// <summary>
/// Runtime/compiler wait-residency marker. It is not an application-facing authoring value.
/// </summary>
internal enum WorkflowWaitMode
{
    Resident,
    Cold
}
