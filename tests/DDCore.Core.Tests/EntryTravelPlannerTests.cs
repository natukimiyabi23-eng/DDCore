using DDCore.Core.Navigation;

namespace DDCore.Core.Tests;

public sealed class EntryTravelPlannerTests
{
    private readonly EntryTravelPlanner planner = new();

    [Fact]
    public void CatalogRetainsOfficialDisplayedCoordinates()
    {
        Assert.Equal(new MapCoordinate(21.8m, 8.1m), EurekaOrthosEntryCatalog.Burnel.MapPosition);
        Assert.Equal(new MapCoordinate(34.8m, 19.2m), EurekaOrthosEntryCatalog.Khatun.MapPosition);
    }

    [Theory]
    [InlineData(EntryAccessState.Unknown, "解放状態")]
    [InlineData(EntryAccessState.Locked, "未解放")]
    public void MissingOrLockedAccessBlocks(EntryAccessState access, string expected)
    {
        var plan = planner.Plan(
            DeepDungeonEntryDestination.EurekaOrthos,
            new EntryTravelContext(EntryTravelArea.Other, access, TeleportAvailability.Available));

        Assert.False(plan.IsExecutable);
        Assert.Empty(plan.Steps);
        Assert.Contains(expected, plan.StatusReason, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownCurrentAreaBlocks()
    {
        var plan = planner.Plan(
            DeepDungeonEntryDestination.EurekaOrthos,
            new EntryTravelContext(EntryTravelArea.Unknown, EntryAccessState.Unlocked, TeleportAvailability.Available));

        Assert.Empty(plan.Steps);
        Assert.Contains("現在地", plan.StatusReason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TeleportAvailability.Unknown, "確認できません")]
    [InlineData(TeleportAvailability.Unavailable, "手段がありません")]
    public void CrossAreaRouteRequiresKnownTeleport(TeleportAvailability teleport, string expected)
    {
        var plan = planner.Plan(
            DeepDungeonEntryDestination.EurekaOrthos,
            new EntryTravelContext(EntryTravelArea.Other, EntryAccessState.Unlocked, teleport));

        Assert.Empty(plan.Steps);
        Assert.Contains(expected, plan.StatusReason, StringComparison.Ordinal);
    }

    [Fact]
    public void CrossAreaPreviewContainsTeleportShortcutAndEntrance()
    {
        var plan = planner.Plan(
            DeepDungeonEntryDestination.EurekaOrthos,
            new EntryTravelContext(EntryTravelArea.Other, EntryAccessState.Unlocked, TeleportAvailability.Available));

        Assert.False(plan.IsExecutable);
        Assert.Equal(
            [
                EntryTravelStepKind.TeleportToHub,
                EntryTravelStepKind.NavigateToLandmark,
                EntryTravelStepKind.UseAreaTransition,
                EntryTravelStepKind.NavigateToLandmark,
                EntryTravelStepKind.VerifyArrival,
            ],
            plan.Steps.Select(step => step.Kind));
        Assert.Equal(EurekaOrthosEntryCatalog.Burnel, plan.Steps[1].Landmark);
        Assert.Equal(EurekaOrthosEntryCatalog.Khatun, plan.Steps[3].Landmark);
        Assert.Contains("未接続", plan.StatusReason, StringComparison.Ordinal);
    }

    [Fact]
    public void EntranceAreaPreviewSkipsTeleportAndShortcut()
    {
        var plan = planner.Plan(
            DeepDungeonEntryDestination.EurekaOrthos,
            new EntryTravelContext(
                EntryTravelArea.MorDhonaEightSentinels,
                EntryAccessState.Unlocked,
                TeleportAvailability.Unknown));

        Assert.Equal(
            [EntryTravelStepKind.NavigateToLandmark, EntryTravelStepKind.VerifyArrival],
            plan.Steps.Select(step => step.Kind));
        Assert.All(plan.Steps, step => Assert.Equal(EurekaOrthosEntryCatalog.Khatun, step.Landmark));
    }

    [Theory]
    [InlineData(DeepDungeonEntryDestination.PalaceOfTheDead)]
    [InlineData(DeepDungeonEntryDestination.HeavenOnHigh)]
    public void FutureDestinationsRemainExplicitlyUnsupported(DeepDungeonEntryDestination destination)
    {
        var plan = planner.Plan(
            destination,
            new EntryTravelContext(EntryTravelArea.Other, EntryAccessState.Unlocked, TeleportAvailability.Available));

        Assert.Empty(plan.Steps);
        Assert.Contains("オルト・エウレカ", plan.StatusReason, StringComparison.Ordinal);
    }
}
