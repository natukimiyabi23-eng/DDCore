using DDCore.Core.Modes;
using DDCore.Core.Safety;

namespace DDCore.Core.Tests;

public sealed class SafetyControllerTests
{
    [Fact]
    public void StartsInObserverWithNoArmedSession()
    {
        var controller = new SafetyController(executionAvailable: false);

        Assert.Equal(OperationMode.Observer, controller.CurrentMode);
        Assert.False(controller.EmergencyStopLatched);
        Assert.False(controller.SessionArmed);
        Assert.False(controller.ExecutionAvailable);
    }

    [Theory]
    [InlineData(OperationMode.Advisor)]
    [InlineData(OperationMode.Simulation)]
    public void AllowsNonOperatingModes(OperationMode requested)
    {
        var controller = new SafetyController(executionAvailable: false);

        var result = controller.RequestMode(requested);

        Assert.True(result.Accepted);
        Assert.Equal(requested, controller.CurrentMode);
    }

    [Theory]
    [InlineData(OperationMode.Assisted)]
    [InlineData(OperationMode.Autonomous)]
    public void RejectsOperatingModesWhenExecutionIsUnavailable(OperationMode requested)
    {
        var controller = new SafetyController(executionAvailable: false);

        var result = controller.RequestMode(requested);

        Assert.False(result.Accepted);
        Assert.Equal(OperationMode.Observer, controller.CurrentMode);
        Assert.Contains("実行機能がない", result.Reason);
    }

    [Fact]
    public void EmergencyStopReturnsToObserverAndBlocksOtherModes()
    {
        var controller = new SafetyController(executionAvailable: false);
        controller.RequestMode(OperationMode.Advisor);

        controller.EmergencyStop("test");
        var blocked = controller.RequestMode(OperationMode.Simulation);

        Assert.Equal(OperationMode.Observer, controller.CurrentMode);
        Assert.True(controller.EmergencyStopLatched);
        Assert.False(blocked.Accepted);
    }

    [Fact]
    public void ResetKeepsObserverAndDoesNotArmExecution()
    {
        var controller = new SafetyController(executionAvailable: false);
        controller.EmergencyStop("test");

        var reset = controller.TryResetEmergencyStop(out _);

        Assert.True(reset);
        Assert.False(controller.EmergencyStopLatched);
        Assert.False(controller.SessionArmed);
        Assert.Equal(OperationMode.Observer, controller.CurrentMode);
    }

    [Fact]
    public void RejectsUndefinedModeWithoutChangingState()
    {
        var controller = new SafetyController(executionAvailable: true);

        var result = controller.RequestMode((OperationMode)999);

        Assert.False(result.Accepted);
        Assert.Equal(OperationMode.Observer, result.PreviousMode);
        Assert.Equal(OperationMode.Observer, result.CurrentMode);
        Assert.False(controller.SessionArmed);
    }

    [Fact]
    public void ExecutionModeRequiresExplicitSessionAuthorization()
    {
        var controller = new SafetyController(executionAvailable: true);

        var beforeAuthorization = controller.RequestMode(OperationMode.Assisted);
        var authorized = controller.TryArmSession(out _);
        var afterAuthorization = controller.RequestMode(OperationMode.Assisted);

        Assert.False(beforeAuthorization.Accepted);
        Assert.True(authorized);
        Assert.True(afterAuthorization.Accepted);
        Assert.Equal(OperationMode.Assisted, controller.CurrentMode);
    }

    [Fact]
    public void ReturningToNonOperatingModeRevokesSessionAuthorization()
    {
        var controller = new SafetyController(executionAvailable: true);
        Assert.True(controller.TryArmSession(out _));
        Assert.True(controller.RequestMode(OperationMode.Assisted).Accepted);

        var advisor = controller.RequestMode(OperationMode.Advisor);
        var retry = controller.RequestMode(OperationMode.Assisted);

        Assert.True(advisor.Accepted);
        Assert.False(controller.SessionArmed);
        Assert.False(retry.Accepted);
    }

    [Fact]
    public void AuthorizationCanOnlyBeGrantedFromObserver()
    {
        var controller = new SafetyController(executionAvailable: true);
        Assert.True(controller.RequestMode(OperationMode.Advisor).Accepted);

        var authorized = controller.TryArmSession(out var reason);

        Assert.False(authorized);
        Assert.Contains("観測モード", reason);
        Assert.False(controller.SessionArmed);
    }

    [Fact]
    public void EmergencyStopResetRequiresFreshAuthorization()
    {
        var controller = new SafetyController(executionAvailable: true);
        Assert.True(controller.TryArmSession(out _));
        Assert.True(controller.RequestMode(OperationMode.Autonomous).Accepted);

        controller.EmergencyStop("boundary test");
        Assert.True(controller.TryResetEmergencyStop(out _));
        var withoutReauthorization = controller.RequestMode(OperationMode.Autonomous);
        Assert.True(controller.TryArmSession(out _));
        var withReauthorization = controller.RequestMode(OperationMode.Autonomous);

        Assert.False(withoutReauthorization.Accepted);
        Assert.True(withReauthorization.Accepted);
    }

    [Fact]
    public void ObserverRequestDoesNotClearEmergencyStopLatch()
    {
        var controller = new SafetyController(executionAvailable: true);
        controller.EmergencyStop("test");

        var result = controller.RequestMode(OperationMode.Observer);

        Assert.True(result.Accepted);
        Assert.True(controller.EmergencyStopLatched);
        Assert.False(controller.SessionArmed);
    }

    [Fact]
    public void ResetWithoutActiveEmergencyStopIsRejected()
    {
        var controller = new SafetyController(executionAvailable: true);

        var reset = controller.TryResetEmergencyStop(out var reason);

        Assert.False(reset);
        Assert.Contains("有効ではありません", reason);
    }

    [Fact]
    public void RestoringEmergencyStopAlwaysReturnsToObserverAndRevokesAuthorization()
    {
        var controller = new SafetyController(executionAvailable: true);
        Assert.True(controller.TryArmSession(out _));
        Assert.True(controller.RequestMode(OperationMode.Assisted).Accepted);

        controller.RestoreEmergencyStop(true, "restored");

        Assert.Equal(OperationMode.Observer, controller.CurrentMode);
        Assert.True(controller.EmergencyStopLatched);
        Assert.False(controller.SessionArmed);
        Assert.Equal("restored", controller.LastStopReason);
    }
}
