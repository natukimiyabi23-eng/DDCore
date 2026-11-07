using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using DDCore.Observation;

namespace DDCore.Plugin.Observation;

internal sealed class DalamudBossModIpcInvoker : IBossModIpcInvoker
{
    internal const string ModuleLookupEndpoint = "BossMod.HasModuleByDataId";

    private readonly ICallGateSubscriber<uint, bool> moduleLookup;

    public DalamudBossModIpcInvoker(IDalamudPluginInterface pluginInterface)
    {
        moduleLookup = pluginInterface.GetIpcSubscriber<uint, bool>(ModuleLookupEndpoint);
    }

    public bool HasModuleLookupFunction => moduleLookup.HasFunction;

    public bool HasModuleByDataId(uint dataId) => moduleLookup.InvokeFunc(dataId);
}
