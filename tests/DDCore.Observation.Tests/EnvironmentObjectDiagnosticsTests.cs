using System.Text;
using System.Text.Json;
using DDCore.Core.World;

namespace DDCore.Observation.Tests;

public sealed class EnvironmentObjectDiagnosticsTests
{
    private static readonly DateTimeOffset Started = new(2026, 11, 11, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(EnvironmentObjectKind.EventObject)]
    [InlineData(EnvironmentObjectKind.Treasure)]
    [InlineData(EnvironmentObjectKind.Aetheryte)]
    public void PolicyAcceptsOnlyConfiguredKinds(EnvironmentObjectKind kind) =>
        Assert.True(EnvironmentObjectDiagnosticPolicy.Accepts(kind));

    [Fact]
    public void PolicyRejectsUndefinedKind() =>
        Assert.False(EnvironmentObjectDiagnosticPolicy.Accepts((EnvironmentObjectKind)999));

    [Fact]
    public void SpatialQualifierKeepsValidNonOriginCoordinateUsable()
    {
        var result = EnvironmentObjectSpatialQualifier.Qualify(Object(1, position: new RawPosition(10, 2, -4)));
        Assert.Equal(SpatialUsability.Usable, result.Usability);
        Assert.Equal(10, result.Position!.Value.X);
        Assert.Equal(-4, result.Position.Value.Y);
    }

    [Fact]
    public void SpatialQualifierQuarantinesOriginAndUnreliableLifecycle()
    {
        var origin = EnvironmentObjectSpatialQualifier.Qualify(Object(1, position: new RawPosition(0, 3, 0)));
        var invalid = EnvironmentObjectSpatialQualifier.Qualify(Object(2, isValid: false));
        Assert.Equal(SpatialUsability.Quarantined, origin.Usability);
        Assert.Equal(EnvironmentObjectSpatialQualifier.OriginReason, origin.ReasonCode);
        Assert.Equal(SpatialUsability.Quarantined, invalid.Usability);
        Assert.Equal(EnvironmentObjectSpatialQualifier.LifecycleReason, invalid.ReasonCode);
    }

    [Fact]
    public void SpatialQualifierSeparatesMissingAndInvalidCoordinates()
    {
        var missing = EnvironmentObjectSpatialQualifier.Qualify(
            new RawEnvironmentObjectObservation(1, 1, EnvironmentObjectKind.EventObject, 1234, null, null, true, true));
        var invalid = EnvironmentObjectSpatialQualifier.Qualify(Object(2, position: new RawPosition(double.NaN, 0, 1)));
        Assert.Equal(SpatialUsability.Missing, missing.Usability);
        Assert.Equal(EnvironmentObjectSpatialQualifier.MissingReason, missing.ReasonCode);
        Assert.Equal(SpatialUsability.Missing, invalid.Usability);
        Assert.Equal(EnvironmentObjectSpatialQualifier.InvalidReason, invalid.ReasonCode);
    }

    [Fact]
    public void RecorderIsInitiallyOffAndRequiresExplicitStart()
    {
        var recorder = CreateRecorder(out _);
        Assert.Equal(EnvironmentObjectRecordingState.Off, recorder.Status.State);
        Assert.False(recorder.Record(Started, Context(), [Object(1)]));
        Assert.True(recorder.Start());
        Assert.False(recorder.Start());
    }

