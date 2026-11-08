using System.Collections.Immutable;
using System.Text.Json.Serialization;
using DDCore.Core.Geometry;
using DDCore.Core.Model;
using DDCore.Core.Schema;

namespace DDCore.Core.World;

public enum ObservationSource
{
    LiveAdapter,
    Replay,
    Mock,
    Simulation,
}

public enum ObservationValueSource
{
    Unknown,
    PublicDalamud,
    Replay,
    Mock,
    Derived,
}

public enum ObservationValueQuality
{
    Direct,
    Derived,
    Missing,
    Unsupported,
    Stale,
    Invalid,
}

public sealed record ObservationMetadata(
    ObservationValueSource Source,
    ObservationValueQuality Quality,
    string? MissingReason)
{
    public void Validate()
    {
        var requiresMissingReason = Quality is not (ObservationValueQuality.Direct or ObservationValueQuality.Derived);
        if (!Enum.IsDefined(Source)
            || !Enum.IsDefined(Quality)
            || (Source == ObservationValueSource.Unknown
                && Quality is (ObservationValueQuality.Direct or ObservationValueQuality.Derived))
            || (requiresMissingReason && string.IsNullOrWhiteSpace(MissingReason))
            || (!requiresMissingReason && !string.IsNullOrWhiteSpace(MissingReason)))
        {
            throw new InvalidDataException("Observation metadata has an invalid source, quality, or missing reason.");
        }
    }
}

public sealed record AnonymousPlayerPose(
    Coordinate2D? Position,
    double? Rotation,
    ObservationMetadata Metadata)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Metadata);
        Metadata.Validate();
        if (Position is { } position)
        {
            Coordinate2D.EnsureFinite(position, nameof(Position));
        }

        if (Rotation is { } rotation)
        {
            Coordinate2D.EnsureFinite(rotation, nameof(Rotation));
        }

        var hasCompletePose = Position.HasValue && Rotation.HasValue;
        if (Metadata.Quality is (ObservationValueQuality.Direct or ObservationValueQuality.Derived) && !hasCompletePose
            || (Metadata.Quality is ObservationValueQuality.Missing or ObservationValueQuality.Unsupported or ObservationValueQuality.Invalid
                && (Position.HasValue || Rotation.HasValue))
            || (Metadata.Quality == ObservationValueQuality.Stale && Position.HasValue != Rotation.HasValue))
        {
            throw new InvalidDataException("Anonymous player pose does not match its observation quality.");
        }
    }
}

public enum DeepDungeonKind
{
    None,
    PalaceOfTheDead,
    HeavenOnHigh,
    EurekaOrthos,
    Unsupported,
}

public enum ActorKind
{
    Unknown,
    Player,
    PartyMember,
    Enemy,
    EventObject,
}

public enum CastLifecycle
{
    Casting,
    Interrupted,
    Completed,
}

public readonly record struct ActorIdentity(ulong EntityId, int Generation)
{
    public void Validate()
    {
        if (EntityId == 0 || Generation < 0)
        {
            throw new InvalidDataException("Actor identity requires a non-zero entity ID and non-negative generation.");
        }
    }
}

public sealed record CastSnapshot(
    uint ActionId,
    CastLifecycle Lifecycle,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishesAt,
    double Rotation,
    ActorIdentity? Target,
    Coordinate2D? TargetLocation)
{
    public void Validate(DateTimeOffset capturedAt)
    {
        if (ActionId == 0 || !Enum.IsDefined(Lifecycle) || StartedAt == default || FinishesAt < StartedAt)
        {
            throw new InvalidDataException("Cast has an invalid action, lifecycle, or time range.");
        }

        Coordinate2D.EnsureFinite(Rotation, nameof(Rotation));

        if (StartedAt > capturedAt + TimeSpan.FromMinutes(10))
        {
            throw new InvalidDataException("Cast start is unreasonably later than the snapshot.");
        }

        Target?.Validate();
        if (TargetLocation is { } location)
        {
            Coordinate2D.EnsureFinite(location, nameof(TargetLocation));
        }
    }
}

public sealed record StatusSnapshot(
    uint StatusId,
    ActorIdentity? Source,
    byte? Stacks,
    DateTimeOffset? ExpiresAt)
{
    public void Validate()
    {
        if (StatusId == 0)
        {
            throw new InvalidDataException("Status ID cannot be zero.");
        }

        Source?.Validate();
    }
}

