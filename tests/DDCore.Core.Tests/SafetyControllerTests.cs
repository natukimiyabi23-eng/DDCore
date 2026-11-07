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
}
