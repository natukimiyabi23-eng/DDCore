using DDCore.Observation;

namespace DDCore.Observation.Tests;

public sealed class BossModIntegrationProbeTests
{
    [Fact]
    public void MissingEndpointLeavesDdDataUnavailable()
    {
        var diagnostics = new BossModIntegrationProbe(new FakeInvoker(hasFunction: false)).Check();

        Assert.Equal(BossModConnectionState.Unavailable, diagnostics.Connection);
        Assert.Equal(BossModDeepDungeonDataCapability.Unavailable, diagnostics.DeepDungeonData);
        Assert.Null(diagnostics.ProtocolVersion);
        Assert.Null(diagnostics.HasGancanaghModule);
        Assert.Equal("bossmod.ipc.endpoint-unavailable", diagnostics.ReasonCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SuccessfulInvocationProvesReachabilityButNotDdData(bool moduleExists)
    {
        var diagnostics = new BossModIntegrationProbe(new FakeInvoker(result: moduleExists)).Check();

        Assert.Equal(BossModConnectionState.ReachableUnversioned, diagnostics.Connection);
        Assert.Equal(BossModDeepDungeonDataCapability.Unavailable, diagnostics.DeepDungeonData);
        Assert.Null(diagnostics.ProtocolVersion);
        Assert.Equal(moduleExists, diagnostics.HasGancanaghModule);
        Assert.Equal("bossmod.ipc.reachable-no-dd-contract", diagnostics.ReasonCode);
    }

    [Fact]
    public void InvocationFailureIsFailClosed()
    {
        var diagnostics = new BossModIntegrationProbe(new FakeInvoker(exception: new InvalidOperationException())).Check();

        Assert.Equal(BossModConnectionState.IncompatibleOrFaulted, diagnostics.Connection);
        Assert.Equal(BossModDeepDungeonDataCapability.Unavailable, diagnostics.DeepDungeonData);
        Assert.Null(diagnostics.HasGancanaghModule);
        Assert.Equal("bossmod.ipc.invocation-failed:InvalidOperationException", diagnostics.ReasonCode);
    }

    [Fact]
    public void SubscriberCreationFailureDoesNotEscapeProbe()
    {
        var diagnostics = BossModIntegrationProbe.Check(
            () => throw new InvalidOperationException("type mismatch"));

        Assert.Equal(BossModConnectionState.IncompatibleOrFaulted, diagnostics.Connection);
        Assert.Equal(BossModDeepDungeonDataCapability.Unavailable, diagnostics.DeepDungeonData);
        Assert.Equal("bossmod.ipc.invocation-failed:InvalidOperationException", diagnostics.ReasonCode);
    }

    [Fact]
    public void ProbeUsesPinnedGancanaghObjectId()
    {
        var invoker = new FakeInvoker(result: true);

        _ = new BossModIntegrationProbe(invoker).Check();

        Assert.Equal(0x3D54u, invoker.LastDataId);
    }

    private sealed class FakeInvoker(
        bool hasFunction = true,
        bool result = false,
        Exception? exception = null) : IBossModIpcInvoker
    {
        public bool HasModuleLookupFunction => hasFunction;

        public uint? LastDataId { get; private set; }

        public bool HasModuleByDataId(uint dataId)
        {
            LastDataId = dataId;
            if (exception is not null)
            {
                throw exception;
            }

            return result;
        }
    }
}