public sealed record ActorSnapshot(
    ActorIdentity Identity,
    uint ObjectId,
    ActorKind Kind,
    Coordinate2D Position,
    double Rotation,
    double HitboxRadius,
    uint CurrentHp,
    uint MaximumHp,
    bool IsDead,
    bool IsDestroyed,
    bool IsInCombat,
    ActorIdentity? Target,
    CastSnapshot? Cast,
    ImmutableArray<StatusSnapshot> Statuses)
{
    /// <summary>
    /// Actor and cast rotations use mathematical radians: zero points along positive X and positive values rotate
    /// counter-clockwise toward positive Y. A game adapter must convert FFXIV/BossMod clockwise-from-positive-Z
    /// rotations before constructing a snapshot.
    /// </summary>
    public void Validate(DateTimeOffset capturedAt)
    {
        Identity.Validate();
        Coordinate2D.EnsureFinite(Position, nameof(Position));
        Coordinate2D.EnsureFinite(Rotation, nameof(Rotation));
        Coordinate2D.EnsureFinite(HitboxRadius, nameof(HitboxRadius));
        if (!Enum.IsDefined(Kind) || HitboxRadius < 0 || MaximumHp < CurrentHp || Statuses.IsDefault)
        {
            throw new InvalidDataException("Actor contains an invalid kind, hitbox, HP range, or status collection.");
        }

        Target?.Validate();
        Cast?.Validate(capturedAt);
        foreach (var status in Statuses)
        {
            status.Validate();
        }
    }
}

public sealed record DeepDungeonContext(
    DeepDungeonKind Kind,
    int? Floor,
    uint? TerritoryId,
    uint? ContentId,
    string ContentKey,
    int? Tileset)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || string.IsNullOrWhiteSpace(ContentKey))
        {
            throw new InvalidDataException("Deep Dungeon context requires a known enum value and content key.");
        }

        if (Floor is < 1 or > 200 || Tileset is < 0)
        {
            throw new InvalidDataException("Deep Dungeon floor or tileset is outside the accepted range.");
        }

        if (Kind == DeepDungeonKind.None && Floor is not null)
        {
            throw new InvalidDataException("A non-Deep-Dungeon context cannot contain a floor.");
        }
    }
}

public sealed record WorldSnapshot
{
    [JsonConstructor]
    public WorldSnapshot(
        int schemaVersion,
        long sequence,
        DateTimeOffset capturedAt,
        Guid sessionId,
        ObservationSource source,
        ObservationQuality quality,
        DeepDungeonContext dungeon,
        ImmutableArray<ActorSnapshot> actors,
        ImmutableArray<string> diagnostics,
        AnonymousPlayerPose? playerPose = null)
    {
        SchemaVersion = schemaVersion;
        Sequence = sequence;
        CapturedAt = capturedAt;
        SessionId = sessionId;
        Source = source;
        Quality = quality;
        Dungeon = dungeon;
        Actors = actors;
        Diagnostics = diagnostics;
        PlayerPose = playerPose;
        Validate();
    }

    public WorldSnapshot(long sequence, DateTimeOffset capturedAt, bool isDeepDungeon, string contentKey)
        : this(
            SchemaVersions.WorldSnapshot,
            sequence,
            capturedAt,
            Guid.NewGuid(),
            ObservationSource.LiveAdapter,
            ObservationQuality.Complete,
            new DeepDungeonContext(isDeepDungeon ? DeepDungeonKind.Unsupported : DeepDungeonKind.None, null, null, null, contentKey, null),
            ImmutableArray<ActorSnapshot>.Empty,
            ImmutableArray<string>.Empty)
    {
    }

    public int SchemaVersion { get; }

    public long Sequence { get; }

    public DateTimeOffset CapturedAt { get; }

    public Guid SessionId { get; }

    public ObservationSource Source { get; }

    public ObservationQuality Quality { get; }

    public DeepDungeonContext Dungeon { get; }

    public ImmutableArray<ActorSnapshot> Actors { get; }

    public ImmutableArray<string> Diagnostics { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AnonymousPlayerPose? PlayerPose { get; }

    public bool IsDeepDungeon => Dungeon.Kind != DeepDungeonKind.None;

    public string ContentKey => Dungeon.ContentKey;

    public void Validate()
    {
        if (SchemaVersion is not (SchemaVersions.LegacyWorldSnapshot or SchemaVersions.WorldSnapshot)
            || Sequence < 0
            || CapturedAt == default
            || SessionId == Guid.Empty
            || !Enum.IsDefined(Source)
            || !Enum.IsDefined(Quality)
            || Actors.IsDefault
            || Diagnostics.IsDefault)
        {
            throw new InvalidDataException("World snapshot header or immutable collections are invalid.");
        }

        Dungeon.Validate();
        if (SchemaVersion == SchemaVersions.LegacyWorldSnapshot && PlayerPose is not null)
        {
            throw new InvalidDataException("Legacy world snapshots cannot contain the Phase 5.5A player pose field.");
        }

        PlayerPose?.Validate();
        var identities = new HashSet<ActorIdentity>();
        foreach (var actor in Actors)
        {
            actor.Validate(CapturedAt);
            if (!identities.Add(actor.Identity))
            {
                throw new InvalidDataException($"Duplicate actor identity: {actor.Identity}.");
            }
        }

        if (Diagnostics.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("Diagnostics cannot contain empty values.");
        }
    }
}
