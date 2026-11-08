using System.Collections.Immutable;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using DDCore.Observation;

namespace DDCore.Plugin.Observation;

internal sealed class DalamudChatFloorDiagnosticSource : IDisposable
{
    private readonly IChatGui chatGui;
    private readonly ChatFloorDiagnosticRecorder recorder;
    private readonly Func<LiveObservationDiagnostics> getContext;
    private long sequence;
    private bool disposed;

    public DalamudChatFloorDiagnosticSource(
        IChatGui chatGui,
        ChatFloorDiagnosticRecorder recorder,
        Func<LiveObservationDiagnostics> getContext)
    {
        this.chatGui = chatGui;
        this.recorder = recorder;
        this.getContext = getContext;
        chatGui.LogMessage += OnLogMessage;
        chatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        chatGui.ChatMessage -= OnChatMessage;
        chatGui.LogMessage -= OnLogMessage;
    }

    private void OnLogMessage(ILogMessage message)
    {
        if (recorder.Status.State != ChatFloorRecordingState.Recording
            || !ChatFloorEventAnalyzer.IsCandidateLogMessage(message.LogMessageId))
        {
            return;
        }

        var parameters = ImmutableArray.CreateBuilder<int>();
        var count = Math.Min(message.ParameterCount, ChatFloorEventAnalyzer.MaximumIntegerParameters);
        for (var index = 0; index < count; index++)
        {
            if (message.TryGetIntParameter(index, out var value))
            {
                parameters.Add(value);
            }
        }

        Record(new ChatFloorRawEvent(
            DateTimeOffset.UtcNow,
            Interlocked.Increment(ref sequence),
            ChatFloorEventSurface.LogMessage,
            message.LogMessageId,
            null,
            parameters.MoveToImmutable(),
            null));
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (recorder.Status.State != ChatFloorRecordingState.Recording
            || message.LogKind != XivChatType.SystemMessage)
        {
            return;
        }

        var floor = ChatFloorEventAnalyzer.ParseJapaneseFloor(message.OriginalMessage.ExtractText());
        if (floor is null)
        {
            return;
        }

        Record(new ChatFloorRawEvent(
            DateTimeOffset.UtcNow,
            Interlocked.Increment(ref sequence),
            ChatFloorEventSurface.ChatMessage,
            null,
            (ushort)message.LogKind,
            ImmutableArray<int>.Empty,
            floor));
    }

    private void Record(ChatFloorRawEvent observed)
    {
        var current = getContext();
        recorder.Record(observed, new ChatFloorContext(
            current.DungeonKind is not DDCore.Core.World.DeepDungeonKind.None and not DDCore.Core.World.DeepDungeonKind.Unsupported,
            current.DungeonKind,
            current.FloorRange));
    }
}
