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

public interface IExecutionGateway
{
    bool IsAvailable { get; }
    ExecutionResult TryExecute(ExecutionIntent intent);
}

public sealed class DisabledExecutionGateway : IExecutionGateway
{
    public bool IsAvailable => false;

    public ExecutionResult TryExecute(ExecutionIntent intent) =>
        new(ExecutionStatus.Disabled, "Phase 2にはゲーム操作機能が含まれていません。");
}
