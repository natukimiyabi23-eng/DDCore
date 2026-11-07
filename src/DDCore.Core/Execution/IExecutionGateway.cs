namespace DDCore.Core.Execution;

public sealed record ExecutionIntent(
    Guid Id,
    string Kind,
    long SnapshotSequence,
    DateTimeOffset ExpiresAt);

public enum ExecutionStatus
{
    Disabled,
    Rejected,
    Succeeded,
    Failed,
    TimedOut,
}

public sealed record ExecutionResult(ExecutionStatus Status, string Reason);

public sealed record ExecutionSafetyValidation(
    bool IsSafe,
    long ValidatedSnapshotSequence,
    string Reason);

/// <summary>
/// Produces a fresh safety decision from the current game and controller state.
/// A cached validation is not valid for a later operation.
/// </summary>
public interface IExecutionSafetyGate
{
    ExecutionSafetyValidation RevalidateImmediatelyBeforeExecution(ExecutionIntent intent);
}

public interface IExecutionGateway
{
    bool IsAvailable { get; }

    /// <summary>
    /// Operation-capable implementations must call <paramref name="safetyGate"/> inside this
    /// method immediately before their first external side effect. They must reject expired
    /// intents, emergency-stop state, unarmed sessions, disallowed modes, and snapshot changes.
    /// A validation performed by the caller before this method is never sufficient.
    /// Disabled implementations may return <see cref="ExecutionStatus.Disabled"/> without
    /// invoking the gate because they cannot create a side effect.
    /// </summary>
    ExecutionResult TryExecute(ExecutionIntent intent, IExecutionSafetyGate safetyGate);
}

public sealed class DisabledExecutionGateway : IExecutionGateway
{
    public bool IsAvailable => false;

    public ExecutionResult TryExecute(ExecutionIntent intent, IExecutionSafetyGate safetyGate) =>
        new(ExecutionStatus.Disabled, "Phase 2にはゲーム操作機能が含まれていません。");
}
