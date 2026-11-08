using DDCore.Core.World;
using DDCore.Observation;

namespace DDCore.Observation.Tests;

public sealed class DeepDungeonContentRegistryTests
{
    public static TheoryData<DeepDungeonContentDefinition> AllDefinitions =>
        new(DeepDungeonContentRegistry.Definitions.ToArray());

    [Theory]
    [MemberData(nameof(AllDefinitions))]
    public void EveryCheckedManifestEntryResolvesExactly(DeepDungeonContentDefinition expected)
    {
        var row = new RawContentFinderConditionObservation(
            expected.ContentFinderConditionId,
            expected.TerritoryId,
            expected.DataContentId,
            expected.ShortCode);

        var resolved = DeepDungeonContentRegistry.TryResolve(
            expected.TerritoryId,
            expected.ContentFinderConditionId,
            row,
            out var actual,
            out var reason);

        Assert.True(resolved);
        Assert.Equal(expected, actual);
        Assert.Equal("observation.dungeon.public-identified", reason);
    }

    [Fact]
    public void ManifestContainsAllSupportedBandsWithoutDuplicateIds()
    {
        Assert.Equal(40, DeepDungeonContentRegistry.Definitions.Length);
        Assert.Equal(40, DeepDungeonContentRegistry.Definitions.Select(item => item.ContentFinderConditionId).Distinct().Count());
        Assert.Equal(40, DeepDungeonContentRegistry.Definitions.Select(item => item.DataContentId).Distinct().Count());
        Assert.All(DeepDungeonContentRegistry.Definitions, item => item.FloorRange.Validate());
        Assert.Equal(20, DeepDungeonContentRegistry.Definitions.Count(item => item.Kind == DeepDungeonKind.PalaceOfTheDead));
        Assert.Equal(10, DeepDungeonContentRegistry.Definitions.Count(item => item.Kind == DeepDungeonKind.HeavenOnHigh));
        Assert.Equal(10, DeepDungeonContentRegistry.Definitions.Count(item => item.Kind == DeepDungeonKind.EurekaOrthos));
    }

    [Fact]
    public void LiveVerifiedContent897MapsOnlyToEurekaOrthosOneToTen()
    {
        var resolved = DeepDungeonContentRegistry.TryResolve(
            1099,
            897,
            new RawContentFinderConditionObservation(897, 1099, 60031, "dd003_001"),
            out var definition,
            out _);

        Assert.True(resolved);
        Assert.Equal(DeepDungeonKind.EurekaOrthos, definition!.Kind);
        Assert.Equal(new DeepDungeonFloorRange(1, 10), definition.FloorRange);
    }

    [Theory]
    [InlineData(543, 782, 31)]
    [InlineData(545, 783, 51)]
    [InlineData(547, 784, 71)]
    [InlineData(549, 785, 91)]
    public void NonSequentialHeavenOnHighTerritoriesAreExplicit(uint cfc, uint territory, int floorStart)
    {
        var definition = Assert.Single(DeepDungeonContentRegistry.Definitions, item => item.ContentFinderConditionId == cfc);
        Assert.Equal(territory, definition.TerritoryId);
        Assert.Equal(floorStart, definition.FloorRange.Start);
    }

    [Fact]
    public void UnknownAndMismatchedEvidenceDoNotResolve()
    {
        Assert.False(DeepDungeonContentRegistry.TryResolve(
            1099,
            9999,
            new RawContentFinderConditionObservation(9999, 1099, 60031, "dd003_001"),
            out _,
            out var unknownReason));
        Assert.Equal("observation.dungeon.content-finder-condition-unknown", unknownReason);

        Assert.False(DeepDungeonContentRegistry.TryResolve(
            1100,
            897,
            new RawContentFinderConditionObservation(897, 1099, 60031, "dd003_001"),
            out _,
            out var territoryReason));
        Assert.Equal("observation.dungeon.territory-mismatch", territoryReason);

        Assert.False(DeepDungeonContentRegistry.TryResolve(
            1099,
            897,
            new RawContentFinderConditionObservation(897, 1099, 60031, "wrong"),
            out _,
            out var rowReason));
        Assert.Equal("observation.dungeon.game-data-row-mismatch", rowReason);
    }
}
