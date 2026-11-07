namespace DDCore.Observation;

public enum BossModConnectionState
{
    NotChecked,
    Unavailable,
    ReachableUnversioned,
    IncompatibleOrFaulted,
}

public enum BossModDeepDungeonDataCapability
{
    Unavailable,
}

public sealed record BossModIntegrationDiagnostics(
    BossModConnectionState Connection,
    BossModDeepDungeonDataCapability DeepDungeonData,
    string? ProtocolVersion,
    bool? HasGancanaghModule,
    string ReasonCode)
{
    public static BossModIntegrationDiagnostics NotChecked { get; } = new(
        BossModConnectionState.NotChecked,
        BossModDeepDungeonDataCapability.Unavailable,
        null,
        null,
        "bossmod.ipc.not-checked");
}

public interface IBossModIpcInvoker
{
    bool HasModuleLookupFunction { get; }

    bool HasModuleByDataId(uint dataId);
}

public sealed class BossModIntegrationProbe(IBossModIpcInvoker invoker)
{
    public const uint GancanaghObjectId = 0x3D54;

    public BossModIntegrationDiagnostics Check()
    {
        try
        {
            if (!invoker.HasModuleLookupFunction)
            {
                return new(
                    BossModConnectionState.Unavailable,
                    BossModDeepDungeonDataCapability.Unavailable,
                    null,
                    null,
                    "bossmod.ipc.endpoint-unavailable");
            }

            var hasModule = invoker.HasModuleByDataId(GancanaghObjectId);
            return new(
                BossModConnectionState.ReachableUnversioned,
                BossModDeepDungeonDataCapability.Unavailable,
                null,
                hasModule,
                "bossmod.ipc.reachable-no-dd-contract");
        }
        catch (Exception exception)
        {
            return new(
                BossModConnectionState.IncompatibleOrFaulted,
                BossModDeepDungeonDataCapability.Unavailable,
                null,
                null,
                $"bossmod.ipc.invocation-failed:{exception.GetType().Name}");
        }
    }

    public static BossModIntegrationDiagnostics Check(Func<IBossModIpcInvoker> invokerFactory)
    {
        try
        {
            return new BossModIntegrationProbe(invokerFactory()).Check();
        }
        catch (Exception exception)
        {
            return Failed(exception);
        }
    }

    private static BossModIntegrationDiagnostics Failed(Exception exception) => new(
        BossModConnectionState.IncompatibleOrFaulted,
        BossModDeepDungeonDataCapability.Unavailable,
        null,
        null,
        $"bossmod.ipc.invocation-failed:{exception.GetType().Name}");
}