    [Fact]
    public void RecorderAnonymizesRuntimeIdentifierAndUsesIndependentSchema()
    {
        var recorder = CreateRecorder(out var storage);
        recorder.Start();
        recorder.Record(Started, Context(), [Object(0xFEEDFACE)]);
        recorder.Stop();

        var json = Encoding.UTF8.GetString(storage.LastWrite!);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Contains("obj-000001", json, StringComparison.Ordinal);
        Assert.DoesNotContain("4277009102", json, StringComparison.Ordinal);
        Assert.DoesNotContain("entityId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gameObjectId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chat", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DisappearanceAndRuntimeIdReuseReceiveNewAnonymousIdentity()
    {
        var recorder = CreateRecorder(out var storage, interval: TimeSpan.Zero);
        recorder.Start();
        recorder.Record(Started, Context(), [Object(8)]);
        recorder.Record(Started.AddSeconds(1), Context(), []);
        recorder.Record(Started.AddSeconds(2), Context(), [Object(8)]);
        recorder.Stop();

        using var json = JsonDocument.Parse(storage.LastWrite!);
        var samples = json.RootElement.GetProperty("samples");
        Assert.Equal("obj-000001", samples[0].GetProperty("objects")[0].GetProperty("anonymousObjectId").GetString());
        Assert.Equal(0, samples[1].GetProperty("objects").GetArrayLength());
        Assert.Equal("obj-000002", samples[2].GetProperty("objects")[0].GetProperty("anonymousObjectId").GetString());
    }

    [Fact]
    public void SessionChangeResetsAnonymousIdentity()
    {
        var recorder = CreateRecorder(out var storage, interval: TimeSpan.Zero);
        recorder.Start();
        recorder.Record(Started, Context("session-a"), [Object(8)]);
        recorder.Record(Started.AddSeconds(1), Context("session-b"), [Object(8)]);
        recorder.Stop();
        var json = Encoding.UTF8.GetString(storage.LastWrite!);
        Assert.Contains("obj-000001", json, StringComparison.Ordinal);
        Assert.Contains("obj-000002", json, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownExactFloorDoesNotClaimFloorContinuity()
    {
        var recorder = CreateRecorder(out var storage);
        recorder.Start();
        recorder.Record(Started, Context(exactFloor: null), [Object(1)]);
        recorder.Stop();
        using var json = JsonDocument.Parse(storage.LastWrite!);
        var sample = json.RootElement.GetProperty("samples")[0];
        Assert.Equal(JsonValueKind.Null, sample.GetProperty("floorEpochId").ValueKind);
        Assert.Equal("environment-object.floor-continuity-unverified", sample.GetProperty("continuityReason").GetString());
    }

    [Fact]
    public void RecorderStopsAtSampleLimitWithoutAddingOverflowSample()
    {
        var recorder = CreateRecorder(out _, maximumSamples: 1, interval: TimeSpan.Zero);
        recorder.Start();
        Assert.True(recorder.Record(Started, Context(), [Object(1)]));
        Assert.False(recorder.Record(Started.AddSeconds(1), Context(), [Object(2)]));
        Assert.Equal(EnvironmentObjectRecordingState.LimitReached, recorder.Status.State);
        Assert.Equal(1, recorder.Status.Preview.SampleCount);
    }

    [Fact]
    public void StorageFailureFaultsOnlyRecorder()
    {
        var recorder = new EnvironmentObjectDiagnosticRecorder(
            "C:/unused", "test", new EnvironmentObjectRecordingLimits(1024 * 1024, 10, TimeSpan.FromMinutes(1), TimeSpan.Zero),
            new MemoryStorage { ThrowOnWrite = true }, () => Started);
        recorder.Start();
        recorder.Record(Started, Context(), [Object(1)]);
        Assert.False(recorder.Stop());
        Assert.Equal(EnvironmentObjectRecordingState.Faulted, recorder.Status.State);
        Assert.Contains("environment-object.save-failed", recorder.Status.LatestError, StringComparison.Ordinal);
    }

    [Fact]
    public void DisposeStopsActiveRecorder()
    {
        var recorder = CreateRecorder(out _);
        recorder.Start();
        recorder.Record(Started, Context(), [Object(1)]);
        recorder.Dispose();
        Assert.Equal(EnvironmentObjectRecordingState.Stopped, recorder.Status.State);
        Assert.False(recorder.Start());
    }

    private static EnvironmentObjectDiagnosticRecorder CreateRecorder(
        out MemoryStorage storage,
        int maximumSamples = 100,
        TimeSpan? interval = null)
    {
        storage = new MemoryStorage();
        return new EnvironmentObjectDiagnosticRecorder(
            "C:/diagnostics", "0.5.6.0",
            new EnvironmentObjectRecordingLimits(1024 * 1024, maximumSamples, TimeSpan.FromMinutes(15), interval ?? TimeSpan.FromMilliseconds(1)),
            storage, () => Started);
    }

    private static EnvironmentObjectContext Context(string session = "session-a", int? exactFloor = null) =>
        new(true, DeepDungeonKind.EurekaOrthos, new DeepDungeonFloorRange(1, 10), exactFloor, session);

    private static RawEnvironmentObjectObservation Object(
        ulong id,
        RawPosition? position = null,
        bool isValid = true) =>
        new(id, (ushort)(id % ushort.MaxValue), EnvironmentObjectKind.EventObject, 1234, position ?? new RawPosition(10, 1, 20), 0.5, true, isValid);

    private sealed class MemoryStorage : IRecordingStorage
    {
        private readonly Dictionary<string, byte[]> files = [];
        public bool ThrowOnWrite { get; init; }
        public byte[]? LastWrite { get; private set; }
        public void EnsureDirectory(string directory) { }
        public void WriteAtomically(string path, byte[] contents)
        {
            if (ThrowOnWrite) throw new IOException("simulated");
            LastWrite = contents;
            files[path] = contents;
        }

        public byte[] ReadAllBytes(string path) => files[path];
        public bool FileExists(string path) => files.ContainsKey(path);
        public void DeleteFile(string path) => files.Remove(path);
    }
}
