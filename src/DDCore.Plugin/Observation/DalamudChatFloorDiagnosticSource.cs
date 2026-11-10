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
        var failureReason = "chat-floor.log-callback-status-failed";
        try
        {
            if (recorder.Status.State != ChatFloorRecordingState.Recording)
            {
                return;
            }

            failureReason = "chat-floor.log-callback-id-failed";
            var logMessageId = message.LogMessageId;
            if (!ChatFloorEventAnalyzer.IsCandidateLogMessage(logMessageId))
            {
                return;
            }

            failureReason = "chat-floor.log-callback-parameter-count-failed";
            var parameterCount = message.ParameterCount;
            failureReason = "chat-floor.log-callback-parameter-read-failed";
            var parameters = ChatFloorEventAnalyzer.CaptureIntegerParameters(
                parameterCount,
                index => message.TryGetIntParameter(index, out var value) ? value : null);
            var observed = new ChatFloorRawEvent(
                DateTimeOffset.UtcNow,
                Interlocked.Increment(ref sequence),
                ChatFloorEventSurface.LogMessage,
                logMessageId,
                null,
                Math.Max(parameterCount, 0),
                parameters,
                null);

            failureReason = "chat-floor.log-callback-context-failed";
            var context = GetContext();
            failureReason = "chat-floor.log-callback-record-failed";
            recorder.Record(observed, context);
        }
        catch (Exception exception)
        {
            recorder.ReportSourceFailure(failureReason, exception);
        }
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        try
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
                null,
                ImmutableArray<ChatFloorIntegerParameter>.Empty,
                floor));
        }
        catch (Exception exception)
        {
            recorder.ReportSourceFailure("chat-floor.chat-callback-failed", exception);
        }
    }

    private void Record(ChatFloorRawEvent observed)
    {
        recorder.Record(observed, GetContext());
    }

    private ChatFloorContext GetContext()
    {
        var current = getContext();
        return new ChatFloorContext(
            current.DungeonKind is not DDCore.Core.World.DeepDungeonKind.None and not DDCore.Core.World.DeepDungeonKind.Unsupported,
            current.DungeonKind,
            current.FloorRange);
    }
}
