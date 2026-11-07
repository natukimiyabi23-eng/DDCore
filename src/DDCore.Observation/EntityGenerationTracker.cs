namespace DDCore.Observation;

internal sealed class EntityGenerationTracker
{
    private readonly Dictionary<uint, Entry> entries = [];
    private HashSet<uint> active = [];

    public int Observe(RawActorObservation actor)
    {
        var fingerprint = new Fingerprint(actor.GameObjectId, actor.ObjectIndex, actor.BaseId);
        if (!entries.TryGetValue(actor.EntityId, out var entry))
        {
            entry = new Entry(fingerprint, 0);
        }
        else if (!active.Contains(actor.EntityId) || entry.Fingerprint != fingerprint)
        {
            entry = new Entry(fingerprint, checked(entry.Generation + 1));
        }

        entries[actor.EntityId] = entry;
        return entry.Generation;
    }

    public int? Current(uint entityId) => entries.TryGetValue(entityId, out var entry) && active.Contains(entityId)
        ? entry.Generation
        : null;

    public void FinishFrame(IEnumerable<uint> entityIds) => active = entityIds.ToHashSet();

    public void Reset()
    {
        entries.Clear();
        active.Clear();
    }

    private readonly record struct Fingerprint(ulong GameObjectId, ushort ObjectIndex, uint BaseId);

    private readonly record struct Entry(Fingerprint Fingerprint, int Generation);
}
