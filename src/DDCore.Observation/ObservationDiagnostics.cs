using System.Collections.Immutable;

namespace DDCore.Observation;

public enum DiagnosticSeverity
{
    Information,
    Warning,
    Error,
}

public sealed record ObservationDiagnosticEntry(
    DateTimeOffset OccurredAt,
    DiagnosticSeverity Severity,
    string Code,
    string Message,
    Guid? SessionId,
    long? SnapshotSequence);

public sealed class ObservationDiagnosticLog
{
    public const int DefaultCapacity = 256;

    private readonly object gate = new();
    private readonly Queue<ObservationDiagnosticEntry> entries = [];
    private readonly int capacity;

    public ObservationDiagnosticLog(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        this.capacity = capacity;
    }

    public ObservationDiagnosticEntry? LatestError
    {
        get
        {
            lock (gate)
            {
                return entries.LastOrDefault(entry => entry.Severity == DiagnosticSeverity.Error);
            }
        }
    }

    public void Append(ObservationDiagnosticEntry entry)
    {
        if (entry.OccurredAt == default
            || string.IsNullOrWhiteSpace(entry.Code)
            || string.IsNullOrWhiteSpace(entry.Message)
            || !Enum.IsDefined(entry.Severity))
        {
            throw new ArgumentException("Diagnostic entry is incomplete.", nameof(entry));
        }

        lock (gate)
        {
            entries.Enqueue(entry);
            while (entries.Count > capacity)
            {
                entries.Dequeue();
            }
        }
    }

    public ImmutableArray<ObservationDiagnosticEntry> Snapshot()
    {
        lock (gate)
        {
            return entries.ToImmutableArray();
        }
    }
}
