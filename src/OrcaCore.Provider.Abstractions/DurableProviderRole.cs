namespace OrcaCore.Provider.Abstractions;

/// <summary>
/// Identifies one complete durable persistence role registered by a provider package.
/// </summary>
public interface IDurableProviderRole
{
    /// <summary>Gets the stable provider role name.</summary>
    string Name { get; }

    /// <summary>Gets whether the role is intended only for development and tests.</summary>
    bool IsDevelopmentOnly { get; }
}
