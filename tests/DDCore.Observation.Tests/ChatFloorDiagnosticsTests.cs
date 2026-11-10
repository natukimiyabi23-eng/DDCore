using System.Collections.Immutable;
using System.Text.Json;
using DDCore.Core.World;
using DDCore.Observation;

namespace DDCore.Observation.Tests;

public sealed class ChatFloorDiagnosticsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 9, 0, 0, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"ddcore-chat-floor-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("地下9階", 9)]
    [InlineData("地下200階", 200)]
    [InlineData("転移が実行された！", null)]
    [InlineData("地下9階です", null)]
    [InlineData("地下0階", null)]
    [InlineData("地下201階", null)]
    public void JapaneseParserAcceptsOnlyExactSupportedShape(string text, int? expected) =>
        Assert.Equal(expected, ChatFloorEventAnalyzer.ParseJapaneseFloor(text));

    [Fact]
    public void MatchingB1RangeIsStillOnlyConsistentCandidate()
    {
        var result = ChatFloorEventAnalyzer.Analyze(LogEvent(7270, 9), Context(1, 10));

        Assert.Equal(9, result.CandidateFloor);
        Assert.Equal(ChatFloorCandidateQuality.ConsistentCandidate, result.Quality);
        Assert.Equal("chat-floor.candidate-range-consistent-unverified", result.ReasonCode);
    }

    [Fact]
    public void CandidateOutsideB1RangeIsConflict()
    {
        var result = ChatFloorEventAnalyzer.Analyze(LogEvent(7270, 25), Context(1, 10));

        Assert.Equal(ChatFloorCandidateQuality.Conflict, result.Quality);
        Assert.Equal("chat-floor.candidate-range-conflict", result.ReasonCode);
    }

    [Theory]
    [InlineData(1, 1, 10, ChatFloorCandidateQuality.ConsistentCandidate)]
    [InlineData(10, 1, 10, ChatFloorCandidateQuality.ConsistentCandidate)]
    [InlineData(11, 1, 10, ChatFloorCandidateQuality.Conflict)]
    [InlineData(11, 11, 20, ChatFloorCandidateQuality.ConsistentCandidate)]
    public void FloorBandBoundariesAreReconciledWithoutVerification(
        int floor,
        int start,
        int end,
        ChatFloorCandidateQuality expected)
    {
        var result = ChatFloorEventAnalyzer.Analyze(LogEvent(7270, floor), Context(start, end));

        Assert.Equal(expected, result.Quality);
        Assert.NotEqual(ChatFloorCandidateQuality.Unverified, result.Quality);
    }

    [Fact]
    public void AmbiguousParametersDoNotProduceFloor()
    {
        var result = ChatFloorEventAnalyzer.Analyze(LogEvent(7270, 9, 10), Context(1, 10));

        Assert.Null(result.CandidateFloor);
        Assert.Equal(ChatFloorCandidateQuality.Missing, result.Quality);
        Assert.Equal("chat-floor.log-parameter-ambiguous", result.ReasonCode);
    }

    [Fact]
    public void MissingParametersDoNotProduceFloor()
    {
        var result = ChatFloorEventAnalyzer.Analyze(LogEvent(7270), Context(1, 10));

        Assert.Null(result.CandidateFloor);
        Assert.Equal(ChatFloorCandidateQuality.Missing, result.Quality);
        Assert.Equal("chat-floor.log-parameter-missing", result.ReasonCode);
    }

    [Fact]
    public void DuplicateEqualParametersRemainOneUnverifiedCandidate()
    {
        var result = ChatFloorEventAnalyzer.Analyze(LogEvent(7270, 9, 9), Context(1, 10));

        Assert.Equal(9, result.CandidateFloor);
        Assert.Equal(ChatFloorCandidateQuality.ConsistentCandidate, result.Quality);
    }

    [Fact]
    public void TransferMarkerNeverProducesFloor()
    {
        var result = ChatFloorEventAnalyzer.Analyze(LogEvent(7248), Context(1, 10));

        Assert.Null(result.CandidateFloor);
        Assert.Equal("chat-floor.transition-marker-unverified", result.ReasonCode);
    }

    [Fact]
    public void ChatCandidateOutsideDeepDungeonIsConflict()
    {
        var observed = new ChatFloorRawEvent(At, 1, ChatFloorEventSurface.ChatMessage, null, 57, null, [], 9);
        var result = ChatFloorEventAnalyzer.Analyze(observed, new ChatFloorContext(false, DeepDungeonKind.None, null));

        Assert.Equal(ChatFloorCandidateQuality.Conflict, result.Quality);
        Assert.Equal("chat-floor.candidate-outside-deep-dungeon", result.ReasonCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void InvalidParsedChatFloorIsRejected(int floor)
    {
        var observed = new ChatFloorRawEvent(At, 1, ChatFloorEventSurface.ChatMessage, null, 57, null, [], floor);
        var result = ChatFloorEventAnalyzer.Analyze(observed, Context(1, 10));

        Assert.Null(result.CandidateFloor);
        Assert.Equal(ChatFloorCandidateQuality.Missing, result.Quality);
        Assert.Equal("chat-floor.chat-floor-invalid", result.ReasonCode);
    }

    [Fact]
    public void NonSystemChatKindCannotProduceCandidate()
    {
        var observed = new ChatFloorRawEvent(At, 1, ChatFloorEventSurface.ChatMessage, null, 10, null, [], 9);
        var result = ChatFloorEventAnalyzer.Analyze(observed, Context(1, 10));

        Assert.Null(result.CandidateFloor);
        Assert.Equal(ChatFloorCandidateQuality.Missing, result.Quality);
        Assert.Equal("chat-floor.chat-kind-unsupported", result.ReasonCode);
    }

    [Fact]
    public void SameCandidateRetainsTheObservedDungeonContext()
    {
        var context = new ChatFloorContext(true, DeepDungeonKind.HeavenOnHigh, new DeepDungeonFloorRange(1, 10));
        var result = ChatFloorEventAnalyzer.Analyze(LogEvent(7270, 9), context);

        Assert.Equal(DeepDungeonKind.HeavenOnHigh, result.DungeonKind);
        Assert.Equal(ChatFloorCandidateQuality.ConsistentCandidate, result.Quality);
    }

    [Fact]
    public void IntegerParameterPositionIsRetainedAndOutOfBoundPositionIsIgnored()
    {
        var observed = new ChatFloorRawEvent(
            At,
            1,
            ChatFloorEventSurface.LogMessage,
            7270,
            null,
            9,
            [new ChatFloorIntegerParameter(4, 9), new ChatFloorIntegerParameter(8, 10)],
            null);

        var result = ChatFloorEventAnalyzer.Analyze(observed, Context(1, 10));

        var parameter = Assert.Single(result.IntegerParameters);
        Assert.Equal(4, parameter.Index);
        Assert.Equal(9, parameter.Value);
        Assert.Equal(9, result.CandidateFloor);
        Assert.Equal(9, result.ParameterCount);
    }

    [Fact]
    public void RecorderStartsOffAndRequiresExplicitStart()
    {
        using var recorder = CreateRecorder();

        Assert.Equal(ChatFloorRecordingState.Off, recorder.Status.State);
        Assert.False(recorder.Record(LogEvent(7270, 9), Context(1, 10)));
        Assert.True(recorder.Start());
        Assert.False(recorder.Start());
    }

    [Fact]
    public void RecorderStopsPersistsAndExportsOnlyAfterPreview()
    {
        using var recorder = CreateRecorder();
        recorder.Start();
        Assert.True(recorder.Record(LogEvent(7270, 9), Context(1, 10)));
        Assert.True(recorder.Stop());
        Assert.True(File.Exists(recorder.Status.RecordingPath));
        Assert.False(recorder.Export(false));
        Assert.True(recorder.Export(true));
        Assert.True(File.Exists(recorder.Status.ExportPath));
    }

    [Fact]
    public void SerializedDiagnosticsContainNoChatTextOrIdentityFields()
    {
        using var recorder = CreateRecorder();
        recorder.Start();
        recorder.Record(LogEvent(7270, 9), Context(1, 10));
        recorder.Stop();

        var json = File.ReadAllText(recorder.Status.RecordingPath!);
        Assert.DoesNotContain("地下9階", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sender", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("player", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("messageText", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("logMessageId", json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(2, document.RootElement.GetProperty("schemaVersion").GetInt32());
        var entry = document.RootElement.GetProperty("entries")[0];
        Assert.Equal(1, entry.GetProperty("parameterCount").GetInt32());
        Assert.Equal(0, entry.GetProperty("integerParameters")[0].GetProperty("index").GetInt32());
        Assert.Equal(9, entry.GetProperty("integerParameters")[0].GetProperty("value").GetInt32());
    }

    [Fact]
    public void EntryLimitStopsSafely()
    {
        using var recorder = CreateRecorder(new ChatFloorRecordingLimits(256 * 1024, 1, TimeSpan.FromMinutes(15)));
        recorder.Start();
        recorder.Record(LogEvent(7270, 9), Context(1, 10));
        Assert.False(recorder.Record(LogEvent(7270, 10), Context(1, 10)));

        Assert.Equal(ChatFloorRecordingState.LimitReached, recorder.Status.State);
        Assert.Equal(1, recorder.Status.EntryCount);
    }

    [Fact]
    public void ByteLimitStopsBeforeOversizedEntryIsRetained()
    {
        using var recorder = CreateRecorder(new ChatFloorRecordingLimits(128, 256, TimeSpan.FromMinutes(15)));
        recorder.Start();

        Assert.False(recorder.Record(LogEvent(7270, 9), Context(1, 10)));
        Assert.Equal(ChatFloorRecordingState.LimitReached, recorder.Status.State);
        Assert.Equal(0, recorder.Status.EntryCount);
        Assert.Equal("chat-floor.byte-limit", recorder.Status.StopReason);
    }

    [Fact]
    public void DurationLimitStopsSafely()
    {
        using var recorder = CreateRecorder(new ChatFloorRecordingLimits(256 * 1024, 256, TimeSpan.FromSeconds(1)));
        recorder.Start();
        var late = LogEvent(7270, 9) with { ObservedAtUtc = At.AddSeconds(2) };

        Assert.False(recorder.Record(late, Context(1, 10)));
        Assert.Equal(ChatFloorRecordingState.LimitReached, recorder.Status.State);
    }

    [Fact]
    public void SaveFailureFaultsRecorderWithoutThrowing()
    {
        using var recorder = new ChatFloorDiagnosticRecorder(directory, "0.5.4.0", storage: new ThrowingStorage(), utcNow: () => At);
        recorder.Start();
        recorder.Record(LogEvent(7270, 9), Context(1, 10));

        Assert.False(recorder.Stop());
        Assert.Equal(ChatFloorRecordingState.Faulted, recorder.Status.State);
        Assert.Contains("chat-floor.save-failed", recorder.Status.LatestError);
    }

    [Fact]
    public void DuplicateAndReorderedEventsArePreservedAsDiagnosticEvidence()
    {
        using var recorder = CreateRecorder();
        recorder.Start();
        Assert.True(recorder.Record(LogEvent(7270, 9) with { Sequence = 2, ObservedAtUtc = At.AddSeconds(1) }, Context(1, 10)));
        Assert.True(recorder.Record(LogEvent(7270, 9) with { Sequence = 1, ObservedAtUtc = At }, Context(1, 10)));

        Assert.Equal(2, recorder.Status.EntryCount);
        Assert.Equal(1, recorder.Status.Latest!.Sequence);
        Assert.Equal(ChatFloorCandidateQuality.ConsistentCandidate, recorder.Status.Latest.Quality);
    }

    [Fact]
    public void StopRejectsFurtherEventsAndRestartCreatesFreshSession()
    {
        using var recorder = CreateRecorder();
        recorder.Start();
        recorder.Record(LogEvent(7270, 9), Context(1, 10));
        Assert.True(recorder.Stop());
        Assert.False(recorder.Record(LogEvent(7270, 10), Context(1, 10)));

        Assert.True(recorder.Start());
        Assert.Equal(ChatFloorRecordingState.Recording, recorder.Status.State);
        Assert.Equal(0, recorder.Status.EntryCount);
        Assert.Null(recorder.Status.Latest);
    }

    [Fact]
    public void SourceFailureStopsWithoutPersistingExceptionMessage()
    {
        using var recorder = CreateRecorder();
        recorder.Start();

        recorder.ReportSourceFailure("chat-floor.log-callback-failed", new InvalidOperationException("private chat text"));

        Assert.Equal(ChatFloorRecordingState.Faulted, recorder.Status.State);
        Assert.Equal("chat-floor.log-callback-failed", recorder.Status.StopReason);
        Assert.Equal("chat-floor.log-callback-failed:InvalidOperationException", recorder.Status.LatestError);
        Assert.DoesNotContain("private chat text", recorder.Status.LatestError, StringComparison.Ordinal);
    }

    [Fact]
    public void DisposeStopsAndPersistsActiveDiagnostic()
    {
        var recorder = CreateRecorder();
        recorder.Start();
        recorder.Record(LogEvent(7270, 9), Context(1, 10));

        recorder.Dispose();

        Assert.Equal(ChatFloorRecordingState.Stopped, recorder.Status.State);
        Assert.Equal("chat-floor.plugin-disposed", recorder.Status.StopReason);
        Assert.True(File.Exists(recorder.Status.RecordingPath));
    }

    [Fact]
    public void DeleteRequiresConfirmationAndRemovesSessionFiles()
    {
        using var recorder = CreateRecorder();
        recorder.Start();
        recorder.Record(LogEvent(7270, 9), Context(1, 10));
        recorder.Stop();
        var path = recorder.Status.RecordingPath!;

        Assert.False(recorder.Delete(false));
        Assert.True(recorder.Delete(true));
        Assert.False(File.Exists(path));
        Assert.Equal(ChatFloorRecordingState.Off, recorder.Status.State);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private ChatFloorDiagnosticRecorder CreateRecorder(ChatFloorRecordingLimits? limits = null) =>
        new(directory, "0.5.4.0", limits, utcNow: () => At);

    private static ChatFloorContext Context(int start, int end) =>
        new(true, DeepDungeonKind.EurekaOrthos, new DeepDungeonFloorRange(start, end));

    private static ChatFloorRawEvent LogEvent(uint id, params int[] parameters) =>
        new(
            At,
            1,
            ChatFloorEventSurface.LogMessage,
            id,
            null,
            parameters.Length,
            parameters.Select(static (value, index) => new ChatFloorIntegerParameter(index, value)).ToImmutableArray(),
            null);

    private sealed class ThrowingStorage : IRecordingStorage
    {
        public void EnsureDirectory(string directoryPath) { }
        public void WriteAtomically(string path, byte[] contents) => throw new IOException("test");
        public byte[] ReadAllBytes(string path) => throw new IOException("test");
        public bool FileExists(string path) => false;
        public void DeleteFile(string path) { }
    }
}
