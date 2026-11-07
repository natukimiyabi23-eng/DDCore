using System.Collections.Immutable;
using DDCore.Core.Geometry;
using DDCore.Core.Mechanics;
using DDCore.Core.Replay;
using DDCore.Core.World;
using DDCore.Observation;

namespace DDCore.Observation.Tests;

public sealed class ObservationRecordingTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Session = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"ddcore-tests-{Guid.NewGuid():N}");

    [Fact]
    public void RecordingStartsOffAndRequiresExplicitStart()
    {
        using var recorder = CreateRecorder();

        recorder.Record(Snapshot(0, At), Frame(Snapshot(0, At)));

        Assert.Equal(ObservationRecordingState.Off, recorder.Status.State);
        Assert.Equal(0, recorder.Status.Preview.SnapshotCount);
    }

    [Fact]
    public void StartRecordStopProducesReplayReadableByExistingCodec()
    {
        using var recorder = CreateRecorder();
        var snapshot = Snapshot(0, At);

        Assert.True(recorder.Start());
        recorder.Record(snapshot, Frame(snapshot));
        Assert.True(recorder.Stop());

        Assert.Equal(ObservationRecordingState.Stopped, recorder.Status.State);
        Assert.Equal(1, recorder.Status.Preview.SnapshotCount);
        var bytes = File.ReadAllBytes(recorder.Status.RecordingPath!);
        var restored = new ReplayCodec().Deserialize(bytes);
        Assert.Single(restored.Snapshots);
        Assert.Equal(snapshot.Sequence, restored.Snapshots[0].Sequence);
    }

    [Fact]
    public void SecondStartIsRejectedWhileRecording()
    {
        using var recorder = CreateRecorder();

        Assert.True(recorder.Start());
        Assert.False(recorder.Start());
        Assert.Equal(ObservationRecordingState.Recording, recorder.Status.State);
    }

    [Fact]
    public void SampleIntervalPreventsHighFrequencyCapture()
    {
        using var recorder = CreateRecorder(new ObservationRecordingLimits(
            ReplayCodec.MaximumDocumentBytes,
            100,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(10)));
        recorder.Start();

        var first = Snapshot(0, At);
        var skipped = Snapshot(1, At.AddMilliseconds(100));
        recorder.Record(first, Frame(first));
        recorder.Record(skipped, Frame(skipped));

        Assert.Equal(1, recorder.Status.Preview.SnapshotCount);
    }

    [Fact]
    public void SnapshotLimitStopsRecording()
    {
        using var recorder = CreateRecorder(new ObservationRecordingLimits(
            ReplayCodec.MaximumDocumentBytes,
            1,
            TimeSpan.FromMinutes(1),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(10)));
        recorder.Start();
        var first = Snapshot(0, At);
        var second = Snapshot(1, At.AddSeconds(1));

        recorder.Record(first, Frame(first));
        recorder.Record(second, Frame(second));

        Assert.Equal(ObservationRecordingState.LimitReached, recorder.Status.State);
        Assert.Equal("recording.snapshot-limit", recorder.Status.StopReason);
        Assert.Equal(1, recorder.Status.Preview.SnapshotCount);
    }

    [Fact]
    public void DurationLimitStopsRecording()
    {
        using var recorder = CreateRecorder(new ObservationRecordingLimits(
            ReplayCodec.MaximumDocumentBytes,
            100,
            TimeSpan.FromSeconds(1),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(10)));
        recorder.Start();
        var first = Snapshot(0, At);
        var late = Snapshot(1, At.AddSeconds(2));

        recorder.Record(first, Frame(first));
        recorder.Record(late, Frame(late));

        Assert.Equal(ObservationRecordingState.LimitReached, recorder.Status.State);
        Assert.Equal("recording.duration-limit", recorder.Status.StopReason);
    }

    [Fact]
    public void ByteLimitStopsWithoutWritingOversizedDocument()
    {
        using var recorder = CreateRecorder(new ObservationRecordingLimits(
            1,
            100,
            TimeSpan.FromMinutes(1),
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(1)));
        recorder.Start();
        var snapshot = Snapshot(0, At);

        recorder.Record(snapshot, Frame(snapshot));

        Assert.Equal(ObservationRecordingState.LimitReached, recorder.Status.State);
        Assert.Equal("recording.byte-limit", recorder.Status.StopReason);
        Assert.Equal(0, recorder.Status.Preview.SnapshotCount);
    }

    [Fact]
    public void StopPreservesByteLimitStateWhenFinalFlushExceedsLimit()
    {
        var first = Snapshot(0, At);
        var oneSnapshotDocument = ReplayDocument.Create(
            At,
            Session,
            "0.3.1.0",
            [first],
            ReplayUserIntent.ExplicitCapture);
        var oneSnapshotBytes = new ReplayCodec().Serialize(oneSnapshotDocument, ReplayUserIntent.ExplicitCaptureAndExport).Length;
        using var recorder = CreateRecorder(new ObservationRecordingLimits(
            oneSnapshotBytes + 32,
            100,
            TimeSpan.FromMinutes(1),
            TimeSpan.Zero,
            TimeSpan.FromMinutes(1)));
        recorder.Start();
        var second = Snapshot(1, At.AddSeconds(1));
        recorder.Record(first, Frame(first));
        recorder.Record(second, Frame(second));

        Assert.False(recorder.Stop());

        Assert.Equal(ObservationRecordingState.LimitReached, recorder.Status.State);
        Assert.Equal("recording.byte-limit", recorder.Status.StopReason);
        Assert.Equal(1, recorder.Status.Preview.SnapshotCount);
    }

    [Fact]
    public void StorageFailureStopsRecordingSafely()
    {
        using var recorder = new ObservationRecordingService(
            directory,
            "0.3.1.0",
            storage: new ThrowingStorage(),
            utcNow: () => At);

        Assert.False(recorder.Start());
        Assert.Equal(ObservationRecordingState.Faulted, recorder.Status.State);
        Assert.StartsWith("recording.start-failed", recorder.Status.LatestError);
    }

    [Fact]
    public void WriteFailureStopsRecordingSafely()
    {
        using var recorder = new ObservationRecordingService(
            directory,
            "0.3.1.0",
            storage: new WriteFailingStorage(),
            utcNow: () => At);
        Assert.True(recorder.Start());
        var snapshot = Snapshot(0, At);

        recorder.Record(snapshot, Frame(snapshot));

        Assert.Equal(ObservationRecordingState.Faulted, recorder.Status.State);
        Assert.StartsWith("recording.capture-failed", recorder.Status.LatestError);
    }

    [Fact]
    public void SessionChangeStopsBeforeMixingReplaySessions()
    {
        using var recorder = CreateRecorder();
        recorder.Start();
        var first = Snapshot(0, At);
        var differentSession = Snapshot(0, At.AddSeconds(1), Guid.NewGuid());

        recorder.Record(first, Frame(first));
        recorder.Record(differentSession, Frame(differentSession));

        Assert.Equal(ObservationRecordingState.Stopped, recorder.Status.State);
        Assert.Equal("recording.session-changed", recorder.Status.StopReason);
        Assert.Equal(1, recorder.Status.Preview.SnapshotCount);
    }

    [Fact]
    public void PrivacyFilterRemovesPlayerAndPartyIdentifiers()
    {
        using var recorder = CreateRecorder();
        recorder.Start();
        var snapshot = Snapshot(0, At, includePlayerAndParty: true);

        recorder.Record(snapshot, Frame(snapshot));
        recorder.Stop();

        var restored = new ReplayCodec().Deserialize(File.ReadAllBytes(recorder.Status.RecordingPath!));
        var actors = Assert.Single(restored.Snapshots).Actors;
        Assert.All(actors, actor => Assert.True(actor.Kind is ActorKind.Enemy or ActorKind.EventObject));
        Assert.DoesNotContain(actors, actor => actor.Identity.EntityId is 10 or 11);
        Assert.Null(Assert.Single(actors).Target);
    }

    [Fact]
    public void ExportRequiresPreviewConfirmationAndCreatesValidatedCopy()
    {
        using var recorder = CreateRecorder();
        recorder.Start();
        var snapshot = Snapshot(0, At);
        recorder.Record(snapshot, Frame(snapshot));
        recorder.Stop();

        Assert.False(recorder.Export(previewConfirmed: false));
        Assert.True(recorder.Export(previewConfirmed: true));
        Assert.True(File.Exists(recorder.Status.ExportPath));
        _ = new ReplayCodec().Deserialize(File.ReadAllBytes(recorder.Status.ExportPath!));
    }

    [Fact]
    public void DeleteRequiresConfirmationAndRemovesRecordingAndExport()
    {
        using var recorder = CreateRecorder();
        recorder.Start();
        var snapshot = Snapshot(0, At);
        recorder.Record(snapshot, Frame(snapshot));
        recorder.Stop();
        recorder.Export(previewConfirmed: true);
        var recordingPath = recorder.Status.RecordingPath!;
        var exportPath = recorder.Status.ExportPath!;

        Assert.False(recorder.Delete(deletionConfirmed: false));
        Assert.True(recorder.Delete(deletionConfirmed: true));

        Assert.False(File.Exists(recordingPath));
        Assert.False(File.Exists(exportPath));
        Assert.Equal(ObservationRecordingState.Off, recorder.Status.State);
    }

    [Fact]
    public void DisposeFlushesAndStopsActiveRecording()
    {
        var recorder = CreateRecorder();
        recorder.Start();
        var snapshot = Snapshot(0, At);
        recorder.Record(snapshot, Frame(snapshot));
        var path = recorder.Status.RecordingPath!;

        recorder.Dispose();

        Assert.Equal(ObservationRecordingState.Stopped, recorder.Status.State);
        Assert.Equal("recording.plugin-disposed", recorder.Status.StopReason);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void DiagnosticLogIsBoundedAndTracksLatestError()
    {
        var log = new ObservationDiagnosticLog(2);
        log.Append(new(At, DiagnosticSeverity.Information, "one", "one", Session, 0));
        log.Append(new(At.AddSeconds(1), DiagnosticSeverity.Error, "two", "two", Session, 1));
        log.Append(new(At.AddSeconds(2), DiagnosticSeverity.Information, "three", "three", Session, 2));

        Assert.Equal(["two", "three"], log.Snapshot().Select(entry => entry.Code));
        Assert.Equal("two", log.LatestError?.Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private ObservationRecordingService CreateRecorder(ObservationRecordingLimits? limits = null) =>
        new(directory, "0.3.1.0", limits, utcNow: () => At);

    private static WorldSnapshot Snapshot(
        long sequence,
        DateTimeOffset capturedAt,
        Guid? sessionId = null,
        bool includePlayerAndParty = false)
    {
        var builder = new MockWorldSnapshotBuilder()
            .InSession(sessionId ?? Session)
            .At(capturedAt)
            .WithSequence(sequence);
        if (includePlayerAndParty)
        {
            builder.AddActor(Actor(10, ActorKind.Player, target: new ActorIdentity(1, 0)));
            builder.AddActor(Actor(11, ActorKind.PartyMember));
        }

        builder.AddActor(Actor(1, ActorKind.Enemy, target: includePlayerAndParty ? new ActorIdentity(10, 0) : null));
        return builder.Build();
    }

    private static ActorSnapshot Actor(ulong entityId, ActorKind kind, ActorIdentity? target = null) =>
        new(
            new ActorIdentity(entityId, 0),
            kind == ActorKind.Enemy ? 0x3D54u : 0x1000u,
            kind,
            new Coordinate2D(-300, -300),
            0,
            1,
            100,
            100,
            false,
            false,
            true,
            target,
            null,
            ImmutableArray<StatusSnapshot>.Empty);

    private static MechanicFrame Frame(WorldSnapshot snapshot) =>
        MechanicFrame.Unknown(snapshot, "analysis.test");

    private sealed class ThrowingStorage : IRecordingStorage
    {
        public void EnsureDirectory(string directory) => throw new IOException("test");
        public void WriteAtomically(string path, byte[] contents) => throw new IOException("test");
        public byte[] ReadAllBytes(string path) => throw new IOException("test");
        public bool FileExists(string path) => false;
        public void DeleteFile(string path) => throw new IOException("test");
    }

    private sealed class WriteFailingStorage : IRecordingStorage
    {
        public void EnsureDirectory(string directory)
        {
        }

        public void WriteAtomically(string path, byte[] contents) => throw new IOException("test");
        public byte[] ReadAllBytes(string path) => throw new IOException("test");
        public bool FileExists(string path) => false;
        public void DeleteFile(string path) => throw new IOException("test");
    }
}
