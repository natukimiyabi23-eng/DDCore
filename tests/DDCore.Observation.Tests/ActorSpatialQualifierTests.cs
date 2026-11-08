using DDCore.Core.Geometry;
using DDCore.Core.World;
using DDCore.Observation;

namespace DDCore.Observation.Tests;

public sealed class ActorSpatialQualifierTests
{
    [Theory]
    [InlineData(6388u)]
    [InlineData(9999u)]
    public void ExactOriginIsPreservedAndQuarantinedForEveryBaseId(uint baseId)
    {
        var spatial = ActorSpatialQualifier.Qualify(Actor(baseId, new RawPosition(0, 5, 0), 0));

        Assert.Equal(new Coordinate2D(0, 0), spatial.Position);
        Assert.Equal(SpatialUsability.Quarantined, spatial.Usability);
        Assert.Equal(ActorSpatialQualifier.OriginReason, spatial.ReasonCode);
        Assert.Null(spatial.UsablePosition);
    }

    [Fact]
    public void MissingCoordinateIsDifferentFromOrigin()
    {
        var spatial = ActorSpatialQualifier.Qualify(Actor(1, null, null, "dalamud.actor.spatial-read-failed:test"));

        Assert.Null(spatial.Position);
        Assert.Equal(SpatialUsability.Missing, spatial.Usability);
        Assert.Equal("dalamud.actor.spatial-read-failed:test", spatial.ReasonCode);
    }

    [Fact]
    public void FiniteNonOriginLiveActorIsUsable()
    {
        var spatial = ActorSpatialQualifier.Qualify(Actor(6388, new RawPosition(12, 3, -4), 0.25));

        Assert.Equal(SpatialUsability.Usable, spatial.Usability);
        Assert.Equal(new Coordinate2D(12, -4), spatial.UsablePosition);
    }

    [Fact]
    public void InvalidAndStaleObservationsNeverBecomeUsable()
    {
        var invalid = ActorSpatialQualifier.Qualify(Actor(1, new RawPosition(double.NaN, 0, 1), 0));
        var stale = ActorSpatialQualifier.Qualify(Actor(1, new RawPosition(1, 0, 2), 0), stale: true);

        Assert.Equal(SpatialUsability.Missing, invalid.Usability);
        Assert.Equal(ObservationValueQuality.Invalid, invalid.Metadata.Quality);
        Assert.Equal(SpatialUsability.Quarantined, stale.Usability);
        Assert.Equal(ObservationValueQuality.Stale, stale.Metadata.Quality);
    }

    [Fact]
    public void DeadOrInvalidActorCoordinateIsQuarantined()
    {
        var dead = ActorSpatialQualifier.Qualify(Actor(1, new RawPosition(1, 0, 2), 0) with { IsDead = true });
        var invalid = ActorSpatialQualifier.Qualify(Actor(2, new RawPosition(1, 0, 2), 0) with { IsValid = false });

        Assert.Equal(SpatialUsability.Quarantined, dead.Usability);
        Assert.Equal(ActorSpatialQualifier.LifecycleReason, dead.ReasonCode);
        Assert.Equal(SpatialUsability.Quarantined, invalid.Usability);
    }

    private static RawActorObservation Actor(
        uint baseId,
        RawPosition? position,
        double? rotation,
        string? missingReason = null) =>
        new(
            1,
            1,
            1,
            baseId,
            RawActorKind.Enemy,
            position,
            rotation,
            1,
            100,
            100,
            false,
            true,
            false,
            null,
            null,
            [],
            missingReason);
}
