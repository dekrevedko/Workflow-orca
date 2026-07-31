using System.Threading;

namespace OrcaCore.Core.Building;

internal enum AuthoringSessionState
{
    Open,
    JoinPending,
    Frozen
}

internal sealed class AuthoringLifecycleSession
{
    private readonly object operationGate = new();
    private int epoch;
    private AuthoringSessionState state;
    private string? transitionLocation;
    private string? activeOperationLocation;
    private int nextLexicalToken;
    private int nextJoinToken;
    private string? deadlineLocation;

    internal AuthoringLifecycleHandle CreateRootHandle() =>
        new(this, epoch, "workflow:$", lexicalToken: null);

    internal AuthoringLifecycleHandle CreateLexicalHandle(string location)
    {
        var token = new AuthoringLexicalToken(
            Interlocked.Increment(ref nextLexicalToken),
            location);
        return new AuthoringLifecycleHandle(this, epoch, location, token);
    }

    internal AuthoringJoinToken BeginJoin(
        AuthoringLifecycleHandle handle,
        string location)
    {
        using var operation = BeginMutation(handle, location);
        state = AuthoringSessionState.JoinPending;
        transitionLocation = location;
        return new AuthoringJoinToken(
            Interlocked.Increment(ref nextJoinToken),
            epoch,
            location);
    }

    internal AuthoringLifecycleHandle CompleteJoin(
        AuthoringJoinToken token,
        Action commit)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(commit);

        if (!Monitor.TryEnter(operationGate))
        {
            throw Lifecycle(
                "SFE-AUTH-LIFECYCLE-005",
                token.Location,
                activeOperationLocation ?? token.Location,
                "Another authoring operation is already in progress.");
        }

        activeOperationLocation = token.Location;
        try
        {
            if (token.IsSelected)
            {
                throw Lifecycle(
                    "SFE-AUTH-LIFECYCLE-002",
                    token.Location,
                    token.SelectedLocation ?? token.Location,
                    "This required join has already been selected.");
            }

            if (state == AuthoringSessionState.Frozen)
            {
                throw Lifecycle(
                    "SFE-AUTH-LIFECYCLE-003",
                    token.Location,
                    transitionLocation ?? token.Location,
                    "The authoring session is frozen.");
            }

            if (state != AuthoringSessionState.JoinPending || token.Epoch != epoch)
            {
                throw Lifecycle(
                    "SFE-AUTH-LIFECYCLE-001",
                    token.Location,
                    transitionLocation ?? token.Location,
                    "This join belongs to a superseded authoring epoch.");
            }

            commit();
            token.Select(token.Location);
            epoch++;
            state = AuthoringSessionState.Open;
            transitionLocation = token.Location;
            return CreateRootHandle();
        }
        finally
        {
            activeOperationLocation = null;
            Monitor.Exit(operationGate);
        }
    }

    internal IDisposable BeginMutation(
        AuthoringLifecycleHandle handle,
        string location)
    {
        ArgumentNullException.ThrowIfNull(handle);

        if (!Monitor.TryEnter(operationGate))
        {
            throw Lifecycle(
                "SFE-AUTH-LIFECYCLE-005",
                location,
                activeOperationLocation ?? handle.Location,
                "Another authoring operation is already in progress.");
        }

        activeOperationLocation = location;
        try
        {
            if (state == AuthoringSessionState.Frozen)
            {
                throw Lifecycle(
                    "SFE-AUTH-LIFECYCLE-003",
                    location,
                    transitionLocation ?? handle.Location,
                    "The authoring session is frozen.");
            }

            if (handle.LexicalToken is { IsActive: false } lexical)
            {
                throw Lifecycle(
                    "SFE-AUTH-LIFECYCLE-004",
                    location,
                    lexical.Location,
                    "This callback-local builder handle has expired.");
            }

            if (handle.LexicalToken is null &&
                (state == AuthoringSessionState.JoinPending || handle.Epoch != epoch))
            {
                throw Lifecycle(
                    "SFE-AUTH-LIFECYCLE-001",
                    location,
                    transitionLocation ?? handle.Location,
                    "This root builder handle has been superseded.");
            }

            return new OperationLease(this);
        }
        catch
        {
            activeOperationLocation = null;
            Monitor.Exit(operationGate);
            throw;
        }
    }

    internal void Freeze(
        AuthoringLifecycleHandle handle,
        string location,
        Action commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        using var operation = BeginMutation(handle, location);
        commit();
        state = AuthoringSessionState.Frozen;
        transitionLocation = location;
    }

    internal void SelectDeadline(
        AuthoringLifecycleHandle handle,
        string location,
        TimeSpan timeout)
    {
        using var operation = BeginMutation(handle, location);
        if (deadlineLocation is { } first)
        {
            throw global::OrcaCore.Core.Authoring.PublicAuthoringContracts.DuplicateDeadline(
                first,
                string.Equals(first, location, StringComparison.Ordinal)
                    ? NextSiblingLocation(location)
                    : location);
        }

        deadlineLocation = location;
        Deadline = timeout;
    }

    private static string NextSiblingLocation(string location)
    {
        const string marker = "/n:";
        var markerIndex = location.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0 ||
            !int.TryParse(location[(markerIndex + marker.Length)..], out var ordinal))
        {
            return $"{location}/n:00000001";
        }

        return $"{location[..markerIndex]}{marker}{checked(ordinal + 1):D8}";
    }

    internal TimeSpan? Deadline { get; private set; }

    private static global::OrcaCore.WorkflowDefinitionException Lifecycle(
        string code,
        string location,
        string relatedLocation,
        string message) =>
        global::OrcaCore.Core.Authoring.PublicAuthoringContracts.Lifecycle(
            code,
            location,
            relatedLocation,
            message);

    private void EndOperation()
    {
        activeOperationLocation = null;
        Monitor.Exit(operationGate);
    }

    private sealed class OperationLease(AuthoringLifecycleSession owner) : IDisposable
    {
        private AuthoringLifecycleSession? owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref owner, null)?.EndOperation();
        }
    }
}

internal sealed class AuthoringLifecycleHandle(
    AuthoringLifecycleSession session,
    int epoch,
    string location,
    AuthoringLexicalToken? lexicalToken)
{
    internal AuthoringLifecycleSession Session { get; } = session;

    internal int Epoch { get; } = epoch;

    internal string Location { get; } = location;

    internal AuthoringLexicalToken? LexicalToken { get; } = lexicalToken;

    internal IDisposable BeginMutation(string location) =>
        Session.BeginMutation(this, location);

    internal AuthoringLifecycleHandle CreateLexical(string location) =>
        Session.CreateLexicalHandle(location);

    internal void Expire() => LexicalToken?.Expire();
}

internal sealed class AuthoringLexicalToken(int id, string location)
{
    private int active = 1;

    internal int Id { get; } = id;

    internal string Location { get; } = location;

    internal bool IsActive => Volatile.Read(ref active) == 1;

    internal void Expire() => Interlocked.Exchange(ref active, 0);
}

internal sealed class AuthoringJoinToken(int id, int epoch, string location)
{
    private int selected;

    internal int Id { get; } = id;

    internal int Epoch { get; } = epoch;

    internal string Location { get; } = location;

    internal bool IsSelected => Volatile.Read(ref selected) == 1;

    internal string? SelectedLocation { get; private set; }

    internal void Select(string location)
    {
        SelectedLocation = location;
        Volatile.Write(ref selected, 1);
    }
}
