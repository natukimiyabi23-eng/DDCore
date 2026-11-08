using DDCore.Core.Geometry;
using DDCore.Core.World;

namespace DDCore.Observation;

public static class ActorSpatialQualifier
{
    public const string MissingReason = "actor.spatial.not-observed";
    public const string InvalidReason = "actor.spatial.invalid";
    public const string OriginReason = "actor.spatial.origin-unverified";
    public const string LifecycleReason = "actor.spatial.lifecycle-unreliable";
    public const string StaleReason = "actor.spatial.stale";

    public static ActorSpatialObservation Qualify(RawActorObservation actor, bool stale = false)
    {
        if (actor.Position is not { } rawPosition || actor.Rotation is not { } rawRotation)
        {
            var reason = string.IsNullOrWhiteSpace(actor.SpatialMissingReason)
                ? MissingReason
                : actor.SpatialMissingReason;
            return ActorSpatialObservation.Missing(
                new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Missing, reason),
                reason);
        }

        if (!double.IsFinite(rawPosition.X)
            || !double.IsFinite(rawPosition.Y)
            || !double.IsFinite(rawPosition.Z)
            || !double.IsFinite(rawRotation))
        {
            return ActorSpatialObservation.Missing(
                new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Invalid, InvalidReason),
                InvalidReason);
        }

        var position = new Coordinate2D(rawPosition.X, rawPosition.Z);
        var rotation = Coordinate2D.FromClockwisePositiveYRotation(rawRotation);
        if (stale)
        {
            return ActorSpatialObservation.Quarantined(
                position,
                rotation,
                new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Stale, StaleReason),
                StaleReason);
        }

        if (!actor.IsValid || actor.IsDead)
        {
            return ActorSpatialObservation.Quarantined(
                position,
                rotation,
                new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Direct, null),
                LifecycleReason);
        }

        if (position == new Coordinate2D(0, 0))
        {
            return ActorSpatialObservation.Quarantined(
                position,
                rotation,
                new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Direct, null),
                OriginReason);
        }

        return ActorSpatialObservation.Usable(position, rotation, ObservationValueSource.PublicDalamud);
    }
}
