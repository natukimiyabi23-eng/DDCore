using System.Collections.Immutable;
using DDCore.Core.Geometry;
using DDCore.Core.Navigation;

namespace DDCore.Core.Tests;

public sealed class EntryTravelProgressTrackerTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 8, 20, 40, 7, TimeSpan.FromHours(9));
    private static readonly EntryTravelProgressPolicy Policy = new(
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(10),
        0.5);

    [Fact]
    public void PreviewOnlyPlanCannotStart()
    {
        var tracker = new EntryTravelProgressTracker(Policy);
        var plan = new EntryTravelPlanner().Plan(
            DeepDungeonEntryDestination.EurekaOrthos,
            new EntryTravelContext(EntryTravelArea.Other, EntryAccessState.Unlocked, TeleportAvailability.Available));

        var started = tracker.TryStart(plan, Authorization(), At, out var reason);

        Assert.False(started);
        Assert.Equal(EntryTravelRunState.Blocked, tracker.Status.State);
        Assert.Contains("未接続", reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-31)]
    public void StartRequiresFreshNonFutureAuthorization(int seconds)
    {
        var tracker = new EntryTravelProgressTracker(Policy);
        var authorization = new EntryTravelStartAuthorization(Guid.NewGuid(), At.AddSeconds(seconds));

        var started = tracker.TryStart(ExecutablePlan(), authorization, At, out _);

        Assert.False(started);
        Assert.Equal(EntryTravelRunState.Blocked, tracker.Status.State);
    }

    [Fact]
    public void MissingPositionStopsFailClosed()
    {
        var tracker = StartedTracker();

        var status = tracker.Observe(new(At.AddSeconds(1), 0, null, false, false, null));

        Assert.Equal(EntryTravelRunState.Blocked, status.State);
        Assert.Contains("現在位置", status.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ClaimedArrivalWithoutPositionStillStopsFailClosed()
    {
        var tracker = StartedTracker();

        var status = tracker.Observe(new(At.AddSeconds(1), 0, null, false, true, null));

        Assert.Equal(EntryTravelRunState.Blocked, status.State);
        Assert.Contains("現在位置", status.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void NonFinitePositionFaultsWithoutThrowing()
    {
        var tracker = StartedTracker();

        var status = tracker.Observe(new(
            At.AddSeconds(1),
            0,
            new Coordinate2D(double.NaN, 1),
            false,
            false,
            null));

        Assert.Equal(EntryTravelRunState.Faulted, status.State);
        Assert.Contains("有限でない", status.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void EmergencyStopHasImmediatePriority()
    {
        var tracker = StartedTracker();

        var status = tracker.EmergencyStop("UIの緊急停止");
        var after = tracker.Observe(new(At.AddSeconds(1), 0, new Coordinate2D(1, 1), false, true, null));

        Assert.Equal(EntryTravelRunState.Stopped, status.State);
        Assert.Equal(status, after);
    }

    [Fact]
    public void NoPositionProgressBeyondTimeoutBlocks()
    {
        var tracker = StartedTracker();
        tracker.Observe(new(At.AddSeconds(1), 0, new Coordinate2D(10, 10), false, false, null));

        var status = tracker.Observe(new(At.AddSeconds(11), 0, new Coordinate2D(10.1, 10.1), false, false, null));

        Assert.Equal(EntryTravelRunState.Blocked, status.State);
        Assert.Contains("進展", status.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void SufficientMovementResetsStallWindow()
    {
        var tracker = StartedTracker();
        tracker.Observe(new(At.AddSeconds(1), 0, new Coordinate2D(10, 10), false, false, null));
        tracker.Observe(new(At.AddSeconds(9), 0, new Coordinate2D(11, 10), false, false, null));

        var status = tracker.Observe(new(At.AddSeconds(15), 0, new Coordinate2D(11.1, 10), false, false, null));

        Assert.Equal(EntryTravelRunState.Running, status.State);
        Assert.Equal(At.AddSeconds(9), status.LastProgressAt);
    }

    [Fact]
    public void CompletingAllStepsArrivesDeterministically()
    {
        var tracker = StartedTracker();
        tracker.Observe(new(At.AddSeconds(1), 0, new Coordinate2D(1, 1), true, false, null));

        var status = tracker.Observe(new(At.AddSeconds(2), 1, new Coordinate2D(2, 1), true, false, null));

        Assert.Equal(EntryTravelRunState.Arrived, status.State);
        Assert.Equal(2, status.CompletedSteps);
    }

    private static EntryTravelProgressTracker StartedTracker()
    {
        var tracker = new EntryTravelProgressTracker(Policy);
        Assert.True(tracker.TryStart(ExecutablePlan(), Authorization(), At, out _));
        return tracker;
    }

    private static EntryTravelStartAuthorization Authorization() => new(Guid.NewGuid(), At);

    private static EntryTravelPlan ExecutablePlan()
    {
        var steps = ImmutableArray.Create(
            new EntryTravelStep(EntryTravelStepKind.NavigateToLandmark, "step 1", EurekaOrthosEntryCatalog.Burnel, true),
            new EntryTravelStep(EntryTravelStepKind.VerifyArrival, "step 2", EurekaOrthosEntryCatalog.Khatun, false));
        var plan = new EntryTravelPlan(DeepDungeonEntryDestination.EurekaOrthos, steps, true, "test executable plan");
        plan.Validate();
        return plan;
    }
}
