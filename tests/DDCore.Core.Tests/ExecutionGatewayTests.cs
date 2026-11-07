using DDCore.Core.Execution;

namespace DDCore.Core.Tests;

public sealed class ExecutionGatewayTests
{
    [Fact]
    public void Phase2GatewayNeverExecutes()
    {
        var gateway = new DisabledExecutionGateway();
        var intent = new ExecutionIntent(Guid.NewGuid(), "test", 1, DateTimeOffset.UtcNow.AddMinutes(1));
        var safetyGate = new ThrowingSafetyGate();

        var result = gateway.TryExecute(intent, safetyGate);

        Assert.False(gateway.IsAvailable);
        Assert.Equal(ExecutionStatus.Disabled, result.Status);
    }

    [Fact]
    public void CoreAssemblyHasNoBossModOrDalamudReference()
    {
        var references = typeof(DisabledExecutionGateway).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain(references, name => name?.StartsWith("BossMod", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(references, name => name?.StartsWith("Dalamud", StringComparison.Ordinal) == true);
    }

    private sealed class ThrowingSafetyGate : IExecutionSafetyGate
    {
        public ExecutionSafetyValidation RevalidateImmediatelyBeforeExecution(ExecutionIntent intent) =>
            throw new InvalidOperationException("Disabled execution must not request authorization.");
    }
}
